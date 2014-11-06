/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FSecure.Utils.ExtensionMethods;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;

namespace FSecure.Lokki.Utils
{
    class LokkiUtils
    {
        public static string GetInitials(string name)
        {
            var parts = name.Split(' ');
            if (parts.Length == 0)
            {
                return "";
            }

            string initials = "";
            if (parts.Length == 1)
            {
                initials = parts[0].Left(2);
            }
            else
            {
                initials = parts[0].Left(1);
                initials += parts[parts.Length - 1].Left(1);
            }

            return initials.ToUpper(CultureInfo.CurrentCulture);
        }

        public static void DumpVisualTree(object element, int logindent = 0)
        {
            string indent = "";
            for (int i = 0; i < logindent; i++)
            {
                indent += "  ";
            }
            Debug.WriteLine(indent + element.ToString());
            
            var children = VisualTreeHelper.GetChildrenCount(element as DependencyObject);
            for (int i = 0; i < children; i++)
            {
                var c = VisualTreeHelper.GetChild(element as DependencyObject, i);
                DumpVisualTree(c, logindent + 1);
            }
        }
    }
}
