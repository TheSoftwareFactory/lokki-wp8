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

namespace FSecure.Lokki.Controls
{
    public partial class SettingButton : UserControl
    {

        #region Properties

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
          "Title",
          typeof(string),
          typeof(SettingButton),
          new PropertyMetadata(null)
        );

        /// <summary>
        /// The text shown on bubble
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public bool IsSelected
        {
            get
            {
                return this.VisualStateGroup.CurrentState.Name != "Unselected";
            }
            set
            {
                VisualStateManager.GoToState(this, value ? "Selected" : "Unselected", true);
            }
        }

        public CornerRadius CornerRadius
        {
            get
            {
                return TerrainBorder.CornerRadius;
            }
            set
            {
                TerrainBorder.CornerRadius = value;
            }
        }

        #endregion

        public SettingButton()
        {
            this.DataContext = this;
            InitializeComponent();
        }
    }
}
