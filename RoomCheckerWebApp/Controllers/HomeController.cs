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
        public async Task<IActionResult> Index(string email)
        {
            if (User.Identity.IsAuthenticated)
            {

            }

            return View();
        }

        [AllowAnonymous]
        // Load user's profile.
        public async Task<IActionResult> Reserve(string Id)
        {
            var id = Id;
            return View();
        }

        public async Task<JsonResult> GetRoomStatus(string roomId)
        {
            var room = GetRooms().Where<Room>(r => r.Name == roomId).First();

            if (User.Identity.IsAuthenticated)
            {
                var identity = (ClaimsIdentity)User.Identity;
                if (identity != null && identity.Claims != null && identity.Claims.Any())
                {
                    var upn = identity.FindFirst(ClaimTypes.Upn);
                }
                

                if (!_cache.TryGetValue(roomId, out Room cachedRoom))
                {
                    if (!_cache.TryGetValue("bGridOccupancies", out List<bGridOccpancy> cachedRoomOccupancies))
                    {
                        _roomOccupancies = await BuildingActionHelper.ExecuteGetAction<List<bGridOccpancy>>("api/occupancy/office",_roomsConfig);
                        var cacheEntryOptionsShort = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(_roomsConfig.Value.CacheTime));
                        _cache.Set("bGridOccupancies", _roomOccupancies, cacheEntryOptionsShort);
                    }
                    else
                    {
                        _roomOccupancies = cachedRoomOccupancies;
                    }

                    var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
                    var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier);
                    room = await GraphService.GetRoomAvailability(graphClient, room, HttpContext);
                    if (room.Nodes != null)
                    {
                        var roomNodes = _roomOccupancies.Where(r => room.Nodes.Where(ro => ro.Id == r.location_id.ToString()).Count() > 0);
                        if (roomNodes != null)
                        {
                            var occupiedNodes = roomNodes.Where(nodes => nodes.value == 2);
                            room.Occupied = occupiedNodes == null ? -1 : occupiedNodes.Count() > 0 ? 2 : 0;
                        }

                        
                    }
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
                var rooms = GetRooms();
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

        [AllowAnonymous]
        public async Task<IActionResult> TeamsRooms()
        {
            if (User.Identity.IsAuthenticated)
            {
                var rooms = GetRooms();
                ViewBag.Message = "";
                return View(rooms);
            }
            else
            {
                ViewBag.Message = "Please Sign-In first.";
                return View(new List<Room>());
            }
        }

        private List<Room> GetRooms()
        {
            return ReadRooms(_roomsConfig);
        }

        private List<Room> ReadRooms(IOptions<RoomsConfig> roomsConfig)
        {
            if (!_cache.TryGetValue("roomslist", out List<Room> cachedRooms))
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

                rooms = rooms.OrderBy(r => r.Name).ToList<Room>();
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
