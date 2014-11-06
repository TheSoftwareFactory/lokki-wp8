/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
///
/// Converted from bool to Visibility
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace FSecure.Converters
{
    public class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            if (value.GetType() != typeof(bool))
            {
                throw new NotSupportedException();
            }
            bool isChecked = (bool)value;

            // True becomes Collapsed
            bool inverted = false;
            if (parameter != null)
            {
                inverted = bool.Parse(parameter.ToString());
            }

            if (inverted)
            {
                isChecked = isChecked != true;
            }

            if (isChecked)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
