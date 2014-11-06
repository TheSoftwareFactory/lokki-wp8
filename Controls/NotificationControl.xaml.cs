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
using FSecure.Utils;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace FSecure.Lokki.Controls
{
    public partial class NotificationControl : UserControl
    {
        
        /// <summary>
        /// Triggered after close animation.
        /// </summary>
        public event EventHandler Dismissed;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
          "Text",
          typeof(string),
          typeof(NotificationControl),
          new PropertyMetadata(null)
        );

        /// <summary>
        /// Timer used to auto close the notification
        /// </summary>
        DispatcherTimer CloseTimer;

        /// <summary>
        /// Animation for displaying the notification
        /// </summary>
        Storyboard ShowAnimation;

        /// <summary>
        /// The text shown on bubble
        /// </summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public NotificationControl()
        {
            InitializeComponent();

            InfoText = (Bubble.Child as Grid).Children.First() as TextBlock;
            InfoText.DataContext = this;
             
            Bubble.PointerPosition = PointerHint.None;
            Bubble.PointAt = new Point(240, 600);
            Bubble.TapOutsideCloses = true;
        }

        public void Show()
        {
            ShowAnimation = FSAnim.Fade(this, from: 0, to: 1, duration : 250, start: true);
            CloseTimer = FSCall.Delayed(() =>
            {
                Bubble.Dismiss();
            }, TimeSpan.FromSeconds(2));
        }

        void WelcomeBubble_Unloaded(object sender, RoutedEventArgs e)
        {
            Bubble.Dismissed -= Bubble_Dismissed;
        }

        void WelcomeBubble_Loaded(object sender, RoutedEventArgs e)
        {
            Bubble.Dismissed += Bubble_Dismissed;
        }

        void Bubble_Dismissed(object sender, EventArgs e)
        {
            if (Dismissed != null)
            {
                if (CloseTimer != null)
                {
                    CloseTimer.Stop();
                }

                if(ShowAnimation != null)
                    ShowAnimation.Stop();

                Dismissed(this, new EventArgs());
            }
        }
    }
}
