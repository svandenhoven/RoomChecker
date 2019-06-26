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

        public async Task<IActionResult> Dashboard()
        {
            //var azureOptions = new AzureAdOptions();
            //_configuration.Bind("AzureAd", azureOptions);

            //var embedService = new EmbedService(azureOptions);
            //var result = await embedService.EmbedReport("", "");
            return View();
        }

        [AllowAnonymous]
        // Load user's profile.
        public IActionResult Reserve(string Id)
        {
            var id = Id;
            return View();
        }

         public async Task<JsonResult> GetRoomStatusOnDate(string roomId, string dateTime, string type)
        {
            var checkDateTime = DateTime.Parse(dateTime);
            var timediff = DateTime.Now - checkDateTime;

            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            checkDateTime = TimeZoneInfo.ConvertTimeToUtc(checkDateTime, tz);

            var room = GetRooms(type).Where<Room>(r => r.Name == roomId).First();

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

        private async Task<Room> GetRoomData(Room room, DateTime dt)
        {
            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier);
            room = await GraphService.GetRoomAvailability(graphClient, room, HttpContext, dt);
            if (room.Nodes != null)
            {
                var roomNodes = _roomOccupancies.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
                if (roomNodes != null)
                {
                    var occupiedNodes = roomNodes.Where(nodes => nodes.value == 2);
                    room.Occupied = occupiedNodes == null ? -1 : occupiedNodes.Count() > 0 ? 2 : 0;
                }

                var roomNodesTemp = _roomTemperatures.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
                if (roomNodesTemp != null)
                {
                    var roomNodesTempLatest = roomNodesTemp.GroupBy(r => r.location_id).Select(ro => ro.OrderByDescending(x => x.timestamp).FirstOrDefault());
                    var avgTemp = roomNodesTemp.Average(r => Convert.ToDecimal(r.value));
                    room.Temperature = avgTemp;
                }
            }

            return room;
        }

        [Authorize]
        public async Task<IActionResult> Rooms()
        {
            if (User.Identity.IsAuthenticated)
            {
                if (!_cache.TryGetValue("bGridOccupancies", out List<bGridOccpancy> cachedRoomOccupancies))
                {
                    _roomOccupancies = await BuildingActionHelper.ExecuteGetAction<List<bGridOccpancy>>("api/occupancy/office",_roomsConfig);
                    var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60));
                    _cache.Set("bGridOccupancies", _roomOccupancies, cacheEntryOptionsShort);
                }
                else
                {
                    _roomOccupancies = cachedRoomOccupancies;
                }

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
                if (!_cache.TryGetValue("bGridOccupancies", out List<bGridOccpancy> cachedRoomOccupancies))
                {
                    _roomOccupancies = await BuildingActionHelper.ExecuteGetAction<List<bGridOccpancy>>("api/occupancy/office", _roomsConfig);
                    var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60));
                    _cache.Set("bGridOccupancies", _roomOccupancies, cacheEntryOptionsShort);
                }
                else
                {
                    _roomOccupancies = cachedRoomOccupancies;
                }

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

        [AllowAnonymous]
        public IActionResult TeamsRooms()
        {
                var rooms = GetRooms("meet");
                return View(rooms);
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

                rooms = rooms.Where(r => r.RoomType == roomType).OrderBy(r => r.Name).ToList<Room>();
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                _cache.Set("roomslist", rooms, cacheEntryOptions);
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
