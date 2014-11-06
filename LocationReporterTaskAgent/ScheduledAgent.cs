/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
//#if DEBUG
#define HAVE_LOG_FILE
//#endif

using System.Diagnostics;
using System.Windows;
using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using Windows.Devices.Geolocation;
using FSecure.Logging;
using Lokki.Location;
using Lokki.Settings;
using System;
using System.Windows.Threading;
using System.Threading.Tasks;
using FSecure.Lokki.ServerAPI;

namespace LocationReporterTaskAgent
{
    /// <summary>
    /// Sends the most accurate location received.
    /// </summary>
    public class ScheduledAgent : ScheduledTaskAgent
    {
        /// <remarks>
        /// ScheduledAgent constructor, initializes the UnhandledException handler
        /// </remarks>
        static ScheduledAgent()
        {
#if HAVE_LOG_FILE
            FSLog.LogFileEnabled = true;
#endif
            FSLog.Info();

            // Subscribe to the managed exception handler
            Deployment.Current.Dispatcher.BeginInvoke(delegate
            {
                Application.Current.UnhandledException += UnhandledException;
            });
        }

        /// Code to execute on Unhandled Exceptions
        private static void UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            FSLog.Exception(e.ExceptionObject);

            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        /// <summary>
        /// Agent that runs a scheduled task
        /// </summary>
        /// <param name="task">
        /// The invoked task
        /// </param>
        /// <remarks>
        /// This method is called when a periodic or resource intensive task is invoked
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            FSLog.Info();
            if (!SettingsManager.IsLocationUploadEnabled)
            {
                FSLog.Info("Locationing not enabled, aborting");
                Abort(); // Agent no longer active
                return;
            }
             
            LocationService.Instance.LocationChanged += LocationService_LocationChanged;
            LocationService.Instance.Start();

#if DEBUG_AGENT
            // If debugging is enabled, launch the agent again in one minute.
            ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(60));
#endif

        }

        private async void LocationService_LocationChanged(object sender, LocationChangeEventArgs e)
        {
            await SendLocation(e.Position);
        }

        private async Task SendLocation(Geoposition position)
        {

            var k = new Geolocation(position);
            var resp = await ServerAPIManager.Instance.ReportLocation(k);
            
            if (resp.IsSuccessful)
            {
                FSLog.Info("Location sent");
            }
            else
            {
                FSLog.Error("Failed to send location");
            }

            /*
            // Launch a toast to show that the agent is running.
            // The toast will not be shown if the foreground application is running.
            ShellToast toast = new ShellToast();
            toast.Title = "Location sent";
            toast.Content = "";
            toast.Show();
            */

            // Call NotifyComplete to let the system know the agent is done working.
            if (position.Coordinate.Accuracy <= LocationService.Instance.TargetAccuracy)
            {
                FSLog.Debug("Stopping");
                NotifyComplete();
            }
            else
            {
                FSLog.Debug("Waiting for more accurate position");
            }
                 
        }
    }
}