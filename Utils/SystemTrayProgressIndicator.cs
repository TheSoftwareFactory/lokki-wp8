/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using Microsoft.Phone.Shell;
using ringo_wp8;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FSecure.Utils
{
    public static class SystemTrayProgressIndicator
    {
        private static int _TaskCount = 0;
        public static int TaskCount {
            get{
                return _TaskCount;
            }
            set{
                _TaskCount = value;
                if (_TaskCount <= 0)
                {
                    IsVisible = false;
                }
                else
                {
                    IsVisible = true;
                }
            }
        }

        public static bool IsVisible 
        {
            get 
            {
                return ((App)App.Current).ProgressIndicator.Visibility == Visibility.Visible;
            }
            set 
            {
                ((App)App.Current).ProgressIndicator.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                _TaskCount = 0;
            }
        }
         
    }
}