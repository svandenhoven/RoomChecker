/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Graph;
using MicrosoftGraphAspNetCoreConnectSample.Models;
using Newtonsoft.Json;
//using RoomChecker.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MicrosoftGraphAspNetCoreConnectSample.Helpers
{
    public static class GraphService
    {
        public static async Task<List<Room>> GetRoomsAvailabilityAsync(GraphServiceClient graphClient, HttpContext httpContext, List<Room> rooms)
        {
            var roomsAvailability = new List<Room>();
            foreach (var room in rooms)
            {
                roomsAvailability.Add(await GraphService.GetRoomAvailability(graphClient, room, httpContext, DateTime.Now));
            }
            //RoomsCache.Cache = roomsAvailability;
            return roomsAvailability;
        }

        public static async Task<Room> GetRoomAvailability(GraphServiceClient graphClient, Room room, HttpContext httpContext, DateTime dateTime)
        {
            Room roomRecent = new Room(24)
            {
                Id = room.Id,
                HasMailBox = room.HasMailBox,
                Available = true,
                Name = room.Name,
                ReservedBy = "",
                RoomType = room.RoomType,
                Floor = room.Floor,
                Type = room.Type,
                Features = "",
                Occupied = -1,
                Nodes = room.Nodes,
                Capacity = room.Capacity,
                AudioVideo = room.AudioVideo
            };

            if (roomRecent.HasMailBox)
            {
                return await GetRoomSchedule(graphClient, room, dateTime, roomRecent);
            }
            else
            {
                roomRecent.Available = false;
                roomRecent.ReservedBy = "Contact Hospitality Team";
                for (int i = 0; i < roomRecent.DaySchedule.Length - 1; i++)
                {
                    roomRecent.DaySchedule[i] = -1;
                }
                return roomRecent;
            }
        }

        private static async Task<Room> GetRoomSchedule(GraphServiceClient graphClient, Room room, DateTime dateTime, Room roomRecent)
        {
            try
            {
                //QueryOption startDateTime = new QueryOption("startDateTime", dateTime.Date.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                //QueryOption endDateTime = new QueryOption("endDateTime", dateTime.Date.AddDays(0).AddHours(23).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                //List<QueryOption> options = new List<QueryOption>
                //{
                //    startDateTime,
                //    endDateTime
                //};

                //var roomSchedules = await graphClient.Users[room.Id].CalendarView.Request(options).GetAsync();


                var beginTime = new DateTimeTimeZone
                {
                    DateTime = dateTime.Date.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var endTime = new DateTimeTimeZone
                {
                    DateTime = dateTime.Date.AddDays(0).AddHours(23).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var roomFreeBusy = await graphClient.Users[room.Id].Calendar.GetSchedule(new List<string> { room.Id }, endTime, beginTime).Request().PostAsync();

                if (roomFreeBusy.First().ScheduleItems.Count() == 0)
                {
                    roomRecent.Available = true;
                }
                else
                {
                    var orderedRoomSchedules = roomFreeBusy.First().ScheduleItems.Where(r => r.Status != FreeBusyStatus.Free).OrderBy(r => r.Start.DateTime).ToList();

                    var cetTime = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                    var scheduleArray = orderedRoomSchedules.ToArray();

                    var roomBusy = false;
                    for (var s = 0; s < scheduleArray.Length; s++)
                    {
                        var beginHour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].Start.DateTime), cetTime).Hour;
                        var endHour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].End.DateTime), cetTime).Hour;
                        for (int t = beginHour; t <= endHour; t++)
                        {
                            if (t == endHour)
                            {
                                if (TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].End.DateTime), cetTime).Minute > 0)
                                {
                                    roomRecent.DaySchedule[t] = 1;
                                }
                            }
                            else
                            {
                                roomRecent.DaySchedule[t] = 1;
                            }
                        }

                        if (DateTime.Parse(scheduleArray[s].Start.DateTime) <= dateTime.ToUniversalTime()
                            && DateTime.Parse(scheduleArray[s].End.DateTime) > dateTime.ToUniversalTime())
                        {
                            //Event is now
                            roomBusy = true;
                            roomRecent.Available = false;
                            if (roomRecent.FreeAt < TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].End.DateTime), cetTime))
                            {
                                //if the end data of current meeting is after the enddate of a previous meeting
                                roomRecent.FreeAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].End.DateTime), cetTime);
                            }
                        }
                        if (DateTime.Parse(scheduleArray[s].Start.DateTime) <= dateTime.ToUniversalTime()
                            && DateTime.Parse(scheduleArray[s].End.DateTime) < dateTime.ToUniversalTime())
                        {
                            //Event has happenend
                            if (!roomBusy)
                                roomRecent.Available = true;
                        }
                        if (DateTime.Parse(scheduleArray[s].Start.DateTime) >= dateTime.ToUniversalTime()
                            && DateTime.Parse(scheduleArray[s].End.DateTime) > dateTime.ToUniversalTime())
                        {
                            //Event needs to happen
                            if (dateTime.Date == DateTime.Now.Date)
                            {
                                //today
                                if (roomRecent.Available && roomRecent.FreeUntil == default(DateTime))
                                {
                                    roomRecent.FreeUntil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].Start.DateTime), cetTime);
                                }
                            }
                            else
                            {
                                //not today
                                roomRecent.FreeAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(scheduleArray[s].End.DateTime), cetTime);
                            }


                        }
                    }
                }
                return roomRecent;
            }
            catch (ServiceException e)
            {
                roomRecent.Available = false;
                roomRecent.ReservedBy = "Contact Hospitality Team";
                for (int i = 0; i < roomRecent.DaySchedule.Length - 1; i++)
                {
                    roomRecent.DaySchedule[i] = -1;
                }
                return roomRecent;
            }
        }

        // Load user's profile in formatted JSON.
        public static async Task<string> GetUserJson(GraphServiceClient graphClient, string email, HttpContext httpContext)
        {
            if (email == null) return JsonConvert.SerializeObject(new { Message = "Email address cannot be null." }, Formatting.Indented);

            try
            {
                // Load user profile.
                var user = await graphClient.Users[email].Request().GetAsync();
                return JsonConvert.SerializeObject(user, Formatting.Indented);
            }
            catch (ServiceException e)
            {
                switch (e.Error.Code)
                {
                    case "Request_ResourceNotFound":
                    case "ResourceNotFound":
                    case "ErrorItemNotFound":
                    case "itemNotFound":
                        return JsonConvert.SerializeObject(new { Message = $"User '{email}' was not found." }, Formatting.Indented);
                    case "ErrorInvalidUser":
                        return JsonConvert.SerializeObject(new { Message = $"The requested user '{email}' is invalid." }, Formatting.Indented);
                    case "AuthenticationFailure":
                        return JsonConvert.SerializeObject(new { e.Error.Message }, Formatting.Indented);
                    case "TokenNotFound":
                        await httpContext.ChallengeAsync();
                        return JsonConvert.SerializeObject(new { e.Error.Message }, Formatting.Indented);
                    default:
                        return JsonConvert.SerializeObject(new { Message = "An unknown error has occurred." }, Formatting.Indented);
                }
            }
        }

        // Load user's profile picture in base64 string.
        public static async Task<string> GetPictureBase64(GraphServiceClient graphClient, string email, HttpContext httpContext)
        {
            try
            {
                // Load user's profile picture.
                var pictureStream = await GetPictureStream(graphClient, email, httpContext);

                // Copy stream to MemoryStream object so that it can be converted to byte array.
                var pictureMemoryStream = new MemoryStream();
                await pictureStream.CopyToAsync(pictureMemoryStream);

                // Convert stream to byte array.
                var pictureByteArray = pictureMemoryStream.ToArray();

                // Convert byte array to base64 string.
                var pictureBase64 = Convert.ToBase64String(pictureByteArray);

                return "data:image/jpeg;base64," + pictureBase64;
            }
            catch (Exception e)
            {
                switch (e.Message)
                {
                    case "ResourceNotFound":
                        // If picture not found, return the default image.
                        return "data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjwhRE9DVFlQRSBzdmcgIFBVQkxJQyAnLS8vVzNDLy9EVEQgU1ZHIDEuMS8vRU4nICAnaHR0cDovL3d3dy53My5vcmcvR3JhcGhpY3MvU1ZHLzEuMS9EVEQvc3ZnMTEuZHRkJz4NCjxzdmcgd2lkdGg9IjQwMXB4IiBoZWlnaHQ9IjQwMXB4IiBlbmFibGUtYmFja2dyb3VuZD0ibmV3IDMxMi44MDkgMCA0MDEgNDAxIiB2ZXJzaW9uPSIxLjEiIHZpZXdCb3g9IjMxMi44MDkgMCA0MDEgNDAxIiB4bWw6c3BhY2U9InByZXNlcnZlIiB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPg0KPGcgdHJhbnNmb3JtPSJtYXRyaXgoMS4yMjMgMCAwIDEuMjIzIC00NjcuNSAtODQzLjQ0KSI+DQoJPHJlY3QgeD0iNjAxLjQ1IiB5PSI2NTMuMDciIHdpZHRoPSI0MDEiIGhlaWdodD0iNDAxIiBmaWxsPSIjRTRFNkU3Ii8+DQoJPHBhdGggZD0ibTgwMi4zOCA5MDguMDhjLTg0LjUxNSAwLTE1My41MiA0OC4xODUtMTU3LjM4IDEwOC42MmgzMTQuNzljLTMuODctNjAuNDQtNzIuOS0xMDguNjItMTU3LjQxLTEwOC42MnoiIGZpbGw9IiNBRUI0QjciLz4NCgk8cGF0aCBkPSJtODgxLjM3IDgxOC44NmMwIDQ2Ljc0Ni0zNS4xMDYgODQuNjQxLTc4LjQxIDg0LjY0MXMtNzguNDEtMzcuODk1LTc4LjQxLTg0LjY0MSAzNS4xMDYtODQuNjQxIDc4LjQxLTg0LjY0MWM0My4zMSAwIDc4LjQxIDM3LjkgNzguNDEgODQuNjR6IiBmaWxsPSIjQUVCNEI3Ii8+DQo8L2c+DQo8L3N2Zz4NCg==";
                    case "EmailIsNull":
                        return JsonConvert.SerializeObject(new { Message = "Email address cannot be null." }, Formatting.Indented);
                    default:
                        return null;
                }
            }
        }

        public static async Task<Stream> GetPictureStream(GraphServiceClient graphClient, string email, HttpContext httpContext)
        {
            if (email == null) throw new Exception("EmailIsNull");

            Stream pictureStream = null;

            try
            {
                try
                {
                    // Load user's profile picture.
                    pictureStream = await graphClient.Users[email].Photo.Content.Request().GetAsync();
                }
                catch (ServiceException e)
                {
                    if (e.Error.Code == "GetUserPhoto") // User is using MSA, we need to use beta endpoint
                    {
                        // Set Microsoft Graph endpoint to beta, to be able to get profile picture for MSAs 
                        graphClient.BaseUrl = "https://graph.microsoft.com/beta";

                        // Get profile picture from Microsoft Graph
                        pictureStream = await graphClient.Users[email].Photo.Content.Request().GetAsync();

                        // Reset Microsoft Graph endpoint to v1.0
                        graphClient.BaseUrl = "https://graph.microsoft.com/v1.0";
                    }
                }
            }
            catch (ServiceException e)
            {
                switch (e.Error.Code)
                {
                    case "Request_ResourceNotFound":
                    case "ResourceNotFound":
                    case "ErrorItemNotFound":
                    case "itemNotFound":
                    case "ErrorInvalidUser":
                        // If picture not found, return the default image.
                        throw new Exception("ResourceNotFound");
                    case "TokenNotFound":
                        await httpContext.ChallengeAsync();
                        return null;
                    default:
                        return null;
                }
            }

            return pictureStream;
        }
        public static async Task<Stream> GetMyPictureStream(GraphServiceClient graphClient, HttpContext httpContext)
        {
            Stream pictureStream = null;

            try
            {
                try
                {
                    // Load user's profile picture.
                    pictureStream = await graphClient.Me.Photo.Content.Request().GetAsync();
                }
                catch (ServiceException e)
                {
                    if (e.Error.Code == "GetUserPhoto") // User is using MSA, we need to use beta endpoint
                    {
                        // Set Microsoft Graph endpoint to beta, to be able to get profile picture for MSAs 
                        graphClient.BaseUrl = "https://graph.microsoft.com/beta";

                        // Get profile picture from Microsoft Graph
                        pictureStream = await graphClient.Me.Photo.Content.Request().GetAsync();

                        // Reset Microsoft Graph endpoint to v1.0
                        graphClient.BaseUrl = "https://graph.microsoft.com/v1.0";
                    }
                }
            }
            catch (ServiceException e)
            {
                switch (e.Error.Code)
                {
                    case "Request_ResourceNotFound":
                    case "ResourceNotFound":
                    case "ErrorItemNotFound":
                    case "itemNotFound":
                    case "ErrorInvalidUser":
                        // If picture not found, return the default image.
                        throw new Exception("ResourceNotFound");
                    case "TokenNotFound":
                        await httpContext.ChallengeAsync();
                        return null;
                    default:
                        return null;
                }
            }

            return pictureStream;
        }

        // Send an email message from the current user.
        public static async Task SendEmail(GraphServiceClient graphClient, IHostingEnvironment hostingEnvironment, string recipients, HttpContext httpContext)
        {
            if (recipients == null) return;

            var attachments = new MessageAttachmentsCollectionPage();

            try
            {
                // Load user's profile picture.
                var pictureStream = await GetMyPictureStream(graphClient, httpContext);

                // Copy stream to MemoryStream object so that it can be converted to byte array.
                var pictureMemoryStream = new MemoryStream();
                await pictureStream.CopyToAsync(pictureMemoryStream);

                // Convert stream to byte array and add as attachment.
                attachments.Add(new FileAttachment
                {
                    ODataType = "#microsoft.graph.fileAttachment",
                    ContentBytes = pictureMemoryStream.ToArray(),
                    ContentType = "image/png",
                    Name = "me.png"
                });
            }
            catch (Exception e)
            {
                switch (e.Message)
                {
                    case "ResourceNotFound":
                        break;
                    default:
                        throw;
                }
            }

            // Prepare the recipient list.
            var splitRecipientsString = recipients.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            var recipientList = splitRecipientsString.Select(recipient => new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = recipient.Trim()
                }
            }).ToList();

            // Build the email message.
            var email = new Message
            {
                Body = new ItemBody
                {
                    Content = System.IO.File.ReadAllText(hostingEnvironment.WebRootPath + "/email_template.html"),
                    ContentType = BodyType.Html,
                },
                Subject = "Sent from the Microsoft Graph Connect sample",
                ToRecipients = recipientList,
                Attachments = attachments
            };

            await graphClient.Me.SendMail(email, true).Request().PostAsync();
        }
    }
}
