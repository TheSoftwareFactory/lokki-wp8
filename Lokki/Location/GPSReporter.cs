/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Reports location to backend

using FSecure.Logging;
using FSecure.Lokki.ServerAPI;
using FSecure.Utils;
using Lokki.Location;
using Lokki.Settings;
using Microsoft.Phone.Controls;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.Geolocation;

namespace FSecure.Lokki
{
    public class GPSReporter
    {
        private static GPSReporter _Instance;
        public static GPSReporter Instance 
        {
            get {
                if (_Instance == null)
                {
                    _Instance = new GPSReporter();
                }
                return _Instance;
            }
        }

        private GPSReporter()
        {
#if !STORE_SCREENSHOT
            LocationService.Instance.LocationChanged += LocationService_LocationChanged;
#endif
        }

        void LocationService_LocationChanged(object sender, LocationChangeEventArgs e)
        {
            FSLog.Debug();

            var coords = e.Position.Coordinate;
             
            var location = new Geolocation(
                lat: coords.Latitude,
                lon : coords.Longitude,
                acc : coords.Accuracy,
                time : coords.Timestamp.UtcDateTime);

            Deployment.Current.Dispatcher.BeginInvoke(async () =>
            {
                var resp = await ServerAPIManager.Instance.ReportLocation(location);
                if (!resp.IsSuccessful)
                {
                    FSLog.Error("Failed to update location");
                }
            });
        }

        public void StartMonitoringGPS()
        {
            LocationService.Instance.Start();
        }

        public void StopMonitoringGPS()
        {
            FSLog.Debug();

            LocationService.Instance.Stop();
        }

        public async Task ForceReportCurrentLocation()
        {
            FSLog.Debug();
            await LocationService.Instance.GetCurrentLocation(accuracy : Windows.Devices.Geolocation.PositionAccuracy.Default);
        }

        public bool IsServiceEnabled
        {
            get
            {
                var status = (new Geolocator()).LocationStatus;
                return status != PositionStatus.Disabled 
                    && status != PositionStatus.NotAvailable;
            }
        }
        
    }
}
