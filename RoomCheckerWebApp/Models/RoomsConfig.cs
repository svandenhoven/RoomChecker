using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoomChecker.Models
{
    public class RoomConfig
    {
        public string id;
        public bool available;
        public string name;
        public int floor;
        public string type;
    }
    public class RoomsConfig
    {
        public string FileLocation { get; set; }
        public string URI { get; set; }
        public int CacheTime { get; set; }

        public string bGridUser { get; set; }
        public string bGridPW { get; set; }
        public string bGridEndPoint { get; set; }
    }
}
