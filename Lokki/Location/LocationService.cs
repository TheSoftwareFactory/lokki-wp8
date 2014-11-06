/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Maintains instance of Geolocator and stores latest location to settings

using FSecure.Logging;
using Lokki.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace Lokki.Location
{
    /// <summary>
    /// The internal mode we are currently in
    /// </summary>
    public enum ReportingMode
    {
        Stopped,
        /// <summary>
        /// Using inaccurate locationing to save battery
        /// </summary>
        Idle,
        /// <summary>
        /// Using high accuracy to report the best location
        /// </summary>
        Reporting
    };

    public class LocationChangeEventArgs : EventArgs
    {
        public bool IsForced { get; set; }
        public Geoposition Position;
    }

    public class StatusEventArgs : EventArgs
    {
        public ReportingMode Mode { get; set; }
    }

    public class LocationService
    {
        /// <summary>
        /// The OS interface to location services.
        /// </summary>
        private Geolocator Locator;

        /// <summary>
        /// The best known position.
        /// </summary>
        private Geoposition BestPosition;

        /// Used to avoid sending same position twice and uploading it unnecessarily
        /// See OnLocationChanged
        private Geoposition LastPosition = null;

        /// <summary>
        /// Reports detected location
        /// </summary>
        public event EventHandler<LocationChangeEventArgs> LocationChanged;

        /// <summary>
        /// Status sent when started and stopped resolving location
        /// </summary>
        public event EventHandler<StatusEventArgs> ModeChanged;

        /// <summary>
        /// If we are in reporting phase.
        /// </summary>
        public bool IsReportingPhaseActive
        {
            get
            {
                return CurrentMode == ReportingMode.Reporting;
            }
        }

        /// <summary>
        /// This timer controls how long we stay in idle phase.
        /// </summary>
        private DispatcherTimer Timer;

        private ReportingMode _CurrentMode;
        public ReportingMode CurrentMode
        {
            get
            {
                return _CurrentMode;
            }
            set
            {
                if (value != _CurrentMode)
                {
                    _CurrentMode = value;
                    if (ModeChanged != null)
                    {
                        ModeChanged(this, new StatusEventArgs { Mode = value });
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if location observation is active
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return Locator != null;
            }
        }

        /// <summary>
        /// The minimum accuracy that we accept. 
        /// If not equal or less than TargetAccuracy, the position is sent
        /// but we keep waiting for more accurate location
        /// </summary>
        public double MinAccuracy { get; set; }

        /// <summary>
        /// The wanted accuracy.
        /// If received, accepted and sent without waiting for more accurate result.
        /// </summary>
        public double TargetAccuracy { get; set; }

        /// <summary>
        /// The time we wait for best position. Default 25 seconds.
        /// From MSDN: Periodic agents typically run for 25 seconds. There are other constraints 
        ///            that may cause an agent to be terminated early.
        /// </summary>
        public TimeSpan ReportingDuration { get; set; }

        /// <summary>
        /// The time we stay idle before querying for location.
        /// </summary>
        public TimeSpan IdleDuration { get; set; }

        /// <summary>
        /// The maximum age of location, older are ignored.
        /// </summary>
        public TimeSpan MaximumLocationAge { get; set; }

        #region construction

        private static LocationService _Instance = null;
        public static LocationService Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new LocationService();
                }

                return _Instance;
            }
        }

#if HAVE_IDLE_LOCATOR
        /// <summary>
        /// We need to maintain inaccurate Geolocator to keep application
        /// running in the background.
        /// 
        /// With single instance, app is closed when trying to change properties
        /// to less accurate. To do that, event listener must be removed first
        /// and it causes the app to exit.
        /// </summary>
        private Geolocator IdleLocator;
#endif
        private LocationService()
        {
            MinAccuracy = 60;
            TargetAccuracy = 25;
            CurrentMode = ReportingMode.Stopped;
            ReportingDuration = TimeSpan.FromSeconds(15);
            IdleDuration = TimeSpan.FromSeconds(60);
            MaximumLocationAge = TimeSpan.FromSeconds(30);
        }

        #endregion

        /// <summary>
        /// Starts reporting position via LocationChanged event.
        /// First the position is observed with high accuracy(reporting phase),
        /// the most accurate position received during short period of time is sent
        /// via LocationChanged event. After that it enters a low power mode(idle phase)
        /// keeping the GeoLocation active to allow app running in the background for some
        /// minutes and triggers the reporting phase again.
        /// </summary>
        public void Start()
        {
            FSLog.Debug();
            if (Timer != null)
            {
                FSLog.Info("Already started");
                return;
            }

#if HAVE_IDLE_LOCATOR
            IdleLocator = new Geolocator();
            IdleLocator.DesiredAccuracy = PositionAccuracy.Default;
            IdleLocator.DesiredAccuracyInMeters = 5000;
            IdleLocator.MovementThreshold = 1000;
            IdleLocator.ReportInterval = 10 * 1000 * 60;

            IdleLocator.PositionChanged += IdleLocator_PositionChanged;
#endif
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                Timer = new DispatcherTimer();

                SetReportingMode(ReportingMode.Reporting);

                Timer.Tick += Timer_Tick;
            });
        }

        void IdleLocator_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            // ignored
        }

        /// <summary>
        /// Create new Locator instance. Have to create new one when
        /// changing settings otherwise an exception is thrown.
        /// </summary>
        /// <param name="accuracyInMeters"></param>
        /// <param name="movementThreshold"></param>
        /// <param name="reportInterval"></param>
        private void CreateLocator(
            uint accuracyInMeters = 25,
            double movementThreshold = 25,
            uint reportInterval = 3000)
        {
            if (Locator != null)
            {
                FSLog.Warning("High accuracy locator exists");
                return;
            }

            Locator = new Geolocator();
            Locator.DesiredAccuracy = PositionAccuracy.High;
            Locator.DesiredAccuracyInMeters = accuracyInMeters;
            Locator.MovementThreshold = movementThreshold; // The units are meters.
            Locator.ReportInterval = reportInterval;

            Locator.PositionChanged += Locator_PositionChanged;
        }

        private void SetReportingMode(ReportingMode mode)
        {
            FSLog.Debug(CurrentMode, "=>", mode);
            if (Timer == null)
            {
                FSLog.Info("Not running");
                return;
            }

            Timer.Stop();

            if (mode == ReportingMode.Reporting)
            {
                CreateLocator(accuracyInMeters: 10,
                              movementThreshold: 10,
                              reportInterval: 2000);

                // Wait for 10 secs to get good position
                // Location updates checks if new position has better accuracy
                // and stores it if it does as best position.
                // When timer triggers, the best position is sent via LocationChanged event
                Timer.Interval = ReportingDuration;
            }
            else
            {
                // Destroy high accuracy locator
                if (Locator != null)
                {
                    Locator.PositionChanged -= Locator_PositionChanged;
                    Locator = null;
                }

                // The time we stay in idle mode
                Timer.Interval = IdleDuration;

                BestPosition = null;
            }

            CurrentMode = mode;

            Timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ToggleMode();
        }

        private void ToggleMode()
        {
            if (IsReportingPhaseActive)
            {
                if (BestPosition != null)
                {
                    FSLog.Debug("Sending best position");
                    OnNewPosition(BestPosition);
                    BestPosition = null; // to avoid sending position twice
                }
                else
                {
                    FSLog.Info("Unable to resolve good position");
                    return; // keep trying
                }
            }
            else
            {
                SetReportingMode(ReportingMode.Reporting);
            }
        }

        /// <summary>
        /// Triggers location update via LocationChanged event
        /// </summary>
        /// <param name="geoposition"></param>
        /// <param name="isForced"></param>
        private void OnNewPosition(Geoposition geoposition, bool isForced = false)
        {

            if (isForced || IsReportingPhaseActive)
            {
                OnLocationChanged(geoposition, isForced: isForced);

                // Don't stop normal operation because of forced location query
                // unless the forced query is accurate enough
                if ((!isForced || geoposition.Coordinate.Accuracy <= this.TargetAccuracy)
                    && IsReportingPhaseActive)
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        SetReportingMode(ReportingMode.Idle);
                    });
                }
            }
        }

        /// <summary>
        /// Triggered when OS has detected a position change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Locator_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            var coord = args.Position.Coordinate;

            if (IsReportingPhaseActive)
            {

                double curacc = double.MaxValue;
                if (BestPosition != null)
                {
                    curacc = BestPosition.Coordinate.Accuracy;
                }
                var newacc = args.Position.Coordinate.Accuracy;

                FSLog.Debug("time:", args.Position.Coordinate.Timestamp);

                /* Disabled for now
                var lastAcceptedTime = new DateTimeOffset(DateTime.Now - MaximumLocationAge);
                if (args.Position.Coordinate.Timestamp < lastAcceptedTime)
                {
                    FSLog.Debug("Position too old:", args.Position.Coordinate.Timestamp, 
                        "<", lastAcceptedTime);
                    return;
                }*/

                if (BestPosition == null ||
                    BestPosition.Coordinate.Accuracy > args.Position.Coordinate.Accuracy)
                {
                    FSLog.Debug("Best position with acc:",
                        args.Position.Coordinate.Accuracy);
                    BestPosition = args.Position;

                    if (newacc <= TargetAccuracy)
                    {
                        FSLog.Debug("Target accuracy reached, report and stop");

                        // Trigger Timer now
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            // If null, already sent by timer
                            if (BestPosition != null)
                            {
                                ToggleMode();
                            }
                        });
                    }
                    else
                    {
                        if (newacc <= MinAccuracy)
                        {
                            FSLog.Debug("Good enough accuracy reached, report and continue");
                            OnLocationChanged(BestPosition, isForced: false);
                        }
                    }
                }

            }
        }

        private void OnLocationChanged(Geoposition position, bool isForced = false)
        {
            if (LocationChanged == null) return;
            if (position == LastPosition)
            {
                FSLog.Debug("Skip, position is same as previously");
                return;
            }

            LastPosition = position;

            LocationChanged(this, new LocationChangeEventArgs
            {
                IsForced = isForced,
                Position = position
            });
        }

        /// <summary>
        /// Stop observing and reporting position
        /// </summary>
        public void Stop()
        {
            FSLog.Debug();

            if (Timer != null)
            {
                Timer.Tick -= Timer_Tick;
                Timer = null;
            }

            if (Locator != null)
            {
                Locator.PositionChanged -= Locator_PositionChanged;
                Locator = null;
            }
#if HAVE_IDLE_LOCATOR
            if (IdleLocator != null)
            {
                IdleLocator.PositionChanged -= IdleLocator_PositionChanged;
                IdleLocator = null;
            }
#endif
            CurrentMode = ReportingMode.Stopped;
            
        }

        /// <summary>
        /// Get the current position from OS.
        /// </summary>
        public async Task GetCurrentLocation(PositionAccuracy accuracy = PositionAccuracy.High)
        {
            FSLog.Debug();

            var locator = new Geolocator();
            locator.DesiredAccuracy = accuracy;
            if (accuracy == PositionAccuracy.High)
            {
                locator.DesiredAccuracyInMeters = 10;
                locator.MovementThreshold = 10;
                locator.ReportInterval = 5000;
            }
            else
            {
                // Get inaccurate rough position( something else than Africa )
                locator.DesiredAccuracyInMeters = 2000;
                locator.MovementThreshold = 1000;
                locator.ReportInterval = 1;
            }

            try
            {
                Geoposition geoposition = await locator.GetGeopositionAsync(
                    maximumAge: TimeSpan.FromMinutes(60),
                    timeout: TimeSpan.FromSeconds(30)
                );
                if (geoposition != null)
                {
                    OnNewPosition(geoposition, isForced: true);
                }
                else
                {
                    FSLog.Warning("Failed to get position");
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x80004004)
                {
                    FSLog.Warning("User has disabled location services or power save");

                }
                //else
                {
                    FSLog.Exception(ex);
                    // something else happened acquring the location
                }
            }
        }
    }
}
