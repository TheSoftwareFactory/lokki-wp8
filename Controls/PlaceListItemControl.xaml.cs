/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Lokki.Settings;
using System.Collections.ObjectModel;
using FSecure.Lokki.ServerAPI;
using System.Collections.Specialized;
  
namespace FSecure.Lokki.Controls
{
    public partial class PlaceListItemControl : UserControl
    {
        public static readonly DependencyProperty PlaceProperty = DependencyProperty.Register(
          "Place",
          typeof(Place),
          typeof(PlaceListItemControl),
          new PropertyMetadata(null)
        );

        /// <summary>
        /// The name of the place
        /// </summary>
        public Place Place
        {
            get 
            { 
                return (Place)GetValue(PlaceProperty); 
            }

            set 
            {
                if (Place != null)
                {
                    Place.People.CollectionChanged -= People_CollectionChanged;
                }

                SetValue(PlaceProperty, value);
                this.DataContext = value;

                this.AvatarStack.Children.Clear();
                value.People.CollectionChanged += People_CollectionChanged;
                foreach (Person person in value.People)
                {
                    AddPersonAvatar(person);
                }
            }
        }
         
        public PlaceListItemControl()
        {
            InitializeComponent();
        }

        private void AddPersonAvatar(Person person)
        {
            var avatar = new AvatarControl();
            avatar.Person = person;
            avatar.Height = 80;
            avatar.Width = 80;
            avatar.Margin = new Thickness(0, 0, 24, 0);

            this.AvatarStack.Children.Add(avatar);
        }

        /// <summary>
        /// Called when the place's people collection changes. Updated by PlacesList.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void People_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Person person in e.NewItems) 
                {
                    AddPersonAvatar(person);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (Person person in e.OldItems)
                {
                    AvatarControl control = null;
                    foreach(var ctrl in this.AvatarStack.Children)
                    {
                        if ((ctrl as AvatarControl).Person == person)
                        {
                            control = ctrl as AvatarControl;
                            break;
                        }
                    }

                    if (control != null)
                    {
                        this.AvatarStack.Children.Remove(control);
                    }
                }
            }
        }

        /// <summary>
        /// Handle deletion via context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MenuItemDelete_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            await ServerAPIManager.Instance.DeletePlace(this.Place);
        }
        
        /// <summary>
        /// Show place editor launched from context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MenuItemEdit_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var editor = new PlaceEditBubble();
            editor.Place = this.Place;
            await editor.Show();
        }
    }
}
