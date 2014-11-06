/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// Send the log via email. In own file to allow use of FSLog from background agent.

using Microsoft.Phone.Tasks;
using System;
using System.IO;
using System.IO.IsolatedStorage;

namespace FSecure.Logging
{
    public class FSLogEmailSender {

        const int MAX_BODY_SIZE = 63 * 1024;

        /// <summary>
        /// Send recored log using email.
        /// </summary>
        public static void Send(string recipient = "", string subject = "",
            string bodyMessage = "")
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            try
            {
                if (storage.FileExists(FSLog.LogFile))
                {
                    EmailComposeTask emailTask = new EmailComposeTask();

                    if (bodyMessage.Length > 0)
                    {
                        bodyMessage += "\r\n";
                    }

                    using (var isoStream = new IsolatedStorageFileStream(FSLog.LogFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, storage))
                    {
                        var logFile = new StreamReader(isoStream);
                        
                        string body = logFile.ReadToEnd();

                        int bodyMessageLength = System.Text.Encoding.Unicode.GetByteCount(bodyMessage);
                        int length = System.Text.Encoding.Unicode.GetByteCount(body);
                        while (length > (MAX_BODY_SIZE - bodyMessageLength))
                        {
                            int removed = Math.Max(1, body.Length - MAX_BODY_SIZE / 2 - bodyMessageLength);
                            body = body.Remove(0, removed);
                            length = System.Text.Encoding.Unicode.GetByteCount(body);
                        }

                        // Find first newline to avoid cut first line
                        var nlpos = body.IndexOf('\n');
                        if (nlpos >= 0 && body.Length >= nlpos + 2)
                        {
                            body = body.Substring(nlpos + 1);
                        }

                        emailTask.To = recipient;
                        emailTask.Subject = subject;
                        emailTask.Body = bodyMessage + body;
                    }

                    emailTask.Show();
                }
            }
            finally
            {
                if (storage != null)
                {
                    storage.Dispose();
                }
            }
        }
    }
}