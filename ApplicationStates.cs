/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FSecure.Logging;

namespace FSecure.Lokki
{
    /// <summary>
    /// Provides static interface to PhoneApplicationService.Current.State
    /// 
    /// An item can be removed from State by setting its value to null.
    /// 
    /// </summary>
    public static class ApplicationStates
    {
        private static void SetItem(object value, [CallerMemberName] string key = "")
        {
            if (value == null)
            {
                PhoneApplicationService.Current.State.Remove(key);
            }
            else
            {
                PhoneApplicationService.Current.State[key] = value;
            }
        }

        private static T GetItem<T>([CallerMemberName] string key = "")
        {
            object val = null;
            try
            {
                PhoneApplicationService.Current.State.TryGetValue(key, out val);
            }
            catch (Exception e)
            {
                FSLog.Exception(e);
            }
            return (T)val;
        }
         
        public static bool InvitationEMailStarted
        {
            // Exception with bool value
            get { return bool.TrueString.Equals(GetItem<string>()); }
            set { SetItem(value.ToString()); }
        }

        /// <summary>
        /// Used to indicate that user has chosen email address for invitation
        /// </summary>
        public static string InvitationEMail
        {
            set { SetItem(value); }
            get { return GetItem<string>(); }
        }

        public static string InvitationDisplayName
        {
            set { SetItem(value); }
            get { return GetItem<string>(); }
        }

        public static bool IsLocationServiceChecked
        {
            // Exception with bool value
            get { return bool.TrueString.Equals(GetItem<string>()); }
            set { SetItem(value.ToString()); }
        }

        public static bool IsDashboardRequestNeeded
        {
            // Exception with bool value
            get { return bool.TrueString.Equals(GetItem<string>()); }
            set { SetItem(value.ToString()); }
        }
    }

}
