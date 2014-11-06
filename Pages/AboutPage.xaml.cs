/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

//#define HAVE_LOG_SENDING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using FSecure.Lokki.Resources;
using FSecure.Utils.ExtensionMethods;
using System.Reflection;
using System.Globalization;
using System.Windows.Media;
using FSecure.Logging;
using Windows.ApplicationModel;
using System.Xml.Linq;

namespace lokki_wp8.Pages
{
    public partial class AboutPage : PhoneApplicationPage
    {
        private int TapCount = 0;

        public AboutPage()
        {
            InitializeComponent();
             
            var version = XDocument.Load("WMAppManifest.xml").Root.Element("App").Attribute("Version").Value;
            VersionText.Text = Localized.Version.FormatLocalized(version);

            HeaderPanel.Title = HeaderPanel.Title.Capitalized();

#if HAVE_LOG_SENDING
            ApplicationBar = new ApplicationBar();
            ApplicationBar.BackgroundColor = (Color)Resources["C6"];
            ApplicationBar.ForegroundColor = (Color)Resources["White"];

            ApplicationBar.Mode = ApplicationBarMode.Default;
            ApplicationBar.IsMenuEnabled = false;
            ApplicationBar.IsVisible = true;

            ApplicationBarIconButton button = new ApplicationBarIconButton(new Uri("/Assets/AppbarEmail.png", UriKind.Relative));
            button.Text = Localized.SendLog;
            button.Click += button_Click;
            ApplicationBar.Buttons.Add(button);
#endif

        }

        void button_Click(object sender, EventArgs e)
        {
            FSLog.OpenLog();
        }

        private void Banner_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            TapCount++;
            FSLog.Info(TapCount);

            if(TapCount >= 5)
            {
                TapCount = 0;
                FSLogHelper.EmailSubject = "Lokki WP8 debug log";
                FSLogHelper.EmailReceiver = "lokki-feedback@f-secure.com";
                FSLogHelper.SendEmail();
            }
        }
    }
}