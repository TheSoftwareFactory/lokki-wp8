/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Logging;
using FSecure.Utils;
using FSecure.Utils.ExtensionMethods;

using Lokki.Settings;
using Microsoft.Phone.Controls;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace FSecure.Lokki.ServerAPI
{
    public class ServerAPIManager
    {

        public bool IsLoggedIn
        {
            get
            {
                return !(string.IsNullOrWhiteSpace(SettingsManager.CurrentUser.BackendId)
                    || string.IsNullOrWhiteSpace(SettingsManager.AuthToken));
            }
        }

        /// <summary>
        /// SettingsService requests
        /// </summary>
        private ServerAPIRequest Request;

        private static ServerAPIManager _Instance;
        public static ServerAPIManager Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new ServerAPIManager();
                }
                return _Instance;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private ServerAPIManager()
        {

#if DEBUG
            //Uri serviceUrl = new Uri("https://ringo-server.f-secure.com");
            Uri serviceUrl = new Uri("https://ringo-test-environment.herokuapp.com");
#else
            Uri serviceUrl = new Uri("https://ringo-server.f-secure.com");
#endif
            Request = new ServerAPIRequest(serviceUrl, 
                SettingsManager.CurrentUser.BackendId, 
                SettingsManager.AuthToken);
        }

        #region Server interfaces

        public void ClearAuthentication()
        {
            SettingsManager.AuthToken = "";
            Request.AuthToken = "";
        }

        /// <summary>
        /// Signup to Lokki service with email
        /// </summary>
        public async Task<SignupResponse> Signup(string email)
        {
            var current = SettingsManager.CurrentUser;
            current.Email = email;

            SignupResponse resp = await Request.Signup(email, DeviceId);

            // Store user id and auth if successful
            if (resp.IsSuccessful)
            {
                FSLog.Info("Signup successful");

                SettingsManager.AuthToken = resp.AuthorizationToken;
                this.Request.AuthToken = SettingsManager.AuthToken;
                this.Request.UserId = resp.Id;

                current.BackendId = resp.Id;
                
                SettingsManager.SavePeople();

                await RegisterNotificationUrl();
            }
            else
            {
                FSLog.Error("Signup failed:", resp.HttpStatus, resp.Error);
            }

            return resp;
        }

        public async Task<ResponseBase> RegisterNotificationUrl()
        {
            if (SettingsManager.IsNotificationUrlRegistered)
            {
                // Already registered
                return null;
            }
            
            FSLog.Info("Not registered yet");

            // Register notification token, this is used to track the type of devices being used
            // at the moment. "W" is used as a workaround for missing token.
            var tokenResp = await this.Request.RegisterNotificationUrl("W");
            if (!tokenResp.IsSuccessful)
            {
                // NOTE: Not failing if this fails.
                FSLog.Error("Failed to register token");
            }
            else
            {
                FSLog.Info("Notification token sent");
                SettingsManager.IsNotificationUrlRegistered = true;
            }

            return tokenResp;
        }

        private void RefreshPerson(Person person, DashboardResponse dashboard)
        {
            person.IsVisible = dashboard.Visibility;
            person.Position = new Geolocation(
                lat: dashboard.Location.Latitude,
                lon: dashboard.Location.Longitude,
                acc: dashboard.Location.Accuracy,
                time: dashboard.Location.Time.ToDateTimeOffsetFromMillisecondsSince1970()
            );
        }

        private void UpdatePlace(Place place, PlaceData placeData)
        {
            place.Name = placeData.Name;
            place.Radius = placeData.Radius;
            place.Latitude = placeData.Latitude;
            place.Longitude = placeData.Longitude;
            place.Image = placeData.Image;
        }

        /// <summary>
        /// Update list of places
        /// </summary>
        /// <returns></returns>
        public async Task<PlacesResponse> GetPlaces()
        {
            var resp = await Request.GetPlaces();
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed", resp.HttpStatus);
                return resp;
            }
             
            var places = SettingsManager.Places;
            var removedPlace = new List<Place>();

            foreach (var place in places)
            {
                if (resp.Places.ContainsKey(place.Id))
                {
                    PlaceData data = resp.Places[place.Id];
                    UpdatePlace(place, data);

                    // Remove so not added as new
                    resp.Places.Remove(place.Id);
                }
                else
                {
                    removedPlace.Add(place);
                }
            }

            // Add new
            foreach (string id in resp.Places.Keys)
            {
                PlaceData data = resp.Places[id];
                
                var place = new Place();
                place.Id = id; 
                UpdatePlace(place, data);
                
                places.Add(place);
            }

            foreach (Place place in removedPlace)
            {
                places.Remove(place);
            }

            // TODO: Check for accidental duplicates. Would use dictionary but there's no builtin ObservableDictionary

            SettingsManager.Save();

            return resp;
        }

        public async Task<CreatePlaceResponse> CreatePlace(Place place)
        {
            var resp = await Request.CreatePlace(place.Name, place.Latitude, place.Longitude, 100);
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed", resp.HttpStatus);
                return resp;
            }

            //var place = new Place(resp.Id, name, lat, lon, 100);
            var places = SettingsManager.Places;

            place.Id = resp.Id;
            places.Add(place);
            SettingsManager.Save();

            return resp;
        }

        public async Task<UpdatePlaceResponse> UpdatePlace(Place place)
        {
            var resp = await Request.UpdatePlace(place.Id, place.Name, place.Latitude, place.Longitude, place.Radius);
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed", resp.HttpStatus);
                return resp;
            }

            SettingsManager.Save();

            return resp;
        }

        public async Task<DeletePlaceResponse> DeletePlace(Place place)
        {
            var resp = await Request.DeletePlace(place.Id);
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed", resp.HttpStatus);
                return resp;
            }

            SettingsManager.Places.Remove(place);
            SettingsManager.Save();

            return resp;
        }

        /** Retrieve dashboard data from backend and update app state */
        public async Task<DashboardResponse> GetDashboard()
        {
            var resp = await Request.GetDashboard();
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed", resp.HttpStatus);
                return resp;
            }

            var people = SettingsManager.People;

            // TODO: Refactor the 2 loops to use common function

            var newPeople = new List<string>(resp.IdMapping.Keys);
            foreach (var person in people)
            {
                if (person.IsCurrent)
                {
                    // Refresh to keep in sync
                    person.IsVisible = resp.Visibility;
                    newPeople.Remove(person.BackendId);
                    continue;
                };

                if (resp.ICanSee.ContainsKey(person.BackendId))
                {
                    person.ICanSee = true;

                    var dashboard = resp.ICanSee[person.BackendId];
                    RefreshPerson(person, dashboard);
                }
                else
                {
                    person.ICanSee = false;
                }

                person.CanSeeMe = resp.CanSeeMe.Contains(person.BackendId);

                newPeople.Remove(person.BackendId);
            }

            // Add new people
            foreach (var key in newPeople)
            {
                var person = new Person();
                person.BackendId = key;

                var email = resp.IdMapping[key];
                person.Email = email;

                person.CanSeeMe = resp.CanSeeMe.Contains(key);

                if (resp.ICanSee.ContainsKey(key))
                {
                    person.ICanSee = true;
                    var dashboard = resp.ICanSee[key];
                    RefreshPerson(person, dashboard);
                }
                else
                {
                    person.ICanSee = false;
                }
                 
                // This triggers CollectionChanged event to UI, see MapPage::People_CollectionChanged
                people.Add(person);
            }

            SettingsManager.SavePeople();

            return resp;
        }

        public async Task<ResponseBase> AllowContactToSeeMe(List<string> emails)
        {
            return await Request.AllowContactToSeeMe(emails);
        }

        public async Task<ResponseBase> AllowContactToSeeMe(string email)
        {
            return await AllowContactToSeeMe(new List<string> { email });
        }

        public async Task<ResponseBase> ChangeVisibility(bool visible)
        {
            return await Request.ChangeVisibility(visible);
        }

        public async Task<ResponseBase> DisallowContactToSeeMe(Person person)
        {
            var resp = await Request.DisallowContactToSeeMe(person.BackendId);
            if (!resp.IsSuccessful)
            {
                FSLog.Error("Request failed");
            }
            return resp;
        }
 
        public async Task<ResponseBase> ReportLocation(Geolocation location)
        {

            // Update setting here to update with bg agent and app.
            var p = SettingsManager.CurrentUser;
            p.Position = location;
            SettingsManager.CurrentUser = p;

            if (string.IsNullOrEmpty(SettingsManager.AuthToken))
            {
                FSLog.Error("Not authenticated");
                return new ResponseBase();
            }

            return await Request.ReportLocationToServer(
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                location.Time.ToMillisecondsSince1970());
        }

        #endregion

        /** Generate base64 encoded string of the device id. */
        private string DeviceId
        {
            get
            {
                string uid = SettingsManager.DeviceId;

                if (uid != null && uid.Length != 0)
                {
                    FSLog.Debug("Existing uid", uid, "length:", uid.Length);
                    return uid;
                }

                object data = null;

                // http://msdn.microsoft.com/en-US/library/windowsphone/develop/microsoft.phone.info.deviceextendedproperties(v=vs.105).aspx
                if (Microsoft.Phone.Info.DeviceExtendedProperties.TryGetValue("DeviceUniqueId", out data))
                {
                    var bytes = (byte[])data;
                    uid = Convert.ToBase64String(bytes);

                    FSLog.Debug("uid", uid, "length:", uid.Length);

                    SettingsManager.DeviceId = uid;
                }
                else
                {
                    FSLog.Error("Failed to get DeviceUniqueId");
                }

                return uid;
            }
        }

    }
}
