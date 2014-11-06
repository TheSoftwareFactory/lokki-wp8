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
using Lokki.Settings;
using FSecure.Logging;
using FSecure.Utils.ExtensionMethods;
using FSecure.Lokki.Resources;
using FSecure.Lokki.ServerAPI;
using Microsoft.Phone.UserData;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using FSecure.Lokki.Utils;

namespace FSecure.Lokki.Controls
{  
    public partial class MyPeopleListItem : UserControl
    {

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
          "Model",
          typeof(PersonViewModel),
          typeof(MyPeopleListItem),
          new PropertyMetadata(null, new PropertyChangedCallback(OnModelChanged))
        );

        /// <summary>
        /// Is checked or not
        /// </summary>
        public PersonViewModel Model
        {
            get { return (PersonViewModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        private static void OnModelChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var self = sender as MyPeopleListItem;
            self.DataContext = self.Model;
        }
        /// <summary>
        /// Observes the People list of the Place it is representing and displays the avatars in vertical list.
        /// </summary>
        public MyPeopleListItem()
        {
            InitializeComponent();

            this.Loaded += MyPeopleListItem_Loaded;
        }

        void MyPeopleListItem_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MyPeopleListItem_Loaded;
        }
          
    }
}
