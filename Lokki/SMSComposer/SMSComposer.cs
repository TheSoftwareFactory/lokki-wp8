/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Plugin for sending SMS

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
    class SMSComposer
    {
        /** SMSComposer.showSMSComposer( { toRecipients : "number1;number2", body : "message" } ) */
        public static void showSMSComposer(List<string> to, string body)
        {
            FSLog.Info();
             
            var task = new SmsComposeTask();
            task.To = string.Join(";", to); // multiple separated with semicolon
            task.Body = body;
            task.Show();
        }
    }
}
