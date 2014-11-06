/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

/// TODO: Use ListBox or LongListSelector instead of stackpanel with Horizontal orientation in PlaceListItemControl to reduce code for handling model changes.

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
using System.Collections.ObjectModel;
using System.Device.Location;
using FSecure.Utils.ExtensionMethods;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Collections.Specialized;
using FSecure.Logging;
using System.ComponentModel;

namespace FSecure.Lokki.Controls
{
    public class PlaceSelectedEventArgs : EventArgs
    {
        public Place Place { get; set; }
    }

    /// <summary>
    /// Displays list of places or tutorial text if there are no places.
    /// 
    /// MyPeopleListItem observes the People list of the Place it is representing
    /// and displays the avatars in vertical list.
    /// 
    /// </summary>
    public partial class PlacesList : UserControl
    {
        public event EventHandler<PlaceSelectedEventArgs> PlaceSelected;

        private int ImageIndex = 0;

        public PlacesList()
        {
            InitializeComponent();

            if (!DesignerProperties.IsInDesignTool)
            {
                InitPlacesStack();

                this.Loaded += PlacesList_Loaded;
                this.Unloaded += PlacesList_Unloaded;
            }
        }

        void PlacesList_Unloaded(object sender, RoutedEventArgs e)
        {
            SettingsManager.Places.CollectionChanged -= Places_CollectionChanged; 
        }

        void PlacesList_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsManager.Places.CollectionChanged += Places_CollectionChanged; 
        }
        
        /// <summary>
        /// Observe changes to places. Add and remove place controls from stack.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Places_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    var place = item as Place;
                    List<UIElement> remove = new List<UIElement>();

                    foreach (var child in this.PlaceStack.Children)
                    {
                        if ((child as PlaceListItemControl).Place == place)
                        {
                            remove.Add(child);
                        }
                    }

                    foreach (var elem in remove)
                    {
                        this.PlaceStack.Children.Remove(elem);
                        elem.Tap -= PlaceControl_Tap;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    var place = item as Place;
                    AddPlace(place);
                }
            }

            // Hide or show tutorial text
            if (PlaceStack.Children.Count == 0)
            {
                TutorialText.Visibility = Visibility.Visible;
            }
            else
            {
                TutorialText.Visibility = Visibility.Collapsed;
            }
        }
          
         
        /// <summary>
        /// Adds a new place to UI list.
        /// </summary>
        /// <param name="place"></param>
        private void AddPlace(Place place)
        {
            var placeControl = new PlaceListItemControl();
            placeControl.Place = place;

            int imageIndex = (ImageIndex % 9) + 1;
            ImageIndex++;

            // Set background image
            string imageName = "/Assets/Place0{0}.jpg".FormatInvariant(imageIndex);
            var src = new BitmapImage(new Uri(imageName, UriKind.RelativeOrAbsolute));
            ((ImageBrush)placeControl.LayoutRoot.Background).ImageSource = src;

            this.PlaceStack.Children.Add(placeControl);
            placeControl.Tap += PlaceControl_Tap;
        }

        /// <summary>
        /// Notifies PlaceSelected listener
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void PlaceControl_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FSLog.Info();

            if (PlaceSelected != null)
            {
                var place = (sender as PlaceListItemControl).Place;
                PlaceSelected(this, new PlaceSelectedEventArgs { Place = place });
            }
        } 

        /// <summary>
        /// Initialize UI with current Places model.
        /// </summary>
        private void InitPlacesStack()
        {
            var places = SettingsManager.Places;

            if (places.Count == 0)
            {
                return;
            }

            TutorialText.Visibility = Visibility.Collapsed;

            foreach (Place place in places)
            {
                AddPlace(place);
            }
        }
    }
}
