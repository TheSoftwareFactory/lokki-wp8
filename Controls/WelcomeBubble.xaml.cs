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
using FSecure.Logging;

namespace FSecure.Lokki.Controls
{
    public partial class WelcomeBubble : UserControl
    {
        /// <summary>
        /// Triggered after close animation.
        /// </summary>
        public event EventHandler Dismissed;

        public WelcomeBubble()
        {
            InitializeComponent();

            this.Loaded += WelcomeBubble_Loaded;
            this.Unloaded += WelcomeBubble_Unloaded;
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
                Dismissed(this, new EventArgs());
            }
        }

        private void OK_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Bubble.Dismiss();
        }

    }
}
