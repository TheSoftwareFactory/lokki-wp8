/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
/// 
/// File Logging.
 
using Microsoft.Phone.Tasks;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Threading;

namespace FSecure.Logging
{

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// API for sending log output.
    /// </summary>
    public sealed class FSLog
    {

        // disallow constructing instance of class that has only static members
        private FSLog() { }

        static FSLog() {
            LogFile = "log.txt";
        }

        public static readonly Mutex Lock = new Mutex(false, "fsecure-log-file");
        
        // Delete log file if it's older than 12 hours
        public const long CleanupInterval = (12 * TimeSpan.TicksPerHour);

        private static bool _logFileEnabled = false;
        // set to true if logging to file is needed
        public static bool LogFileEnabled
        {
            get
            {
                return _logFileEnabled;
            }
            set
            {
                if (_logFileEnabled != value)
                {
                    _logFileEnabled = value;
                }
            }
        }
         
        public static string LogFile{ get; set;}

        internal static string[] LevelStrings = new string[] { "D", "I", "W", "E", "F" };

        /// <summary>
        /// Send a DEBUG log message.
        /// </summary>
        /// <param name="message">The message you would like logged.</param>
        public static void Debug(params object[] message)
        {
            Print(message, LogLevel.Debug);
        }

        /// <summary>
        /// Send a ERROR log message.
        /// </summary>
        /// <param name="message">The message you would like logged.</param>
        public static void Error(params object[] message)
        {
            Print(message, LogLevel.Error);
        }

        /// <summary>
        /// Send a INFO log message.
        /// </summary>
        /// <param name="message">The message you would like logged.</param>
        public static void Info(params object[] message)
        {
            Print(message, LogLevel.Info);
        }

        /// <summary>
        /// Send a WARNING log message.
        /// </summary>
        /// <param name="message">The message you would like logged.</param>
        public static void Warning(params object[] message)
        {
            Print(message, LogLevel.Warning);
        }

        /// <summary>
        /// Send a FATAL log message.
        /// </summary>
        /// <param name="message">The message you would like logged.</param>
        public static void Fatal(params object[] message)
        {
            Print(message, LogLevel.Fatal);
        }

        /// <summary>
        /// Send a EXCEPTION log message.
        /// </summary>
        /// <param name="e">An exception to log</param>
        public static void Exception(Exception e)
        {
            string text = string.Format(CultureInfo.InvariantCulture, "{0} [{1}] E: Exception caught: {2}\n{3}\n{4}",
                DateTime.Now.ToString(@"yyyy-MM-dd hh\:mm\:ss.ffff", CultureInfo.InvariantCulture), 
                Thread.CurrentThread.ManagedThreadId,
                e.GetType().ToString(),
                e.Message, 
                e.StackTrace);

            if (e.InnerException != null)
            {
                var inner = e.InnerException;
                text += string.Format(CultureInfo.InvariantCulture, "\nInnerException: {0}\n{1}\n{2}",
                    inner.GetType().ToString(),
                    inner.Message,
                    inner.StackTrace);
            }

            if (LogFileEnabled)
            {
                WriteToFile(text);
            }
            System.Diagnostics.Debug.WriteLine(text);
        }

        /// <summary>
        /// Delete log file from isolated storage.
        /// </summary>
        public static void DeleteLog()
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (storage.FileExists(LogFile))
            {
                storage.DeleteFile(LogFile);
            }
        }

        /// <summary>
        /// Generates log message using date, thread ID, function name and message.
        /// Sends message to debug port and incase of debug build, writes to debug file in isolated storage.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">Debug level (DEBUG, INFO, WARNING, ERROR, FATAL).</param>
        private static void Print(object[] message, LogLevel level)
        {
            MethodBase method = null;

            // Find the caller which is the first method that is not from FSLog.
            // Our helper methods are optimized away on release builds and caused a crash when 2 frames were skipped.
            // Logging a destructor caused crash, as GC is not included in the stacktrace and destructor is called
            // directly leaving only 2 items in the stack. Also displayed wrong caller method.
            // [MethodImpl(MethodImplOptions.NoOptimization)] didn't work

            var frame = new StackFrame(1);
            method = frame.GetMethod();
            if (method.DeclaringType == typeof(FSLog))
            {
                // This is when helpers are not optimized away
                frame = new StackFrame(2);
                method = frame.GetMethod();
            }

            string typename = "????";
            string methodName = "????";
            if (method != null)
            {
                methodName = method.Name;
                typename = method.DeclaringType.Name;
            }

            string tag = String.Format(CultureInfo.InvariantCulture, "{0}::{1}", typename, methodName);
            string msg = String.Join(" ", message);
            WriteLine(level, tag, msg);
        }

        public static void WriteLine(LogLevel level, string tag, params object[] message)
        {
            string msg = String.Join(" ", message);
            
            string text = string.Format(CultureInfo.InvariantCulture, "{0} [{1}] {2}: {3}: {4}", 
                DateTime.Now.ToString(@"yyyy-MM-dd hh\:mm\:ss.ffff", CultureInfo.InvariantCulture),
                Thread.CurrentThread.ManagedThreadId, LevelStrings[(int)level], tag, msg);
            if (LogFileEnabled)
            {
                WriteToFile(text);
            }
            System.Diagnostics.Debug.WriteLine(text);
        }

        /// <summary>
        /// Writes text to log file located in isolated storage.
        /// Clears log file if it's older than CleanupInterval
        /// </summary>
        /// <param name="text">Text to be written in the log file.</param>
        private static void WriteToFile(string text)
        {   
            // http://stackoverflow.com/questions/15456986/how-to-gracefully-get-out-of-abandonedmutexexception
            // "if you can assure the integrity of the data structures protected by the mutex you can simply ignore the exception and continue executing your application normally."
            // The bg agent can terminate and the mutex is not released properly
            try
            {
                Lock.WaitOne();
            }
            catch (AbandonedMutexException e)
            {
                FSLog.Exception(e);
            }
            catch (Exception e)
            {
                FSLog.Exception(e);
            }

            try
            {
                IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
                if (storage.FileExists(LogFile))
                {
                    if ((DateTime.Now.Ticks - CleanupInterval) > storage.GetLastWriteTime(LogFile).Ticks)
                    {
                        storage.DeleteFile(LogFile);
                    }
                }

                IsolatedStorageFileStream isoStream = null;
                try
                {
                    isoStream = new IsolatedStorageFileStream(LogFile, FileMode.Append,
                           FileAccess.Write, FileShare.Write, IsolatedStorageFile.GetUserStoreForApplication());
                    
                    using (StreamWriter logFile = new StreamWriter(isoStream))
                    {
                        isoStream = null;
                        logFile.WriteLine(text);
                        logFile.Flush();
                    }
                }
                finally
                {
                    if (isoStream != null)
                    {
                        isoStream.Dispose();
                    }
                }
            }
            finally
            {
                Lock.ReleaseMutex();
            }
        }
         
        /// <summary>
        /// Open log file with user selected application
        /// </summary>
        public static void OpenLog()
        {
            var uri = new Uri("ms-appdata:///local/" + LogFile);
            
            var fileTask = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri).AsTask();
            fileTask.Wait();

            Windows.Storage.StorageFile file = fileTask.Result;

            if (file != null)
            {
                // NOTE: There must be a timeout on the wait here
                //       or resuming freezes when navigating back 
                //       from the launched application.
                //       The behavior is unknown without the wait, may or may not work or blow up
                //       (got Catastrophic failure once)
                var asyncOperation = Windows.System.Launcher.LaunchFileAsync(file);
                var task = asyncOperation.AsTask();
                task.Wait(TimeSpan.FromSeconds(2));
            }
        }

    }
}