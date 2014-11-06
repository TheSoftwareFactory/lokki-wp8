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
using FSecure.Lokki.ServerAPI;
using FSecure.Logging;
using FSecure.Utils;
using System.Threading.Tasks;
using FSecure.Lokki.Resources;
using ringo_wp8;
using System.Windows.Input;

namespace FSecure.Lokki.Controls
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public partial class PlaceEditBubble : UserControl
    {
        FSAsyncResult AsyncResult;

        public static readonly DependencyProperty PlaceProperty = DependencyProperty.Register(
          "Place",
          typeof(Place),
          typeof(PlaceEditBubble),
          new PropertyMetadata(null)
        );

        /// <summary>
        /// The place this editor is changing
        /// </summary>
        public Place Place
        {
            get { return (Place)GetValue(PlaceProperty); }
            set { SetValue(PlaceProperty, value); }
        }

        public PlaceEditBubble()
        {
            InitializeComponent();

            this.NameTextBox.KeyUp += NameTextBox_KeyUp;
            //UpdateDoneButton();
        }

        void UpdateDoneButton()
        {
            bool isEnabled = NameTextBox.Text.Length > 0;
            DoneButton.Opacity = isEnabled ? 1 : 0.5;
            DoneButton.IsHitTestVisible = isEnabled;
        }

        void NameTextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleDone();
            }
        }

        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDoneButton();
        }

        private void CancelButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FSLog.Info();
            AsyncResult.IsCompleted = true;
        }

        private void DoneButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            HandleDone();
        }

        private void HandleDone() 
        {
            FSLog.Info();

            if (NameTextBox.Text.Length > 0 && !NameTextBox.Text.Equals(Place.Name))
            {
                Dispatcher.BeginInvoke(async () =>
                {
                    SystemTrayProgressIndicator.TaskCount++;
                    try
                    {

                        Place.Name = NameTextBox.Text;
                        if (string.IsNullOrWhiteSpace(Place.Id))
                        {
                            var resp = await ServerAPIManager.Instance.CreatePlace(Place);
                            if (!resp.IsSuccessful)
                            {
                                if (resp.HttpStatus == HttpStatusCode.Forbidden)
                                {
                                    MessageBox.Show(Localized.PlacesLimitReached);
                                }
                                else
                                {
                                    MessageBox.Show(Localized.ApiError);
                                }
                            }
                        }
                        else
                        {
                            var resp = await ServerAPIManager.Instance.UpdatePlace(Place);
                            if (!resp.IsSuccessful)
                            {
                                MessageBox.Show(Localized.ApiError);
                            }
                        }

                    }
                    finally
                    {
                        SystemTrayProgressIndicator.TaskCount--;
                    }
                });
            }

            AsyncResult.IsCompleted = true;
        }

        private void EnableMenu(bool enable)
        {
            var currentPage = ((App)Application.Current).RootFrame.Content as PhoneApplicationPage;
            foreach (var button in currentPage.ApplicationBar.Buttons)
            {
                (button as ApplicationBarIconButton).IsEnabled = enable;
            }
            currentPage.ApplicationBar.IsMenuEnabled = enable;
        }

        public Task Show()
        {
            var currentPage = ((App)Application.Current).RootFrame.Content as PhoneApplicationPage;
            var layoutroot = currentPage.Content as Grid;
            layoutroot.Children.Add(this);

            AsyncResult = new FSAsyncResult();
            if (string.IsNullOrWhiteSpace(Place.Id))
            {
                ActionText.Text = Localized.PlaceEditorAdd;
            }
            else
            {
                ActionText.Text = Localized.PlaceEditorEdit;
            }

            EnableMenu(false);

            return Task.Run(() =>
            {
                AsyncResult.AsyncWaitHandle.WaitOne();
                AsyncResult.Dispose();

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    layoutroot.Children.Remove(this);
                    EnableMenu(true);
                });
            }
            );
        }
    }
}
