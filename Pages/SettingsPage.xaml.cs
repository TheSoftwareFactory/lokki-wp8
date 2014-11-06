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
using FSecure.Logging;
using Microsoft.Phone.UserData;
using Lokki.Settings;
using FSecure.Utils.ExtensionMethods;

using System.Windows.Media.Imaging;
using System.Windows.Media;
using FSecure.Lokki.Controls;

namespace FSecure.Lokki.Pages
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public static readonly DependencyProperty PersonProperty = DependencyProperty.Register(
            "Person",
            typeof(Person),
            typeof(SettingsPage),
            new PropertyMetadata(null)
        );

        public Person Person
        {
            get { return (Person)GetValue(PersonProperty); }
            set { SetValue(PersonProperty, value); }
        }

        
        public SettingsPage()
        {
            DataContext = this; 
            
            InitializeComponent();

            Person = SettingsManager.CurrentUser;
            Avatar.Person = Person;// TODO: Why binding doesn't work?

            LokkiIdText.Text = LokkiIdText.Text.FormatLocalized(SettingsManager.CurrentUser.Email);

            UserNameText.Text = Person.Name;
            HeaderPanel.Title = HeaderPanel.Title.Capitalized();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            UpdateVisibility();
            RefreshMapModeButtons();
 
            SettingsManager.CurrentUser.PropertyChanged += CurrentUser_PropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            SettingsManager.CurrentUser.PropertyChanged -= CurrentUser_PropertyChanged;
        }

        void CurrentUser_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(UpdateVisibility);
            UserNameText.Text = Person.Name;
        }

        private void UpdateVisibility()
        {
            var visible = SettingsManager.CurrentUser.IsVisible;

            var main = (SolidColorBrush)Resources["BRUSH_Main"];
            var white = (SolidColorBrush)Resources["BRUSH_White"];
            var transp = (SolidColorBrush)Resources["BRUSH_TransparentTouchable"];

            if (visible)
            {
                VisibleButton.IsSelected = true;
                InvisibleButton.IsSelected = false;
            }
            else
            {
                VisibleButton.IsSelected = false;
                InvisibleButton.IsSelected = true;
            }
        }
         
        private void RefreshMapModeButtons()
        {
            var buttons = new Dictionary<MapMode, SettingButton> {
                { MapMode.Terrain, TerrainButton },
                { MapMode.Hybrid, HybridButton },
                { MapMode.Road, RoadButton },
                { MapMode.Default, RoadButton },
                { MapMode.Aerial, AerialButton }
            };

            var mode = SettingsManager.CartographicMode;
            foreach (var btn in buttons.Values)
            {
                btn.IsSelected = false;
            }

            buttons[mode].IsSelected = true;
        }

        private void TerrainButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SettingsManager.CartographicMode = MapMode.Terrain;
            RefreshMapModeButtons();
        }

        private void RoadButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SettingsManager.CartographicMode = MapMode.Road;
            RefreshMapModeButtons();
        }

        private void AerialButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SettingsManager.CartographicMode = MapMode.Aerial;
            RefreshMapModeButtons();
        }

        private void HybridButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SettingsManager.CartographicMode = MapMode.Hybrid;
            RefreshMapModeButtons();
        }

        private void VisibleButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (!SettingsManager.CurrentUser.IsVisible)
                HeaderPanel.ToggleVisibility();
        }

        private void InvisibleButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (SettingsManager.CurrentUser.IsVisible)
                HeaderPanel.ToggleVisibility();
        }
    }
}