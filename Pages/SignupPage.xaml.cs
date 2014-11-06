/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using ringo_wp8;
using FSecure.Utils;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Media.Animation;
using FSecure.Lokki.ServerAPI;
using FSecure.Logging;
using System.Threading.Tasks;
using System.Windows.Input;
using Lokki.Settings;
using FSecure.Lokki.Resources;

using FSecure.Utils.ExtensionMethods;

namespace FSecure.Lokki.Pages
{
    public partial class SignupPage : PhoneApplicationPage
    {
        public SignupPage()
        {
            InitializeComponent();

            if (!DesignerProperties.IsInDesignTool)
            {
                this.Loaded += SignupPage_Loaded;
            }

            AppInfoContainer.RenderTransform = new CompositeTransform();
            AppInfoContainer.RenderTransformOrigin = new Point(0.5, 0);

            AppIconImage.RenderTransform = new CompositeTransform();
            AppIconImage.RenderTransformOrigin = new Point(0.5, 0.5);

            ByFSecureImage.RenderTransform = new CompositeTransform();
            ByFSecureImage.RenderTransformOrigin = new Point(0.5, 0.5);

            AppNameText.RenderTransform = new CompositeTransform();
            AppNameText.RenderTransformOrigin = new Point(0.5, 0.5);

            AppIconImage.Opacity = 0;
            AppNameText.Opacity = 0;
            ByFSecureImage.Opacity = 0;

            InfoText.Opacity = 0;
            SignupButton.Opacity = 0;
            EmailTextBox.Opacity = 0;

            SignupButton.Tap += SignupButton_Tap;
            SignupButton.IsEnabled = false;
            EmailTextBox.TextChanged += EmailTextBox_TextChanged;

            var current = SettingsManager.CurrentUser;
            if (current != null)
            {
                EmailTextBox.Text = current.Email;
                SignupButton.IsEnabled = IsEmail(current.Email);
            }

            // Uncomment for testing
            //SettingsManager.IsPrivacyPolicyAccepted = false;
        }

        bool IsEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2)
            {
                return false;
            }

            if (parts[0].Length < 1)
            {
                return false;
            }

            if (parts[1].Length < 3)
            {
                return false;
            }

            parts = parts[1].Split('.');
            if (parts.Length < 2)
            {
                return false;
            }

            if (parts[0].Length < 1)
            {
                return false;
            }

            if (parts[1].Length < 1)
            {
                return false;
            }

            // Close enough
            return true;
        }

        void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = EmailTextBox.Text;
            SignupButton.IsEnabled = IsEmail(text);
        }

        private void OpenMapPage()
        {
            FSLog.Info();
            NavigationService.Navigate(new Uri("/Pages/MapPage.xaml", UriKind.Relative));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            EmailTextBox.KeyUp -= EmailTextBox_KeyUp;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            (App.Current as App).HideHeaderPanel();

            var colors = ((ResourceDictionary)Application.Current.Resources["Colors"]);
            SystemTray.BackgroundColor = (Color)colors["Main"];
            SystemTray.Opacity = 0;

            EmailTextBox.KeyUp += EmailTextBox_KeyUp;

            // Clear backstack. Needed if navigated from mainpage on 401 auth error
            while (NavigationService.CanGoBack)
            {
                NavigationService.RemoveBackEntry();
            }

            if (e.NavigationMode == NavigationMode.New)
            {
                // Start fetching position so we have good one when displaying map page
                GPSReporter.Instance.StartMonitoringGPS();
                Dispatcher.BeginInvoke(async () => await GPSReporter.Instance.ForceReportCurrentLocation());
            }
        }

        async private Task<SignupResponse> Signup()
        {
            var email = EmailTextBox.Text;
            if (!IsEmail(email)) return null;

            SignupButton.IsEnabled = false;
            EmailTextBox.IsEnabled = false;

            SystemTrayProgressIndicator.TaskCount++;
            var reply = await ServerAPIManager.Instance.Signup(EmailTextBox.Text);
            SystemTrayProgressIndicator.TaskCount--;

            if (reply.IsSuccessful)
            {
                FSLog.Info("Login succesful, show map page");
                
                // To enable location updater task
                SettingsManager.IsLocationUploadEnabled = true;

                OpenMapPage();
            }
            else
            {
                FSLog.Error("Failed to login");
                
                SignupButton.IsEnabled = true;
                EmailTextBox.IsEnabled = true;

                // Handle 401
                if (reply.HttpStatus == HttpStatusCode.Unauthorized)
                {
                    MessageBox.Show(Localized.SignupError401.FormatLocalized(email));
                }
                else
                {
                    MessageBox.Show(Localized.SignupError);
                }
            }

            return reply;
        }

        async void EmailTextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await Signup();
            }
        }

        async void SignupButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            await Signup();
        }

        void SignupPage_Loaded(object sender, RoutedEventArgs e)
        {
            var easing = new CubicEase();
            easing.EasingMode = EasingMode.EaseOut;

            var story = FSAnim.Fade(AppIconImage,
                from: 0,
                to: 1,
                easing: easing,
                duration: 250);

            FSAnim.Fade(AppNameText,
                from: 0,
                to: 1,
                story: story,
                easing: easing,
                startTime: 250,
                duration: 250);

            FSAnim.Fade(ByFSecureImage,
                from: 0,
                to: 1,
                story: story,
                easing: easing,
                startTime: 500,
                duration: 250);

            story.Completed += (object s2, EventArgs e2) =>
            {

                if (ServerAPIManager.Instance.IsLoggedIn)
                {
                    // Register if failed to do so at signup
                    Dispatcher.BeginInvoke(async () => 
                        await ServerAPIManager.Instance.RegisterNotificationUrl());

                    OpenMapPage();
                    return;
                }

                // Scale app icon smaller for content
                FSAnim.Scale(AppInfoContainer.RenderTransform,
                    //story: story,
                    easing: easing,
                    toX: 0.66,
                    toY: 0.66,
                    startTime: 0,
                    start : true,
                    onCompletion: () =>
                    {
                        // Show privacy policy agreement if not accepted yet
                        if (!SettingsManager.IsPrivacyPolicyAccepted)
                        {
                            PrivacyPolicyContainer.Opacity = 0;

                            story = FSAnim.Fade(PrivacyPolicyContainer, from: 0, to: 1, easing: easing);

                            FSAnim.Fade(ByFSecureImage,
                                to: 0,
                                story: story,
                                easing: easing,
                                startTime: 0,
                                duration: 250,
                                onCompletion: () =>
                                {
                                    ByFSecureImage.Visibility = Visibility.Collapsed;
                                });

                            PrivacyPolicyContainer.Visibility = Visibility.Visible;

                            story.Begin();

                            return;
                        }

                        // Show signup controls if not signed up yet
                        ShowSignupControls();

                    });
            };

            FSCall.Delayed(() => story.Begin(), 250);
        }

        private void ShowSignupControls()
        {
            var easing = new CubicEase();
            easing.EasingMode = EasingMode.EaseOut;
             
            var story = FSAnim.Fade(InfoText,
                to: 1,
                //story: story,
                easing: easing,
                startTime: 250,
                duration: 250);

            if (ByFSecureImage.Visibility == Visibility.Visible)
            {
                FSAnim.Fade(ByFSecureImage,
                    to: 0,
                    story: story,
                    easing: easing,
                    startTime: 0,
                    duration: 250,
                    onCompletion: () =>
                    {
                        ByFSecureImage.Visibility = Visibility.Collapsed;
                    });
            }

            FSAnim.Fade(EmailTextBox,
                to: 1,
                story: story,
                easing: easing,
                startTime: 350,
                duration: 250);

            FSAnim.Fade(SignupButton,
                to: 1,
                story: story,
                easing: easing,
                startTime: 450,
                duration: 250);

            LoginContainer.Visibility = Visibility.Visible;

            story.Begin();
        }

        private void AgreeButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var easing = new CubicEase();
            easing.EasingMode = EasingMode.EaseOut;

            SettingsManager.IsPrivacyPolicyAccepted = true;

            FSAnim.Fade(PrivacyPolicyContainer, to: 0, easing: easing, start: true,
                onCompletion: () =>
                {
                    PrivacyPolicyContainer.Visibility = Visibility.Collapsed;
                    ShowSignupControls();
                });
        }
    }
}