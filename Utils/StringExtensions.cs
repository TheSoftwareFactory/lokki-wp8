/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Utils.ExtensionMethods
{
    public static class StringExtensions
    {
        public static string Left(this string text, int length)
        {
            if (text.Length < length) return text;
            return text.Substring(0, length);
        }
         
        public static string Right(this string text, int length)
        {
            if (text.Length < length) return text;
            return text.Substring(text.Length - length, length);
        }

        /// <summary>
        /// Use this instead of string.Format when formatting UI strings
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string FormatLocalized(this string format, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        /// <summary>
        /// Use this instead of string.Format when formatting non-UI strings
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string FormatInvariant(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        /// <summary>
        /// Use this when converting integer to non-UI string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ToStringInvariant(this int obj)
        {
            return obj.ToString(CultureInfo.InvariantCulture);
        }
    }
}
