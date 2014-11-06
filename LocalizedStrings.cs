/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using FSecure.Lokki.Resources;

namespace FSecure.Lokki
{
    /// <summary>
    /// Provides access to string resources.
    /// </summary>
    public class LocalizedStrings
    {
        private static Localized _localizedResources = new Localized();

        public Localized LocalizedResources { get { return _localizedResources; } }
    }
}