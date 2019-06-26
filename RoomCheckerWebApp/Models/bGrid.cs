using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoomChecker.Models
{
    using System;
    using System.Collections;

    //bGrid
    public class bGridTemperature
    {
        public int location_id { get; set; }
        public int timestamp { get; set; }
        public float value { get; set; }
    }

    public class bGridMovement
    {
        public int location_id { get; set; }
        public int timestamp { get; set; }
        public float value { get; set; }
    }


    public class bGridOccpancy
    {
        public int id { get; set; }
        public int location_id { get; set; }
        public int timestamp { get; set; }
        public int value { get; set; }
    }


    public class bGridAsset
    {
        public int id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public int lastSeen { get; set; }
        public int floor { get; set; }
        public string building { get; set; }
    }


    public class bGridLocations
    {
        public int id { get; set; }
        public string name { get; set; }
        public int type { get; set; }
        public int floor { get; set; }
        public string building { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public int? island_id { get; set; }
    }
}
