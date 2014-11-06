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
using FSecure.Utils;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Phone.UserData;
using FSecure.Logging;
using Lokki.Settings;

using FSecure.Utils.ExtensionMethods;
using System.Globalization;
using FSecure.Lokki.Utils;

namespace FSecure.Lokki.Controls
{
    public enum PinColorType
    {
        Unknown,
        Blue,
        Green,
        Grey,
        Orange
    }

    // Pushpin control to display location on a map
    public partial class MapPushPin : UserControl
    {
        #region Properties
        
        #region Person

        public static readonly DependencyProperty PersonProperty = DependencyProperty.Register(
            "Person",
            typeof(object),
            typeof(MapPushPin),
            new PropertyMetadata(default(object), new PropertyChangedCallback(OnPersonChanged))
        );

        public Person Person
        {
            get { return (Person)GetValue(PersonProperty); }
            set { SetValue(PersonProperty, value); }
        }

        private static void OnPersonChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var self = (MapPushPin)sender;
            /*
            Binding binding;
            binding = new Binding("Progress");
            binding.Source = self.Person;
            self.SetBinding(ProgressProperty, binding);
            */

            if (e.OldValue != null)
            {
                ((Person)e.OldValue).PropertyChanged -= self.Person_PropertyChanged;
            }

            self.RefreshVisibility();

            self.Person.PropertyChanged += self.Person_PropertyChanged;
        }
        #endregion

        #region IconSource

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
          "IconSource",
          typeof(Uri),
          typeof(MapPushPin),
          new PropertyMetadata(null)
        );

        public Uri IconSource
        {
            get { return (Uri)GetValue(IconSourceProperty); }
            set { SetValue(IconSourceProperty, value); }
        }
        #endregion
        
        #region PinColor

        private PinColorType _PinColor;
        public PinColorType PinColor
        {
            get
            {
                return _PinColor;
            }
            set
            {
                if (value == PinColorType.Unknown)
                {
                    _PinColor = value;
                    this.Opacity = 0;
                    return;
                }

                // Update PIN image
                if (_PinColor != value)
                {
                    if (_PinColor == PinColorType.Unknown)
                    {
                        this.Opacity = 1;
                    }

                    _PinColor = value;
                    IconSource = new Uri("/Assets/Pin" + this.PinColor.ToString() + ".png", UriKind.Relative);
                }
            }
        }


        /** Get color for PIN.*/
        private static PinColorType GetPinColor(Person person)
        {
            /*
            // location too inaccurate?
            if (ann.userCoordinateAccuracy > 100) {
                return @"orangePin";
            }
            */

            if (person.Position.Accuracy > 100)
            {
                return PinColorType.Orange;
            }

            /*
            // location too old?
            NSTimeInterval diff = [ann.userLastReportTime timeIntervalSinceNow];
            if (diff <= 0) {
                diff = -diff;
                if (diff > 60*60) {
                    return @"orangePin";
                }
            }*/
            var diff = DateTime.UtcNow - person.Position.Time;
            if (Math.Abs(diff.TotalMinutes) > 60)
            {
                return PinColorType.Orange;
            }

            /*
            if ([ann.userID isEqualToString:[LocalStorage getLoggedInUserId]]) {
                return @"greenPin";
            } else {
                return @"bluePin";
            }
            */
            if (person.IsCurrent)
            {
                return PinColorType.Green;
            }
            else
            {
                return PinColorType.Blue;
            }
        }

        private void SetupPinColor()
        {
            PinColor = GetPinColor(Person);
        }

        #endregion
         
        public bool IsInProgress
        {
            get
            {
                return this.VisualStateGroup.CurrentState.Name == "InProgress";
            }
            set
            {
                if (IsInProgress != value)
                {
                    VisualStateManager.GoToState(this, value ? "InProgress" : "Normal", true);
                }
            }
        }
        #endregion

        public MapPushPin()
        {

            this.DataContext = this;
            InitializeComponent();

            this.Unloaded += MapPushPin_Unloaded;
            this.Loaded += MapPushPin_Loaded;

            PinColor = PinColorType.Unknown;

            this.RenderTransform = new TranslateTransform();

            VisualStateManager.GoToState(this, "Default", false);
        }

        void MapPushPin_Loaded(object sender, RoutedEventArgs e)
        {
            Person.PropertyChanged += Person_PropertyChanged;

            SetupPinColor();
        }

        /// <summary>
        /// Release event handlers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>         
        void MapPushPin_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Person != null)
            {
                Person.PropertyChanged -= Person_PropertyChanged;
            }
        }

        /// <summary>
        /// Update visibility if model changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ICanSee" 
                || e.PropertyName == "CanSeeMe" 
                || e.PropertyName == "IsShownOnMap" 
                || e.PropertyName == "IsVisible")
            {
                Dispatcher.BeginInvoke(this.RefreshVisibility);
            }
        }

        private void RefreshVisibility()
        {
            var v = this.Visibility;

            // Don't show people who don't want to be seen
            if (!this.Person.IsCurrent
                && !(this.Person.ICanSee && this.Person.IsShownOnMap && this.Person.IsVisible && this.Person.CanSeeMe))
            {
                v = Visibility.Collapsed;
            }
            else
            {
                v = Visibility.Visible;
            }

            if (v != this.Visibility)
            {
                this.Visibility = v;
                this.Avatar.RefreshPinImage();
                SetupPinColor();
            }
        }

        /**
         * View hierarchy of pin. Use LokkiUtils.DumpVisualTree to get it.
         * 
         * To bring pin on top of others, we must change the z-index of MapOverlayPresenter
          
            Microsoft.Phone.Maps.Controls.Map
              System.Windows.Controls.Border
                System.Windows.Controls.Border
                  Microsoft.Phone.Maps.Controls.MapPresentationContainer
                    MS.Internal.ExternalInputContainer
                      System.Windows.Controls.Grid
                        MS.Internal.TileHostV2
                        Microsoft.Phone.Maps.Controls.RootMapLayer
                          Microsoft.Phone.Maps.Controls.MapLayerPanel
                            Microsoft.Phone.Maps.Controls.MapOverlayPresenter
                              System.Windows.Controls.ContentPresenter
                                FSecure.Lokki.Controls.MapPushPin
                            Microsoft.Phone.Maps.Controls.MapOverlayPresenter
                              System.Windows.Controls.ContentPresenter
                                FSecure.Lokki.Controls.MapPushPin
         */
        public int ZIndex
        {
            get {
                var parent = VisualTreeHelper.GetParent(this); // ContentPresenter
                parent = VisualTreeHelper.GetParent(parent);   // MapOverlayPresenter
                return Canvas.GetZIndex(parent as UIElement);
            }
            set{
                var parent = VisualTreeHelper.GetParent(this);
                parent = VisualTreeHelper.GetParent(parent);
                Canvas.SetZIndex(parent as UIElement, value);
            }
        }

    }
}
