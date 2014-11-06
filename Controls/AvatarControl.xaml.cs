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
using FSecure.Lokki.Utils;
using Microsoft.Phone.UserData;
using Lokki.Settings;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using FSecure.Logging;

namespace FSecure.Lokki.Controls
{
    public partial class AvatarControl : UserControl
    {
        /// <summary>
        /// Common bitmap for default avatars.
        /// </summary>
        private static readonly BitmapImage DefaultAvatar = new BitmapImage(new Uri("/Assets/DefaultAvatar.png", UriKind.RelativeOrAbsolute));

        public ImageBrush Avatar { get; set; }

        public static readonly DependencyProperty PersonProperty = DependencyProperty.Register(
            "Person",
            typeof(Person),
            typeof(AvatarControl),
            new PropertyMetadata(default(object), new PropertyChangedCallback(OnPersonChanged))
        );

        private static void OnPersonChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var self = (AvatarControl)sender;
            
            self.UpdateInitials(self.Person.Name);
            self.RefreshPinImage();
            self.RefreshVisibilityIcon();
            //self.VisibilityIndicator.GetBindingExpression(Grid.VisibilityProperty).UpdateSource();

            if (e.OldValue != null)
            {
                ((Person)e.OldValue).PropertyChanged -= self.Person_PropertyChanged;
            }
            self.Person.PropertyChanged += self.Person_PropertyChanged;
        }

        void Person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsVisible")
            {
                Dispatcher.BeginInvoke(RefreshVisibilityIcon);
            }
        }

        private void RefreshVisibilityIcon()
        {
            if (this.Person.IsVisible)
            {
                VisibilityIndicator.Visibility = Visibility.Collapsed;
            }
            else
            {
                VisibilityIndicator.Visibility = Visibility.Visible;
            }
        }

        public Person Person
        {
            get { return (Person)GetValue(PersonProperty); }
            set { 
                SetValue(PersonProperty, value);
                this.DataContext = value;
            }
        }
         
        public AvatarControl()
        {
            InitializeComponent();
        }

        private void SetDefaultAvatar()
        {
            ((ImageBrush)this.AvatarArc.Fill).ImageSource = DefaultAvatar;
        }

        public void UpdateInitials(string name)
        {
            InitialsText.Text = LokkiUtils.GetInitials(name);
        }
        /// <summary>
        /// 
        /// </summary>
        public void RefreshPinImage()
        {
            if (Person == null || string.IsNullOrWhiteSpace(Person.Email))
            {
                FSLog.Warning("Not valid email");
                SetDefaultAvatar();
                return;
            }

#if STORE_SCREENSHOTS
            Avatar = GetStoreScreenshotPhoto();
            InitialsText.Visibility = Visibility.Collapsed;
#endif
            if (Avatar == null)
            {
                Dispatcher.BeginInvoke(async () => {

                    var contactsData = await ContactsManager.Instance.GetContactData(Person.Email);
                    
                    if (!this.Person.Name.Equals(contactsData.DisplayName))
                    {
                        this.Person.Name = contactsData.DisplayName;
                    }

                    if (contactsData.Photo != null)
                    {
                        InitialsText.Visibility = Visibility.Collapsed;
                        ((ImageBrush)this.AvatarArc.Fill).ImageSource = contactsData.Photo;
                        Avatar = ((ImageBrush)this.AvatarArc.Fill);
                    }
                    else
                    {
                        InitialsText.Visibility = Visibility.Visible;
                        UpdateInitials(this.Person.Name);

                        SetDefaultAvatar();
                    }
                });

            }
            else
            {
                this.AvatarArc.Fill = Avatar;
            }
        }
#if STORE_SCREENSHOTS
        ImageBrush GetStoreScreenshotPhoto()
        {

            string imagename = null;
            if (Person.IsCurrent)
            {
                imagename = "user.jpg";
            }
            else
            {
                imagename = Person.Email.Split('@').First() + ".jpg";
            }

            var src = new BitmapImage(new Uri("/SubmissionInfo/" + imagename, UriKind.RelativeOrAbsolute));
            ((ImageBrush)this.AvatarArc.Fill).ImageSource = src;
            return ((ImageBrush)this.AvatarArc.Fill);
        }
#endif

    }
}
