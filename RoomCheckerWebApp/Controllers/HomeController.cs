/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicrosoftGraphAspNetCoreConnectSample.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.AspNetCore.Hosting;
using MicrosoftGraphAspNetCoreConnectSample.Models;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using RoomChecker.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Net;
using RoomChecker.Helpers;
using Microsoft.Identity.Client;
using MicrosoftGraphAspNetCoreConnectSample.Extensions;
using Microsoft.PowerBI.Api.V2;
using Microsoft.Rest;
//using RoomChecker.Models;

namespace MicrosoftGraphAspNetCoreConnectSample.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private readonly IGraphSdkHelper _graphSdkHelper;
        private IMemoryCache _cache;
        private readonly IOptions<RoomsConfig> _roomsConfig;
        private List<bGridOccpancy> _roomOccupancies = new List<bGridOccpancy>();
        private List<bGridTemperature> _roomTemperatures = new List<bGridTemperature>();
        private List<bGridIsland> _bGridIslands = new List<bGridIsland>();
        private List<bGridAsset> _bGridAssets = new List<bGridAsset>();

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
            var checkDateTime = DateTime.Parse(dateTime);
            var timediff = DateTime.Now - checkDateTime;

            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            checkDateTime = TimeZoneInfo.ConvertTimeToUtc(checkDateTime, tz);

            var room = GetRooms(type).Where<Room>(r => r.Name == roomId).First();

            await GetbGridOccupancies();
            await GetbGridTemperatures();
            //           await GetbGridIslands();

            if (Math.Abs(timediff.TotalMinutes) > 30)
            {
                room = await GetRoomData(room, checkDateTime);
            }
            else
            {
                if (!_cache.TryGetValue(roomId, out Room cachedRoom))
                {
                    room = await GetRoomData(room, checkDateTime);
                    // Save data in cache.
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                    _cache.Set(roomId, room, cacheEntryOptions);
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
        public async Task<IActionResult> Rooms()
        {
            if (User.Identity.IsAuthenticated)
            {
                await GetbGridOccupancies();
                await GetbGridTemperatures();
                //await GetbGridIslands();

                var rooms = GetRooms("meet");
                ViewBag.Message = "";
                return View(rooms);
            }
            else
            {
                ViewBag.Message = "Please Sign-In first.";
                return View(new List<Room>());
            }

            #region serverside roomscheck
            //try
            //{

            //    if (User.Identity.IsAuthenticated)
            //    {
            //        if (!_cache.TryGetValue("_Rooms", out List<Room> roomsAvailability))
            //        {
            //            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            //            var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier);
            //            roomsAvailability = await GraphService.GetRoomsAvailabilityAsync(graphClient, HttpContext, rooms);
            //            if (roomsAvailability != null)
            //            {
            //                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
            //                // Save data in cache.
            //                _cache.Set("_Rooms", roomsAvailability, cacheEntryOptions);
            //                var json = JsonConvert.SerializeObject(roomsAvailability);
            //            }
            //        }
            //        ViewBag.Message = "";
            //        return View(roomsAvailability);
            //    }
            //    ViewBag.Message = "Please Sign-In first.";
            //    return View(new List<Room>());
            //}
            //catch (Exception ex)
            //{
            //    ViewBag.Message = ex.Message;
            //    return View(new List<Room>());
            //}
            #endregion

        }

        [Authorize]
        public async Task<IActionResult> WorkRooms()
        {
            if (User.Identity.IsAuthenticated)
            {
                await GetbGridOccupancies();
                await GetbGridTemperatures();
                //await GetbGridIslands();

                var rooms = GetRooms("work");
                ViewBag.Message = "";
                return View(rooms);
            }
            else
            {
                ViewBag.Message = "Please Sign-In first.";
                return View(new List<Room>());
            }
        }

        [Authorize]
        public async Task<IActionResult> Assets()
        {
            await GetbGridAssets();
            var knowAssets = new int[] { 5472, 5448, 5451, 5465, 5656 };
            var assetsList = _bGridAssets.Where(a => knowAssets.Contains(a.id));
            var assets = new List<bGridAsset>();

            foreach(var asset in assetsList)
            {
                asset.assetType = "surfacehub2.jpg";
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

        private async Task GetbGridTemperatures()
        {
            if (!_cache.TryGetValue("bGridTemperatures", out List<bGridTemperature> cachedRoomTemperatures))
            {
                _roomTemperatures = await BuildingActionHelper.ExecuteGetAction<List<bGridTemperature>>("api/locations/recent/temperature", _roomsConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set("bGridTemperatures", _roomTemperatures, cacheEntryOptionsShort);
            }
            else
            {
                _roomTemperatures = cachedRoomTemperatures;
            }
        }


        private async Task<Room> GetRoomData(Room room, DateTime dt)
        {
            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;

            var azureOptions = new AzureAdOptions();
            _configuration.Bind("AzureAd", azureOptions);

            var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier, azureOptions.GraphScopes.Split(new[] { ' ' }));
            room = await GraphService.GetRoomAvailability(graphClient, room, HttpContext, dt);
            if (room.Nodes != null)
            {
                var roomNodes = _roomOccupancies.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
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

                var roomNodesTemp = _roomTemperatures.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
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

            return room;
        }

        private async Task GetbGridOccupancies()
        {
            if (!_cache.TryGetValue("bGridOccupancies", out List<bGridOccpancy> cachedRoomOccupancies))
            {
                _roomOccupancies = await BuildingActionHelper.ExecuteGetAction<List<bGridOccpancy>>("api/occupancy/office", _roomsConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set("bGridOccupancies", _roomOccupancies, cacheEntryOptionsShort);
            }
            else
            {
                _roomOccupancies = cachedRoomOccupancies;
            }
        }

        private async Task GetbGridIslands()
        {
            if (!_cache.TryGetValue("bGridLocations", out List<bGridIsland> cachedIslands))
            {
                _bGridIslands = await BuildingActionHelper.ExecuteGetAction<List<bGridIsland>>("api/islands", _roomsConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set("bGridLocations", _bGridIslands, cacheEntryOptionsShort);
            }
            else
            {
                _bGridIslands = cachedIslands;
            }
        }

        private async Task GetbGridAssets()
        {
            if (!_cache.TryGetValue("bGridAssets", out List<bGridAsset> cachedAssets))
            {
                _bGridAssets = await BuildingActionHelper.ExecuteGetAction<List<bGridAsset>>("api/assets", _roomsConfig);
                var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set("bGridAssets", _bGridAssets, cacheEntryOptionsShort);
            }
            else
            {
                _bGridAssets = cachedAssets;
            }
        }

        private List<Room> GetRooms(string type)
        {
            return ReadRooms(_roomsConfig, type);
        }


        private List<Room> ReadRooms(IOptions<RoomsConfig> roomsConfig, string roomType)
        {
            if (!_cache.TryGetValue(roomType+"roomslist", out List<Room> cachedRooms))
            {
                List<Room> rooms = new List<Room>();
                if (roomsConfig.Value == null)
                {
                    return rooms;
                }

                switch (roomsConfig.Value.FileLocation)
                {
                    case "LocalFile":
                        var json = System.IO.File.ReadAllText(roomsConfig.Value.URI);
                        rooms = JsonConvert.DeserializeObject<List<Room>>(json);
                        break;
                    case "AzureStorageFile":
                        var webClient = new WebClient();
                        var jsonAzure = webClient.DownloadString(roomsConfig.Value.URI);
                        rooms = JsonConvert.DeserializeObject<List<Room>>(jsonAzure);
                        break;
                    default:
                        break;
                }

                var bGridlocations = BuildingActionHelper.ExecuteGetAction<List<bGridLocation>>("api/locations", _roomsConfig).Result;
                if (bGridlocations != null)
                {
                    foreach (var room in rooms)
                    {
                        if (room.Nodes.Count > 0)
                        {
                            var bGridLocation = room.Nodes.First().Id;
                            room.X = Convert.ToInt32(bGridlocations.Where(b => b.id.ToString() == bGridLocation).First().x);
                            room.Y = Convert.ToInt32(bGridlocations.Where(b => b.id.ToString() == bGridLocation).First().y);
                        }

                    }
                }
                rooms = rooms.Where(r => r.RoomType == roomType).OrderBy(r => r.Name).ToList<Room>();
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set(roomType + "roomslist", rooms, cacheEntryOptions);
                return rooms;
            }
            else
            {
                return cachedRooms;
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
