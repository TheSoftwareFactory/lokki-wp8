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
    /// <summary>
    /// Utility extensions
    /// </summary>
    public static class MathExtensions
    {
        public static double ToRadians(this double degrees)
        {
            return (Math.PI / 180.0) * degrees;
        }

        public static double ToDegrees(this double radians)
        {
            return (180.0 / Math.PI) * radians;
        }
    }
}
