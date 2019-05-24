using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RoomChecker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RoomChecker.Helpers
{
    public static class BuildingActionHelper
    {


        public static HttpClient GetHttpClient(IOptions<RoomsConfig> roomConfig)
        {
            var endpoint = roomConfig.Value.bGridEndPoint;
            var user = roomConfig.Value.bGridUser;
            var pw = roomConfig.Value.bGridPW;

            var bGridClient = new HttpClient()
            {
                BaseAddress = new Uri(endpoint),
                Timeout = new TimeSpan(0, 0, 2)
            };


            var byteArray = Encoding.ASCII.GetBytes($"{user}:{pw}");
            bGridClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return bGridClient;
        }

        public static async Task<T> ExecuteGetAction<T>(string action, IOptions<RoomsConfig> roomConfig)
        {
            var bGridClient = GetHttpClient(roomConfig);
            var response = await bGridClient.GetAsync(action);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<T>(jsonString);
                return jsonObject;
            }
            else
            {
                return default(T);
            }
        }
    }
}
