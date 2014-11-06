/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Logging;
using FSecure.Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FSecure.Utils.ExtensionMethods;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

/** Safe Apps backend API */
namespace FSecure.Lokki.ServerAPI
{

    public enum SettingsServiceErrorCode
    {
        // TODO: Error codes
    }

    #region JSON schemas

    public class ResponseBase
    {

        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }

        [JsonIgnore]
        public HttpStatusCode HttpStatus { get; set; }

        [JsonIgnore]
        public bool IsSuccessful
        {
            get
            {
                return string.IsNullOrEmpty(Error) && HttpStatus == HttpStatusCode.OK;
            }
        }

        virtual public void PostProcess(JObject json)
        {
            return;
        }
    }

    /** {
     * id: 'userId', 
     * authorizationtoken: 'mytoken', 
     * icansee: ['userId2', 'userId3'], 
     * canseeme: ['userId4']}
    }*/
    public class SignupResponse : ResponseBase
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "authorizationtoken")]
        public string AuthorizationToken { get; set; }

        [JsonProperty(PropertyName = "icansee")]
        public List<string> ICanSee { get; set; }

        [JsonProperty(PropertyName = "canseeme")]
        public List<string> CanSeeMe { get; set; }
    }

    /**
     {
        "location": {
            "lat": 34.482391357421875,
            "lon": 108.46331024169922,
            "acc": 5,
            "time": 1389951524042
        },
        "visibility": true,
        "battery": "",
        "canseeme": [],
        "icansee": {},
        "idmapping": {
            "750c3dc8f9c3f129cc8278c84f8fe75fd6091305": "jack.doe@gtn.com"
        }
     }
    */
    public class DashboardResponse : ResponseBase
    {

        [JsonProperty(PropertyName = "location")]
        public LocationData Location { get; set; }

        [JsonProperty(PropertyName = "visibility")]
        public bool Visibility { get; set; }

        [JsonProperty(PropertyName = "battery")]
        public string Battery { get; set; }

        [JsonProperty(PropertyName = "icansee")]
        public Dictionary<string, DashboardResponse> ICanSee { get; set; }

        [JsonProperty(PropertyName = "canseeme")]
        public List<string> CanSeeMe { get; set; }

        [JsonProperty(PropertyName = "idmapping")]
        public Dictionary<string, string> IdMapping { get; set; }
    }

    public class PlaceData
    {
        [JsonProperty(PropertyName = "name")]
        public string Name {get; set;}
        
        [JsonProperty(PropertyName = "lat")]
        public double Latitude { get; set; }

        [JsonProperty(PropertyName = "lon")]
        public double Longitude { get; set; }

        [JsonProperty(PropertyName = "rad")]
        public double Radius { get; set; }

        [JsonProperty(PropertyName = "img")]
        public string Image { get; set; }
    }

    public class PlacesResponse : ResponseBase
    {

        [JsonIgnore]
        public Dictionary<string, PlaceData> Places { get; set; }

        public override void PostProcess(JObject json)
        {
            Places = new Dictionary<string, PlaceData>();

            foreach (var key in json)
            {
                Places.Add(key.Key, key.Value.ToObject(typeof(PlaceData)) as PlaceData);
            }

        }
    }

    public class DeletePlaceResponse : ResponseBase
    {

    }

    public class UpdatePlaceResponse : ResponseBase
    {

    }

    public class CreatePlaceResponse : ResponseBase
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
    }

    public class SignupRequestData
    {
        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "device_id")]
        public string DeviceId { get; set; }
    }

    public class LocationData
    {
        [JsonProperty(PropertyName = "lat")]
        public double Latitude { get; set; }

        [JsonProperty(PropertyName = "lon")]
        public double Longitude { get; set; }

        [JsonProperty(PropertyName = "acc")]
        public double Accuracy { get; set; }

        [JsonProperty(PropertyName = "time")]
        public long Time { get; set; }
    }

    public class ReportLocationData
    {
        [JsonProperty(PropertyName = "location")]
        public LocationData Location { get; set; }
    }

    public class ChangeVisibilityData
    {
        [JsonProperty(PropertyName = "visibility")]
        public bool Visibility { get; set; }
    }

    public class AllowedContactsData
    {
        [JsonProperty(PropertyName = "emails")]
        public List<string> Emails { get; set; }
    }

    public class NotificationConfigData
    {
        [JsonProperty(PropertyName = "wp8")]
        public string Uri { get; set; }
    }

    #endregion

    /// <summary>
    /// Handles communications with Lokke backend.
    /// </summary>
    public class ServerAPIRequest
    {
        #region Properties
        public Uri BackendUri { get; set; }
        public string AuthToken { get; set; }
        public string UserId { get; set; }
        #endregion

        /// <summary>
        /// Implements IAsyncResult for SettingsService requests.
        /// </summary>
        internal sealed class RequestContext : IAsyncResult, IDisposable
        {

            //internal SettingsServiceProducts Products { get; set; }

            // Name of the type of request for tracking purposes
            public string Type { get; set; }

            public string ResponseBody { get; set; }
            public HttpStatusCode HttpStatus { get; set; }
            public string ContentType { get; set; }

            public RequestContext(string type)
            {
                this.Type = type;
            }

            #region IAsyncResult

            /// <summary>
            /// Event triggered once request has completed
            /// </summary>
            private ManualResetEvent CompletionEvent = new ManualResetEvent(false);

            private bool _IsCompleted;

            public object AsyncState
            {
                get
                {
                    return _IsCompleted;
                }
            }

            public System.Threading.WaitHandle AsyncWaitHandle
            {
                get { return CompletionEvent; }
            }

            public bool CompletedSynchronously
            {
                get { return false; } // never 
            }

            /// <summary>
            /// Return true if completion event is set
            /// </summary>
            public bool IsCompleted
            {
                get
                {
                    return _IsCompleted;
                }
                set
                {
                    _IsCompleted = value;
                    if (value)
                    {
                        CompletionEvent.Set();
                    }
                }
            }

            #endregion

            #region IDisposable

            /// <summary>
            /// Required for disposing logic
            /// </summary>
            private bool IsDisposed = false;

            /// <summary>
            /// Disposes native objects.
            /// </summary>
            ~RequestContext()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    if (disposing)
                    {
                        CompletionEvent.Dispose();
                    }
                    // Free your own state (unmanaged objects).
                    // Set large fields to null.
                    IsDisposed = true;
                }
            }

            /// <summary>
            /// CA1001: Types that own disposable fields should be disposable
            /// We must dispose CompletionEvent
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            #endregion
        }

        #region Construction

        /// <summary>
        /// Handles communication with SettingsService
        /// <param name="serverAddress">Uri to backend. eg https://can-blue-dev-iaps.sp.f-secure.com</param>
        /// <param name="productName">Name of the product at SettingsService. eg. f-secure.safe-browser.wp8</param>
        /// </summary> 
        public ServerAPIRequest(Uri serverAddress, string userId, string authToken)
        {
            this.BackendUri = serverAddress;
            this.UserId = userId;
            this.AuthToken = authToken;
        }

        #endregion

        private Uri GetApiUri(string api)
        {
            return new Uri(this.BackendUri.OriginalString + "/api/locmap/v1/" + api);
        }

        private Uri GetUserUri(string api)
        {
            return new Uri(this.BackendUri.OriginalString + "/api/locmap/v1/user/" + this.UserId + "/" + api);
        }

        #region Public interfaces

        /// <summary>
        /// Signup to Lokki service
        /// @param emailAddress: The email of user
        /// @param deviceIdentifier: The deviceID of user's device.
        /// </summary>
        public Task<SignupResponse> Signup(string emailAddress, string deviceIdentifier)
        {
            var data = new SignupRequestData();
            data.Email = emailAddress;
            data.DeviceId = deviceIdentifier;

            var uri = GetApiUri("signup");
            var exec = new RequestExecutor<SignupResponse>(this);
            return exec.Execute(uri, data, method: "POST");
        }

        /**
         * Get dashboard data of user
         * @param user: the id of current user
         * */
        public Task<DashboardResponse> GetDashboard()
        {
            var uri = GetUserUri("dashboard");
            var exec = new RequestExecutor<DashboardResponse>(this);
            return exec.Execute(uri, null, method: "GET");
        }

        /// <summary>
        /// Get places for user
        /// </summary>
        /// <returns></returns>
        public Task<PlacesResponse> GetPlaces()
        {
            var uri = GetUserUri("places");
            var exec = new RequestExecutor<PlacesResponse>(this);
            return exec.Execute(uri, null, method: "GET");
        }

        /// <summary>
        /// Create new place for user
        /// </summary>
        /// <returns></returns>
        public Task<CreatePlaceResponse> CreatePlace(string name, double lat, double lon, double radius)
        {
            var uri = GetUserUri("place");
            var exec = new RequestExecutor<CreatePlaceResponse>(this);

            var data = new PlaceData();
            data.Name = name;
            data.Latitude = lat;
            data.Longitude = lon;
            data.Radius = radius;
            data.Image = "";
            return exec.Execute(uri, data, method: "POST");
        }

        /// <summary>
        /// Create new place for user
        /// </summary>
        /// <returns></returns>
        public Task<DeletePlaceResponse> DeletePlace(string id)
        {
            var uri = GetUserUri("place/" + id);
            var exec = new RequestExecutor<DeletePlaceResponse>(this);

            return exec.Execute(uri, null, method: "DELETE");
        }

        /// <summary>
        /// Create new place for user
        /// </summary>
        /// <returns></returns>
        public Task<UpdatePlaceResponse> UpdatePlace(string id, string name, double lat, double lon, double radius)
        {
            var uri = GetUserUri("place/" + id);
            var exec = new RequestExecutor<UpdatePlaceResponse>(this);

            var data = new PlaceData();
            data.Name = name;
            data.Latitude = lat;
            data.Longitude = lon;
            data.Radius = radius;
            data.Image = "";
            return exec.Execute(uri, data, method: "PUT");
        }

        /// <summary>
        /// Report location to backend
        /// </summary>
        public Task<ResponseBase> ReportLocationToServer(double lat, double lon, double acc, long time)
        {
            var data = new ReportLocationData();
            data.Location = new LocationData();
            data.Location.Latitude = lat;
            data.Location.Longitude = lon;
            data.Location.Accuracy = acc;
            data.Location.Time = time;

            var uri = GetUserUri("location");
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, data, method: "POST");
        }

        /**
         * @param user: the id of current user
         * @param visible: true or false to set visibility of user
         * */
        public Task<ResponseBase> ChangeVisibility(bool visible)
        {
            var data = new ChangeVisibilityData();
            data.Visibility = visible;

            var uri = GetUserUri("visibility");
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, data, method: "PUT");
        }

        /**
         * @param user: Request location of user
         * */
        public Task<ResponseBase> RequestLocationUpdates(string user)
        {
            var uri = GetUserUri("update/locations");
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, null, method: "POST");
        }

        /**
         * @param user: The id of current user
         * @param placeId: ID of the place to delete
         * */
        public Task<ResponseBase> DeletePlace(string user, string placeId)
        {
            var uri = GetUserUri("place/" + placeId);
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, null, method: "DELETE");
        }

        /**
         * Disallow user to see me
         * */
        public Task<ResponseBase> DisallowContactToSeeMe(string anotherUser)
        {
            var uri = GetUserUri("allow/" + anotherUser);
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, null, method: "DELETE");
        }

        /** 
         * Allow one or more users to see me.
         * @param user: the id of current user
         * @param anotherUsers: List of emails
         * */
        public Task<ResponseBase> AllowContactToSeeMe(List<string> anotherUsers)
        {
            var data = new AllowedContactsData();
            data.Emails = anotherUsers;

            var uri = GetUserUri("allow");
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, data, method: "POST");
        }

        /** 
         * Register WP8 notification token
         * @param notificationUri: The token
         * */
        public Task<ResponseBase> RegisterNotificationUrl(string notificationUri)
        {
            var data = new NotificationConfigData();
            data.Uri = notificationUri;

            var uri = GetUserUri("wp8NotificationURL");
            var exec = new RequestExecutor<ResponseBase>(this);
            return exec.Execute(uri, data, method: "POST");
        }

        #endregion

        #region Internals

        private class RequestExecutor<T> where T : ResponseBase, new()
        {
            private HttpWebRequest Request;
            private ServerAPIRequest Parent;

            public RequestExecutor(ServerAPIRequest parent)
            {
                this.Parent = parent;
            }

            public Task<T> Execute(Uri uri, object data, string method = "POST", [CallerMemberName] string requestName = "")
            {
                return Task<T>.Run(() =>
                {
                    RequestContext context = new RequestContext(requestName);

                    if (method == "POST" || method == "PUT")
                    {
                        var jsonData = "";

                        if (data != null)
                        {
                            jsonData = JsonConvert.SerializeObject(data);
                        }
                        this.StartPostRequest(context, uri, jsonData, method: method);
                    }
                    else
                    {
                        this.StartRequest(context, uri, method: method);
                    }

                    context.AsyncWaitHandle.WaitOne();

                    if (context.HttpStatus != HttpStatusCode.OK || context.ResponseBody == null)
                    {
                        FSLog.Error("Request failed");

                        return new T
                        {
                            Error = "Request failed",
                            HttpStatus = context.HttpStatus
                        };
                    }

                    T products;

                    var aslist = new List<string>(context.ContentType.Split(';'));
                    if (aslist.Contains("application/json"))
                    {
                        //products = JsonConvert.DeserializeObject<T>(context.ResponseBody);
                        JObject json = JObject.Parse(context.ResponseBody);
                        products = json.ToObject<T>();
                        products.PostProcess(json);
                    }
                    else
                    {
                        products = new T();
                    }

                    products.HttpStatus = context.HttpStatus;
                    return products;
                });
            }

            /// <summary>
            /// Starts a new GET request to given Uri
            /// </summary>
            private void StartRequest(RequestContext context, Uri uri, string method = "GET")
            {

                if (uri.Scheme != "https")
                {
                    throw new ArgumentException("https url is required");
                }

                FSLog.Info(context.Type, uri);

                Request = (HttpWebRequest)FSRequest.Create(uri);
                Request.Method = method;

                if (Request.Headers == null)
                {
                    Request.Headers = new System.Net.WebHeaderCollection();
                }

                if (!string.IsNullOrWhiteSpace(Parent.AuthToken))
                {
                    Request.Headers["authorizationtoken"] = Parent.AuthToken;
                }

                Request.Headers["platform"] = "WP";
                Request.Headers["version"] = "3.0";
                // No caching
                Request.Headers[HttpRequestHeader.IfModifiedSince] = DateTime.UtcNow.ToString();

                Request.BeginGetResponse(new AsyncCallback((IAsyncResult asynchronousResult) =>
                {
                    ReadCallBack(context, asynchronousResult);
                }), Request);
            }

            /// <summary>
            /// Starts a new POST request to given Uri
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
            private void StartPostRequest(RequestContext context, Uri uri, string data, string method = "POST")
            {
                if (uri.Scheme != "https")
                {
                    throw new ArgumentException("https url is required");
                }

                FSLog.Info(context.Type, uri);
                //FSLog.Debug(data);

                var bytes = System.Text.Encoding.UTF8.GetBytes(data);

                Request = (HttpWebRequest)FSRequest.Create(uri);
                Request.Method = method;

                Request.Headers = new System.Net.WebHeaderCollection(); ;
                Request.Headers["charset"] = "UTF-8";
                // No caching
                Request.Headers[HttpRequestHeader.IfModifiedSince] = DateTime.UtcNow.ToString();

                Request.ContentType = "application/json";
                Request.ContentLength = bytes.Length;
                Request.Accept = "application/json";

                if (!string.IsNullOrWhiteSpace(Parent.AuthToken))
                {
                    Request.Headers["authorizationtoken"] = Parent.AuthToken;
                }
                Request.Headers["platform"] = "WP";
                Request.Headers["version"] = "3.0";

                Request.BeginGetRequestStream(new AsyncCallback((IAsyncResult writeResult) =>
                {
                    try
                    {
                        using (Stream writeStream = Request.EndGetRequestStream(writeResult))
                        {
                            writeStream.Write(bytes, 0, bytes.Length);
                        }

                        Request.BeginGetResponse(new AsyncCallback((IAsyncResult asynchronousResult) =>
                        {
                            ReadCallBack(context, asynchronousResult);
                        }), Request);

                    }
                    catch (Exception e)
                    {
                        FSLog.Exception(e);

                        context.ResponseBody = null;
                        context.IsCompleted = true;
                    }

                }), Request);
            }

            /// <summary>
            /// Handles request created with StartPostRequest or StartGetRequest. 
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
            private void ReadCallBack(RequestContext context, IAsyncResult asynchronousResult)
            {
                FSLog.Debug(context.Type);

                try
                {
                    HttpWebResponse response = Request.EndGetResponse(asynchronousResult) as HttpWebResponse;
                    FSLog.Info(response.StatusCode, response.StatusDescription);
                    context.HttpStatus = response.StatusCode;
                    context.ContentType = response.ContentType;
                    using (StreamReader responseStream = new StreamReader(response.GetResponseStream()))
                    {
                        context.ResponseBody = responseStream.ReadToEnd();
                    }
                }
                catch (WebException e)
                {
                    FSLog.Exception(e);
                    HttpStatusCode responseCode;

                    // Get the error JSON from SettingsService
                    if (e.Response != null)
                    {
                        var response = ((HttpWebResponse)e.Response);
                        responseCode = response.StatusCode;
                        context.HttpStatus = response.StatusCode;

                        FSLog.Error("StatusCode", (int)responseCode, responseCode, response.StatusDescription);
                        FSLog.Error(((HttpWebResponse)e.Response).StatusDescription);

                        using (StreamReader responseStream = new StreamReader(response.GetResponseStream()))
                        {
                            string err = responseStream.ReadToEnd();
                            FSLog.Error(err);

                            context.ResponseBody = err;
                        }
                    }
                    else
                    {
                        FSLog.Error("No response, canceled");
                        return;
                    }
                }
                catch (Exception e)
                {
                    FSLog.Error("Unexpected error");
                    FSLog.Exception(e);
                }
                finally
                {
                    context.IsCompleted = true;
                }
            }
        }
    }
        #endregion
}
