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

        //Not from bGrid
        public DateTime lastSeenDT
        {
            get
            {
                // LastSeen = Unix timestamp is seconds past epoch
                System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
                dtDateTime = dtDateTime.AddSeconds(this.lastSeen).ToLocalTime();
                return dtDateTime;
            }
        }
        public string assetType { get; set; }
    }


    public class bGridLocation
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

    public class bGridIsland
    {
        public int id { get; set; }
        public int light_intensity { get; set; }
        public string light_status { get; set; }
        public int type { get; set; }
        public List<int> locations { get; set; }
    }
}
