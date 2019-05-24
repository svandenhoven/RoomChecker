//using MicrosoftGraphAspNetCoreConnectSample.Helpers;
//using MicrosoftGraphAspNetCoreConnectSample.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace RoomChecker.Models
//{

//    public class RoomsCache
//    {
//        private DateTime LastRefresh;

//        public  bool Fresh()
//        {
//            if ((DateTime.Now - LastRefresh).Seconds < 60)
//                return true;
//            else
//                return false;
//        }

//        public List<Room> Cache = new List<Room>();
//    }

//    public static class CacheSingleton
//    {
//        private RoomsCache _cache;

//        public void Init()
//        {
//            _cache = new RoomsCache();
//        }
//    }

//}
