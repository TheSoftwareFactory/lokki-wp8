/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Plugin for sharing in social media

using FSecure.Logging;
using Microsoft.Phone.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
 
namespace WPCordovaClassLib.Cordova.Commands
{ 
    class Share
    {
        public static void shareLink(Uri link, string message, string title)
        {
            FSLog.Info();
              
            var task = new ShareLinkTask();
            task.LinkUri = link;
            task.Message = message;
            task.Title = title;
            task.Show();
        }
    }
}

