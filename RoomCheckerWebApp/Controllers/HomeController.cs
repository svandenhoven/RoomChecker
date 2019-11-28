/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomChecker.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using RoomChecker.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using RoomChecker.Extensions;
using Microsoft.PowerBI.Api.V2;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Identity.Web;
using Graph = Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.WindowsAzure.Storage.Auth;

namespace RoomChecker.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private IMemoryCache _cache;
        private readonly IOptions<RoomsConfig> _roomsConfig;
        private List<bGridAsset> _bGridAssets = new List<bGridAsset>();
        private TenantConfig _tenantConfig;
        readonly ITokenAcquisition _tokenAcquisition;
        readonly WebOptions _webOptions;
        private string _tenantId;

        public HomeController(IConfiguration configuration, 
            IHostingEnvironment hostingEnvironment, 
            IMemoryCache memoryCache, 
            IOptions<RoomsConfig> roomsConfig, 
            ITokenAcquisition tokenAcquisition,
            IOptions<WebOptions> webOptionValue)
        {
            _configuration = configuration;
            _env = hostingEnvironment;
            _cache = memoryCache;
            _roomsConfig = roomsConfig;
            _tokenAcquisition = tokenAcquisition;
            _webOptions = webOptionValue.Value;
        }

        private Graph::GraphServiceClient GetGraphServiceClient(string[] scopes, string tenantId)
        {
            return GraphServiceClientFactory.GetAuthenticatedGraphClient(async () =>
            {
                string result = await _tokenAcquisition.GetAccessTokenOnBehalfOfUserAsync(scopes);
                return result;
            }, _webOptions.GraphApiUrl);
        }


        [AllowAnonymous]
        // Load user's profile.
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(policy: "MSFTOnly")]
        [AuthorizeForScopes(Scopes = new[] { "https://analysis.windows.net/powerbi/api/.default" })]
        public IActionResult Dashboard()
        {
            _tenantId = GetTenantId(null);
            _tenantConfig = ReadConfig(_roomsConfig).Result;

            if (_tenantConfig.PBIConfig == null)
            {
                return View();
            }
            var workspaceId = _tenantConfig.PBIConfig.WorkspaceId;
            var reportId = _tenantConfig.PBIConfig.ReportId;
            var powerBiApiUrl = "https://api.powerbi.com/";
            string[] pBIScopes = { "https://analysis.windows.net/powerbi/api/.default" };

            var pbiAccessToken = _tokenAcquisition.GetAccessTokenOnBehalfOfUserAsync(pBIScopes).Result;

            using (var client = new PowerBIClient(new Uri(powerBiApiUrl), new TokenCredentials(pbiAccessToken, "Bearer")))
                {
                    Microsoft.PowerBI.Api.V2.Models.Report report = null;

                    if (!string.IsNullOrEmpty(workspaceId))
                    {
                        report = client.Reports.GetReportInGroup(workspaceId, reportId);
                    }

                    if (report != null)
                    {
                        ViewBag.EmbedUrl = report.EmbedUrl;
                        ViewBag.ReportId = report.Id;
                        ViewBag.ReportName = report.Name;
                        ViewBag.AccessToken = pbiAccessToken;
                    }
                }
            return View();
        }

        [Authorize]
        public async Task<JsonResult> GetRoomStatusOnDate(string roomId, string dateTime, string type, string tenantName)
        {
            _tenantId = GetTenantId(tenantName);
            _tenantConfig = await ReadConfig(_roomsConfig);

            var checkDateTime = DateTime.Parse(dateTime);
            var timediff = DateTime.Now - checkDateTime;

            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            checkDateTime = TimeZoneInfo.ConvertTimeToUtc(checkDateTime, tz);

            var room = new Room(24);
            var rooms = GetRooms(type).Where<Room>(r => r.Id == roomId);
            if (rooms.Count() > 0)
                room = rooms.First();
            else
            {
                room.Id = roomId;
                room.Name = roomId.Split('@')[0];
                room.HasMailBox = true;
            };


            if (Math.Abs(timediff.TotalMinutes) > 30)
            {
                room = await GetRoomData(room, checkDateTime, _tenantId);
            }
            else
            {
                if (!_cache.TryGetValue(_tenantId + "_"+roomId, out Room cachedRoom))
                {
                    room = await GetRoomData(room, checkDateTime, _tenantId);
                    // Save data in cache.
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                    _cache.Set(_tenantId + "_" + roomId, room, cacheEntryOptions);
                }
                else
                {
                    room = cachedRoom;
                }
            }

            return Json(room);
        }


        [Authorize]
        public async Task<IActionResult> Rooms(string tenantName = null)
        {
            _tenantId = GetTenantId(tenantName);

            var rooms = GetRooms("meet");
            ViewBag.Message = "";
            ViewBag.Tenant = tenantName;
            return View(rooms);
        }

        private string GetTenantId(string tenantName)
        {
            string tenantId = null;
            if (tenantName == null)
            {
                tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;
            }
            else
            {
                switch (tenantName.ToLower())
                {
                    case "microsoft305":
                        tenantId = "9e021cc9-7821-437d-af4f-41ae85cc1ca5";
                        break;
                    default:
                        tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;
                        break;
                }
            }
            return tenantId;
        }

        [Authorize]
        [AuthorizeForScopes(Scopes = new[] { "User.ReadBasic.All" })]
        public async Task<IActionResult> O365Rooms(string tenantName = null, string roomListAddress = null)
        {
            _tenantId = GetTenantId(tenantName);
            _tenantConfig = await ReadConfig(_roomsConfig);
            roomListAddress = roomListAddress ?? _tenantConfig.PreferredRoomList;

            var accessToken = _tokenAcquisition.GetAccessTokenOnBehalfOfUserAsync(new[] { "User.ReadBasic.All" }).Result;
            var roomLists = await GraphService.GetRoomLists(accessToken);
            roomListAddress = roomListAddress ?? roomLists.value.FirstOrDefault().Address;

            var rooms = await GraphService.GetRooms(accessToken, roomListAddress);

            ViewBag.Tenant = tenantName;
            ViewBag.RoomListAddress = roomListAddress;
            return View(new O365Rooms { RoomLists = roomLists, Rooms = rooms});
        }

        [Authorize]
        public async Task<IActionResult> WorkRooms(string tenantName = null)
        {
            _tenantId = GetTenantId(tenantName);
            var rooms = GetRooms("work");
            ViewBag.Message = "";
            return View(rooms);
        }

        [Authorize]
        public async Task<IActionResult> Assets(string tenantName = null)
        {
            _tenantId = GetTenantId(tenantName);

            var assets = new List<bGridAsset>();
            _tenantConfig = await ReadConfig(_roomsConfig);

            if (_tenantConfig.KnownAssets == null || _tenantConfig.KnownAssets.Count == 0)
                return View(assets);

            await GetbGridAssets();
            var knowAssets = _tenantConfig.KnownAssets;
            var assetsList = _bGridAssets.Where(a => knowAssets.Count(k => k.Id == a.id) > 0);


            foreach(var asset in assetsList)
            {
                asset.assetType = knowAssets.Where(k => k.Id == asset.id).First().Type;
                asset.assetName = knowAssets.Where(k => k.Id == asset.id).First().Name;
                assets.Add(asset);
            }
            return View(assets);
            
        }

        [AllowAnonymous]
        public IActionResult TeamsRooms(string tenantName = null)
        {
            _tenantId = GetTenantId(tenantName);
            var rooms = GetRooms("meet");
            return View(rooms);
        }

        private async Task<Room> GetRoomData(Room room, DateTime dt, string tenantId)
        {
            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;

            var azureOptions = new AzureAdOptions();
            _configuration.Bind("AzureAd", azureOptions);

            if (room.HasMailBox)
            {
                Graph::GraphServiceClient graphClient = GetGraphServiceClient(new[] { "Calendars.Read.Shared" }, tenantId);
                room = await GraphService.GetRoomAvailability(graphClient, room, HttpContext, dt);
            }

            if (_tenantConfig.bGridConfig.bGridUser != "")
            {
                var roomOccupancies = await GetbGridOccupancies();
                var roomTemperatures = await GetbGridTemperatures();

                if (room.Nodes != null)
                {
                    var roomNodes = roomOccupancies.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
                    if (roomNodes != null)
                    {
                        var occupiedNodes = roomNodes.Where(nodes => nodes.value == 2);
                        room.Occupied = occupiedNodes == null ? -1 : occupiedNodes.Count() > 0 ? 2 : 0;

                        //Get Associated Island
                        //var islands = _bGridIslands.Where(i => i.locations.Any(l => room.Nodes.Any(n => Convert.ToInt32(n.Id).Equals(l))));
                        //if(islands != null)
                        //{
                        //    room.Island = islands.First();
                        //}
                    }

                    var roomNodesTemp = roomTemperatures.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
                    if (roomNodesTemp != null)
                    {
                        var roomNodesTempLatest = roomNodesTemp.GroupBy(r => r.location_id).Select(ro => ro.OrderByDescending(x => x.timestamp).FirstOrDefault());
                        if (roomNodesTemp.Count() > 0)
                        {
                            var avgTemp = roomNodesTemp.Average(r => Convert.ToDecimal(r.value));
                            room.Temperature = avgTemp;
                        }
                    }
                }
            }

            return room;
        }

        private async Task<List<bGridOccpancy>> GetbGridOccupancies()
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;

            if (!_cache.TryGetValue(tenantId + "_bGridOccupancies", out List<bGridOccpancy> cachedRoomOccupancies))
            {
                var roomOccupancies = await BuildingActionHelper.ExecuteGetAction<List<bGridOccpancy>>("api/occupancy/office", _tenantConfig.bGridConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set(tenantId + "_bGridOccupancies", roomOccupancies, cacheEntryOptionsShort);
                return roomOccupancies;
            }
            else
            {
                return cachedRoomOccupancies;
            }
        }

        private async Task<List<bGridTemperature>> GetbGridTemperatures()
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;

            if (!_cache.TryGetValue(tenantId + "_bGridTemperatures", out List<bGridTemperature> cachedRoomTemperatures))
            {
                var roomTemperatures = await BuildingActionHelper.ExecuteGetAction<List<bGridTemperature>>("api/locations/recent/temperature", _tenantConfig.bGridConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set(tenantId + "_bGridTemperatures", roomTemperatures, cacheEntryOptionsShort);
                return roomTemperatures;
            }
            else
            {
                return cachedRoomTemperatures;
            }
        }

        private async Task<List<bGridIsland>> GetbGridIslands()
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;

            if (!_cache.TryGetValue(tenantId + "_bGridLocations", out List<bGridIsland> cachedIslands))
            {
                var bGridIslands = await BuildingActionHelper.ExecuteGetAction<List<bGridIsland>>("api/islands", _tenantConfig.bGridConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set(tenantId + "_bGridLocations", bGridIslands, cacheEntryOptionsShort);
                return bGridIslands;
            }
            else
            {
                return cachedIslands;
            }
        }

        private async Task GetbGridAssets()
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;

            if (!_cache.TryGetValue(tenantId + "_bGridAssets", out List<bGridAsset> cachedAssets))
            {
                _bGridAssets = await BuildingActionHelper.ExecuteGetAction<List<bGridAsset>>("api/assets", _tenantConfig.bGridConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime/5));
                _cache.Set(tenantId + "_bGridAssets", _bGridAssets, cacheEntryOptionsShort);
            }
            else
            {
                _bGridAssets = cachedAssets;
            }
        }

        private List<Room> GetRooms(string type)
        {
            _tenantConfig = ReadConfig(_roomsConfig).Result;
            if(_tenantConfig.Rooms != null)
                return _tenantConfig.Rooms.Where(r => r.RoomType == type).OrderBy(r => r.Name).ToList<Room>();
            else
                return new List<Room>();
        }


        private async Task<TenantConfig> ReadConfig(IOptions<RoomsConfig> roomsConfig)
        {
            //var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault();

            if (!_cache.TryGetValue(_tenantId+"_tenantConfig", out TenantConfig cachedConfig))
            {
                TenantConfig tenantConfig = new TenantConfig();
                if (roomsConfig.Value == null)
                {
                    return cachedConfig;
                }

                var blobName = "dev-" + _tenantId + ".json";

                switch (roomsConfig.Value.FileLocation)
                {
                    case "LocalFile":
                        var json = System.IO.File.ReadAllText(roomsConfig.Value.URI);
                        tenantConfig = JsonConvert.DeserializeObject<TenantConfig>(json);
                        break;
                    case "AzureStorageFile":
                        using (var webClient = new WebClient())
                        {
                            var jsonWC = webClient.DownloadString(roomsConfig.Value.URI);
                            tenantConfig = JsonConvert.DeserializeObject<TenantConfig>(jsonWC);
                        }
                        break;
                    case "AzureStorageContainerAndTenantIdSAS":
                        var blobContainer = new CloudBlobContainer(new Uri(roomsConfig.Value.URI));
                        CloudBlockBlob blob = blobContainer.GetBlockBlobReference(blobName);
                        if (blob.ExistsAsync().Result)
                        {
                            var jsonTidSAS = blob.DownloadTextAsync().Result;
                            tenantConfig = JsonConvert.DeserializeObject<TenantConfig>(jsonTidSAS);
                        }
                        else
                        {
                            return tenantConfig;
                        }
                        break;
                    case "AzureStorageContainerAndTenantIdMI":
                        var jsonTidMI = await ReadConfigBlob(blobName);
                        if(jsonTidMI != null)
                        {
                            tenantConfig = JsonConvert.DeserializeObject<TenantConfig>(jsonTidMI);
                        }
                        else
                        {
                            return tenantConfig;
                        }
                        break;
                    default:
                        break;
                }

                if (tenantConfig.bGridConfig.bGridUser != "")
                {
                    var bGridlocations = await BuildingActionHelper.ExecuteGetAction<List<bGridLocation>>("api/locations", tenantConfig.bGridConfig);
                    if (bGridlocations != null)
                    {
                        foreach (var room in tenantConfig.Rooms)
                        {
                            if (room.Nodes.Count > 0)
                            {
                                var bGridLocation = room.Nodes.First().Id;
                                room.X = Convert.ToInt32(bGridlocations.Where(b => b.id.ToString() == bGridLocation).First().x);
                                room.Y = Convert.ToInt32(bGridlocations.Where(b => b.id.ToString() == bGridLocation).First().y);
                            }

                        }
                    }
                }
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set(_tenantId + "_tenantConfig", tenantConfig, cacheEntryOptions);
                return tenantConfig;
            }
            else
            {
                return cachedConfig;
            }
        }

        private async Task<string> ReadConfigBlob(string FileName)
        {
            var accessToken = await GetAccessTokenAsync();

            var tokenCredential = new TokenCredential(accessToken);
            var storageCredentials = new StorageCredentials(tokenCredential);
            var blobContainer = new CloudBlobContainer(new Uri($"https://mindparkstorage.blob.core.windows.net/roomchecker"), storageCredentials);
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference(FileName);

            if (await blob.ExistsAsync())
            {
                return await blob.DownloadTextAsync();
            }
            else
            {
                return null;
            }
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var tokenProvider = new AzureServiceTokenProvider();
            return await tokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult TOU()
        {
            return View();
        }
    }
}
