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

namespace RoomChecker.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private readonly IGraphSdkHelper _graphSdkHelper;
        private IMemoryCache _cache;
        private readonly IOptions<RoomsConfig> _roomsConfig;
        private List<bGridAsset> _bGridAssets = new List<bGridAsset>();
        private TenantConfig _tenantConfig;

        public HomeController(IConfiguration configuration, IHostingEnvironment hostingEnvironment, IGraphSdkHelper graphSdkHelper, IMemoryCache memoryCache, IOptions<RoomsConfig> roomsConfig)
        {
            _configuration = configuration;
            _env = hostingEnvironment;
            _graphSdkHelper = graphSdkHelper;
            _cache = memoryCache;
            _roomsConfig = roomsConfig;
        }

        [AllowAnonymous]
        // Load user's profile.
        public IActionResult Index(string email)
        {
            if (User.Identity.IsAuthenticated)
            {

            }

            return View();
        }

        [Authorize]
        public IActionResult Dashboard()
        {
            var roomsConfig = new RoomsConfig();
            _configuration.Bind("RoomsConfig", roomsConfig);

            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            string[] pBIScopes = { "https://analysis.windows.net/powerbi/api/.default" };
            var pbiAccessToken = _graphSdkHelper.GetPBIAccessToken(identifier, pBIScopes);
            //if (pbiAccessToken != null)
            //{
            var workspaceId = roomsConfig.WorkspaceId;
            var reportId = roomsConfig.ReportId;
            var powerBiApiUrl = "https://api.powerbi.com/";

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
                //}
            }
            return View();
        }

        [AllowAnonymous]
        // Load user's profile.
        public IActionResult Reserve(string Id)
        {
            var id = Id;
            return View();
        }

        [Authorize]
        public async Task<JsonResult> GetRoomStatusOnDate(string roomId, string dateTime, string type)
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault().Value;

            var checkDateTime = DateTime.Parse(dateTime);
            var timediff = DateTime.Now - checkDateTime;

            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            checkDateTime = TimeZoneInfo.ConvertTimeToUtc(checkDateTime, tz);

            var room = GetRooms(type).Where<Room>(r => r.Name == roomId).First();

            if (Math.Abs(timediff.TotalMinutes) > 30)
            {
                room = await GetRoomData(room, checkDateTime);
            }
            else
            {
                if (!_cache.TryGetValue(tenantId+"_"+roomId, out Room cachedRoom))
                {
                    room = await GetRoomData(room, checkDateTime);
                    // Save data in cache.
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                    _cache.Set(tenantId + "_" + roomId, room, cacheEntryOptions);
                }
                else
                {
                    room = cachedRoom;
                }
            }

            return Json(room);
        }


        [Authorize]
        public IActionResult Bot()
        {
            return View();
        }

        [Authorize]
        public IActionResult Rooms()
        {
            var rooms = GetRooms("meet");
            ViewBag.Message = "";
            return View(rooms);

        }

        [Authorize]
        public IActionResult WorkRooms()
        {
            var rooms = GetRooms("work");
            ViewBag.Message = "";
            return View(rooms);
        }

        [Authorize]
        public async Task<IActionResult> Assets()
        {
            _tenantConfig = ReadConfig(_roomsConfig);

            await GetbGridAssets();
            var knowAssets = new int[] { 5448, 5451, 5465, 5656 };
            var assetsList = _bGridAssets.Where(a => knowAssets.Contains(a.id));
            var assets = new List<bGridAsset>();

            foreach(var asset in assetsList)
            {
                switch(asset.id)
                {
                    case 5448:
                        asset.assetType = "surfacehub.png";
                        asset.assetName = "Surface Hub";
                        break;
                    case 5451:
                        asset.assetType = "postcar.jpg";
                        asset.assetName = "Trolley 1";
                        break;
                    case 5465:
                        asset.assetType = "cleantrolley.jpg";
                        asset.assetName = "Cleaning 1";
                        break;
                    case 5656:
                        asset.assetType = "headbhv.jpg";
                        asset.assetName = "Head BHV";
                        break;
                    default:
                        asset.assetType = "unknowntype.jpg";
                        asset.assetName = "Unknown Asset";
                        break;
                }
                assets.Add(asset);
            }
            return View(assets);
            
        }

        [AllowAnonymous]
        public IActionResult TeamsRooms()
        {
            var rooms = GetRooms("meet");
            return View(rooms);
        }

        private async Task<Room> GetRoomData(Room room, DateTime dt)
        {
            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;

            var azureOptions = new AzureAdOptions();
            _configuration.Bind("AzureAd", azureOptions);

            var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier, azureOptions.GraphScopes.Split(new[] { ' ' }));
            room = await GraphService.GetRoomAvailability(graphClient, room, HttpContext, dt);
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
            _tenantConfig = ReadConfig(_roomsConfig);
            var rooms = _tenantConfig.Rooms.Where(r => r.RoomType == type).OrderBy(r => r.Name).ToList<Room>();
            return rooms;
        }


        private TenantConfig ReadConfig(IOptions<RoomsConfig> roomsConfig)
        {
            var tenantId = User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid").FirstOrDefault();

            if (!_cache.TryGetValue(tenantId+"_tenantConfig", out TenantConfig cachedConfig))
            {
                TenantConfig tenantConfig = new TenantConfig();
                if (roomsConfig.Value == null)
                {
                    return cachedConfig;
                }

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
                    case "AzureStorageContainerAndTenantId":
                        var blobContainer = new CloudBlobContainer(new Uri(roomsConfig.Value.URI));
                        var blobName = "dev-" + tenantId.Value + ".json";
                        CloudBlockBlob blob = blobContainer.GetBlockBlobReference(blobName);
                        if(blob.ExistsAsync().Result)
                        {
                            var jsonTid = blob.DownloadTextAsync().Result;
                            tenantConfig = JsonConvert.DeserializeObject<TenantConfig>(jsonTid);
                        }
                        break;
                    default:
                        break;
                }

                if (tenantConfig.bGridConfig.bGridUser != "")
                {
                    var bGridlocations = BuildingActionHelper.ExecuteGetAction<List<bGridLocation>>("api/locations", tenantConfig.bGridConfig).Result;
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
                _cache.Set(tenantId + "_tenantConfig", tenantConfig, cacheEntryOptions);
                return tenantConfig;
            }
            else
            {
                return cachedConfig;
            }
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
