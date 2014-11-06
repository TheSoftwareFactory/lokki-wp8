/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Utils.ExtensionMethods
{
    public static class DateExtensions
    {
        public static DateTime DateTimeSince1970Utc {
            get
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }   
        public static long ToMillisecondsSince1970(this DateTime date)
        {
            var diff = date.ToUniversalTime().Subtract(DateTimeSince1970Utc);
            return (long)diff.TotalMilliseconds;
        }

        public static DateTime ToDateTimeFromMillisecondsSince1970(this long time)
        {
            var date = DateTimeSince1970Utc + TimeSpan.FromMilliseconds(time);
            
            return date;
        }

        public static long ToMillisecondsSince1970(this DateTimeOffset date)
        {
            var diff = date.UtcDateTime.Subtract(DateTimeSince1970Utc);
            return (long)diff.TotalMilliseconds;
        }

        public static DateTimeOffset ToDateTimeOffsetFromMillisecondsSince1970(this long time)
        {
            return new DateTimeOffset(time.ToDateTimeFromMillisecondsSince1970());
        }
    }
}
        