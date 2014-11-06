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

namespace FSecure.Lokki.Controls
{
    public enum BubbleButton
    {
        None,
        Ok,
        Later,
        Yes
    }

    public class BubbleButtonEventArgs : EventArgs
    {
        public BubbleButton Button { get; set; }
        public BubbleButtonEventArgs(BubbleButton button)
        {
            Button = button;
        }
    }

    public partial class AddFriendTutorialBubble : UserControl
    {
        /// <summary>
        /// Triggered after close animation.
        /// </summary>
        public event EventHandler<BubbleButtonEventArgs> Dismissed;
        private BubbleButton SelectedButton = BubbleButton.None;

        public AddFriendTutorialBubble()
        {
            InitializeComponent();
            
            this.Loaded += this_Loaded;
            this.Unloaded += this_Unloaded;
        }
       
        void this_Unloaded(object sender, RoutedEventArgs e)
        {
            Bubble.Dismissed -= Bubble_Dismissed;
        }

        void this_Loaded(object sender, RoutedEventArgs e)
        {
            Bubble.Dismissed += Bubble_Dismissed;
        }

        void Bubble_Dismissed(object sender, EventArgs e)
        {
            if (Dismissed != null)
            {
                Dismissed(this, new BubbleButtonEventArgs(SelectedButton));
                SelectedButton = BubbleButton.None;
            }
        }

        private void Dismiss(BubbleButton button)
        {
            SelectedButton = button;
            Bubble.Dismiss();
        }

        private void YesButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Dismiss(BubbleButton.Yes);
        }

        private void LaterButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Dismiss(BubbleButton.Later);
        }

    }
}
