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
using FSecure.Lokki.Resources;
using FSecure.Utils.ExtensionMethods;

namespace FSecure.Lokki.Controls
{
    public partial class CanSeeYouTutorialBubble : UserControl
    {
        /// <summary>
        /// Triggered after close animation.
        /// </summary>
        public event EventHandler Dismissed;

        #region Properties
        
        public string Names { 
            set {
                Title = Localized.TutorialCanSeeYou.FormatLocalized(value);
            }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
          "Title",
          typeof(string),
          typeof(CanSeeYouTutorialBubble),
          new PropertyMetadata(null)
        );


        /// <summary>
        /// The text shown on bubble
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            private set { SetValue(TitleProperty, value); }
        }

        #endregion

        public CanSeeYouTutorialBubble()
        {
            InitializeComponent();

            InfoText = (Bubble.Child as Grid).Children.First() as TextBlock;
            InfoText.DataContext = this;
            
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
    }
}
