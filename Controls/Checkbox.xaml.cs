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
    public partial class Checkbox : UserControl
    {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
          "IsChecked",
          typeof(bool),
          typeof(Checkbox),
          new PropertyMetadata(false, new PropertyChangedCallback(OnIsCheckedChanged))
        );

        /// <summary>
        /// Is checked or not
        /// </summary>
        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public Checkbox()
        {
            //DataContext = this;

            InitializeComponent();

            this.Tap += Checkbox_Tap;

            this.Loaded += Checkbox_Loaded;
        }

        void Checkbox_Loaded(object sender, RoutedEventArgs e)
        {
            var self = (Checkbox)sender;
            if (self.IsChecked)
            {
                self.CheckMark.Visibility = Visibility.Visible;
            }
            else
            {
                self.CheckMark.Visibility = Visibility.Collapsed;
            }
        }

        void Checkbox_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.IsChecked = IsChecked ? false : true;
            e.Handled = true;
        }

        private static void OnIsCheckedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var self = (Checkbox)sender;
            if (self.IsChecked)
            {
                self.CheckMark.Visibility = Visibility.Visible;
            }
            else
            {
                self.CheckMark.Visibility = Visibility.Collapsed;
            }
        }
    }
}
