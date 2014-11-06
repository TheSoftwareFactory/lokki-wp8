/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Lokki.Settings;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Tasks;
using Microsoft.Phone.UserData;
using FSecure.Logging;
using FSecure.Utils;
using FSecure.Lokki.ServerAPI;
using FSecure.Utils.ExtensionMethods;
using System.Threading.Tasks;
using FSecure.Lokki.Resources;

namespace FSecure.Lokki.Controls
{
    public partial class HeaderPanel : UserControl
    {
        public event EventHandler UserInvited;

        #region Properties
        
        public bool IsVisibilityButtonVisible
        {
            get
            {
                return VisibilityButton.Visibility == Visibility.Visible;
            }
            set
            {
                VisibilityButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsAddContactButtonVisible
        {
            get
            {
                return AddContactButton.Visibility == Visibility.Visible;
            }
            set
            {
                AddContactButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
          "Title",
          typeof(string),
          typeof(HeaderPanel),
          new PropertyMetadata(null)
        );


        /// <summary>
        /// The text shown on bubble
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        #endregion

        public HeaderPanel()
        {
            this.DataContext = this;

            InitializeComponent();

            this.Loaded += HeaderPanel_Loaded;
            this.Unloaded += HeaderPanel_Unloaded;
        }

        void HeaderPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            SettingsManager.CurrentUser.PropertyChanged -= CurrentUser_PropertyChanged;
        }

        void HeaderPanel_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= HeaderPanel_Loaded;

            SettingsManager.CurrentUser.PropertyChanged += CurrentUser_PropertyChanged;
            UpdateVisibilityIcon();
        }

        public void UpdateVisibilityIcon()
        {
            var visible = SettingsManager.CurrentUser.IsVisible;

            BitmapImage icon = new BitmapImage();
            if (visible)
            {
                icon.SetSource(Application.GetResourceStream(new Uri("Assets/VisibleIcon.png", UriKind.Relative)).Stream);
            }
            else
            {
                icon.SetSource(Application.GetResourceStream(new Uri("Assets/InvisibleIcon.png", UriKind.Relative)).Stream);
            }

            VisibilityIcon.Source = icon;
        }

        void CurrentUser_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsVisible")
            {
                var visible = SettingsManager.CurrentUser.IsVisible;

                BitmapImage icon = new BitmapImage();
                if (visible)
                {
                    icon.SetSource(Application.GetResourceStream(new Uri("Assets/VisibleIcon.png", UriKind.Relative)).Stream);
                }
                else
                {
                    icon.SetSource(Application.GetResourceStream(new Uri("Assets/InvisibleIcon.png", UriKind.Relative)).Stream);
                }

                VisibilityIcon.Source = icon;
            }
        }
        
        /// <summary>
        /// Display chooser for invitation
        /// </summary>
        public void ShowInviteUserChooser()
        {
            var chooser = new EmailAddressChooserTask();
            chooser.Completed += InvitationContactChooser_Completed;
            chooser.Show();
        }

        /// <summary>
        /// Toggle user's visibility to others.
        /// </summary>
        public async void ToggleVisibility()
        {
            SystemTrayProgressIndicator.TaskCount++;
            try
            {

                var previous = SettingsManager.CurrentUser.IsVisible;
                // Toggle
                var visible = previous == false;

                // Change immediately to show something happened
                SettingsManager.CurrentUser.IsVisible = visible;
                Dispatcher.BeginInvoke(UpdateVisibilityIcon);

                // Send
                var resp = await ServerAPIManager.Instance.ChangeVisibility(visible);
                if (!resp.IsSuccessful)
                {
                    FSLog.Error("Failed to update visibility");
                    SettingsManager.CurrentUser.IsVisible = previous;
                    Dispatcher.BeginInvoke(UpdateVisibilityIcon);
                }
            }
            finally
            {
                SystemTrayProgressIndicator.TaskCount--;
            }
        }

        /// <summary>
        /// Start inviting another user with confirmation dialog.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task InviteUserAsync(string email)
        {
            SystemTrayProgressIndicator.TaskCount++;

            //Email = e.Email;
            var reply = MessageBox.Show(
                Localized.InvitingConfirmMessage.FormatLocalized(email),
                Localized.Inviting,
                MessageBoxButton.OKCancel);

            if (reply != MessageBoxResult.OK)
            {
                FSLog.Info("User declined");
                return;
            }

            var resp = await ServerAPIManager.Instance.AllowContactToSeeMe(new List<string> { email });
            if (resp.IsSuccessful)
            {
                // Display tutorial popup in map page
                if (UserInvited != null)
                {
                    UserInvited(this, new EventArgs());
                }
            }
            else
            {
                FSLog.Error("Request failed");
                MessageBox.Show(Localized.ApiError);
            }

            SystemTrayProgressIndicator.TaskCount--;
        }

        /// <summary>
        /// Called when chooser for inviting person completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void InvitationContactChooser_Completed(object sender, EmailResult e)
        {
            FSLog.Info(e.TaskResult);

            (sender as EmailAddressChooserTask).Completed -= InvitationContactChooser_Completed;

            if (e.TaskResult == TaskResult.OK)
            {
                ApplicationStates.InvitationEMailStarted = true;
                ApplicationStates.InvitationEMail = e.Email;
                ApplicationStates.InvitationDisplayName = e.DisplayName;
            }
        }

        #region Event handlers

        private void VisibilityButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ToggleVisibility();
        }

        private void AddContactButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ShowInviteUserChooser();
        }

        #endregion
    }
}
