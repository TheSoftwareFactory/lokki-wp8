/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Initializes push notification receiver

using FSecure.Logging;
using Lokki.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Net.NetworkInformation;
using Windows.Networking.Connectivity;
using FSecure.Utils;

namespace Lokki.Notification
{
    public class NotificationService : FSDisposable
    {

        public bool IsRunning
        {
            get { return HttpNotificationChannel.Find(channelName) != null; }
        }

        #region Construction
        
        private static NotificationService _Instance = null;
        public static NotificationService Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new NotificationService();
                }

                return _Instance;
            }
        }

        private NotificationService()
        {
            NetworkInformation.NetworkStatusChanged += NoticationService_NetworkStatusChanged;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
        ~NotificationService()
        {
            Stop();
        }

        #endregion
         
        public void Release()
        {
            // Cleanup
            Stop();
            NetworkInformation.NetworkStatusChanged -= NoticationService_NetworkStatusChanged;
            SettingsManager.NotificationChannelUriString = string.Empty;
            _Instance = null;
        }

        private HttpNotificationChannel NotificationChannel = HttpNotificationChannel.Find(channelName);

        const string channelName = "LokkiNotificationChannnel";

        public void Start()
        {
            FSLog.Debug();

            // Already running?
            if (HttpNotificationChannel.Find(channelName) == null)
            {
                NotificationChannel = new HttpNotificationChannel(channelName);

                // register event handlers
                NotificationChannel.ChannelUriUpdated +=
                    new EventHandler<NotificationChannelUriEventArgs>(NotificationService_ChannelUriUpdated);
                NotificationChannel.ErrorOccurred +=
                    new EventHandler<NotificationChannelErrorEventArgs>(NotificationService_ErrorOccurred);
                NotificationChannel.ShellToastNotificationReceived +=
                    new EventHandler<NotificationEventArgs>(NotificationService_ShellToastNotificationReceived);

                NotificationChannel.Open();

                NotificationChannel.BindToShellToast();
                NotificationChannel.BindToShellTile();
            }
            else
            {
                // register event handlers
                NotificationChannel.ChannelUriUpdated +=
                    new EventHandler<NotificationChannelUriEventArgs>(NotificationService_ChannelUriUpdated);
                NotificationChannel.ErrorOccurred +=
                    new EventHandler<NotificationChannelErrorEventArgs>(NotificationService_ErrorOccurred);
                NotificationChannel.ShellToastNotificationReceived +=
                    new EventHandler<NotificationEventArgs>(NotificationService_ShellToastNotificationReceived);

                FSLog.Info("Channel URL: ", NotificationChannel.ChannelUri.ToString());
                SettingsManager.NotificationChannelUriString = NotificationChannel.ChannelUri.ToString();
            }

        }

        public void Stop()
        {
            FSLog.Debug();

            if (NotificationChannel != null)
            {
                NotificationChannel.UnbindToShellTile();
                NotificationChannel.UnbindToShellToast();
                NotificationChannel.Close();

                NotificationChannel.ChannelUriUpdated -= NotificationService_ChannelUriUpdated;
                NotificationChannel.ErrorOccurred -= NotificationService_ErrorOccurred;
                NotificationChannel.ShellToastNotificationReceived -= NotificationService_ShellToastNotificationReceived;

                Dispose();
                NotificationChannel = null;
            }
        }

        void NotificationService_ChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
        {
            FSLog.Info("Channel URL: ", e.ChannelUri.ToString()); 

            // update URI to settings
            SettingsManager.NotificationChannelUriString = e.ChannelUri.ToString();
        }

        void NotificationService_ErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
        {
            FSLog.Error("Push notification error {0} occured. {1} {2} {3}", e.ErrorType, e.Message, e.ErrorCode, e.ErrorAdditionalData);

            Stop();
            Start();
        }

        void NotificationService_ShellToastNotificationReceived(object sender, NotificationEventArgs e)
        {
            FSLog.Info("Shell message received");

            string payload = string.Empty;

            foreach (string key in e.Collection.Keys)
            {
                if (string.Compare(key, "wp:payload") == 0)
                {
                    if (e.Collection[key] != null)
                    {
                        payload = "{" + String.Format("\"{0}\":\"{1}\"", key, e.Collection[key]) + "}";
                    }
                }
            }
            // update settings
            SettingsManager.NotificationMessageString = payload;
        }

        void NoticationService_NetworkStatusChanged(object sender)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                Stop();
                SettingsManager.NotificationChannelUriString = string.Empty;
            }
            else
            {
                if (HttpNotificationChannel.Find(channelName) == null)
                {
                    Start();
                }
            }
        }

        protected override IEnumerable<IDisposable> GetDisposables()
        {
            yield return this.NotificationChannel;
        }
    }
}
