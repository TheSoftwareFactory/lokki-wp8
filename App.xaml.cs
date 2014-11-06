/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

#if DEBUG
#define DEBUG_AGENT
#endif

//#define HAVE_CRASH_LOG
#define HAVE_LOG_FILE

//#define HAVE_BACKGROUND_RUNNING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Threading;
using Microsoft.Phone.Scheduler;
using FSecure.Logging;
using Lokki.Settings;
using Microsoft.Phone.Info;
using FSecure.Utils;
using System.Windows.Controls.Primitives;
using System.Runtime.CompilerServices;
using FSecure.Lokki;
using FSecure.Lokki.ServerAPI;
using System.Windows.Markup;
using FSecure.Lokki.Resources;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Globalization;

using FSecure.Utils.ExtensionMethods;

namespace ringo_wp8
{
    public partial class App : Application
    {
        /// <summary>
        /// Provides easy access to the root frame of the Phone Application.
        /// </summary>
        /// <returns>The root frame of the Phone Application.</returns>
        public PhoneApplicationFrame RootFrame { get; private set; }

#if HAVE_BACKGROUND_RUNNING
        public static bool RunningInBackground { get; set; }
#endif

        private PeriodicTask PeriodicTask;
        private string PeriodicTaskName = "PeriodicLocationSendingAgent";

        /// <summary>
        /// Constructor for the Application object.
        /// </summary>
        public App()
        {

#if HAVE_LOG_FILE
            FSLog.LogFileEnabled = true;
#endif
            FSLog.Info("App starting");

#if HAVE_BACKGROUND_RUNNING
            RunningInBackground = false;
#endif
            // Global handler for uncaught exceptions. 
            UnhandledException += Application_UnhandledException;

            // Show graphics profiling information while debugging.
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // Display the current frame rate counters.
                //Application.Current.Host.Settings.EnableFrameRateCounter = true;

                // Show the areas of the app that are being redrawn in each frame.
                //Application.Current.Host.Settings.EnableRedrawRegions = true;

                // Enable non-production analysis visualization mode, 
                // which shows areas of a page that are being GPU accelerated with a colored overlay.
                //Application.Current.Host.Settings.EnableCacheVisualization = true;
            }

            // Standard Silverlight initialization
            InitializeComponent();

            // Phone-specific initialization
            InitializePhoneApplication();

            // Language display initialization
            InitializeLanguage();

            // For now this affects the textbox on signup page only
            (App.Current.Resources["PhoneTextBoxEditBorderBrush"] as SolidColorBrush).Color 
                = (Color)((ResourceDictionary)Resources["Colors"])["Main"];

            // Affects many texts, which should be white
            (App.Current.Resources["PhoneForegroundBrush"] as SolidColorBrush).Color
                = (Color)((ResourceDictionary)Resources["Colors"])["White"];

#if RELEASE
            AppRater.Initialize(TimeSpan.FromDays(4), 10);
#else
            AppRater.Initialize(TimeSpan.FromMinutes(5), 1);
#endif
            
            AppRater.IncreaseLaunchCount();

            PlacesManager.Instance.Initialize();
        }

        // Code to execute when the application is launching (eg, from Start)
        // This code will not execute when the application is reactivated
        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
            FSLog.Info();
#if HAVE_BACKGROUND_RUNNING 
            RunningInBackground = false;
#endif
            RemoveAgent();
        }

        // Code to execute when the application is activated (brought to foreground)
        // This code will not execute when the application is first launched
        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            FSLog.Info();

#if HAVE_BACKGROUND_RUNNING
            RunningInBackground = false;
#else
            // On application start, the locationing is started by signup page
            if (SettingsManager.IsLocationUploadEnabled)
            {
                GPSReporter.Instance.StartMonitoringGPS();
            }
#endif

            RemoveAgent();
            ClearUnreadMessagesFromLiveTile();
        }

        private void LogMemoryInfo()
        {
            var total = DeviceStatus.ApplicationMemoryUsageLimit;
            var free = DeviceStatus.ApplicationMemoryUsageLimit -
                        DeviceStatus.ApplicationCurrentMemoryUsage;
            var percent = (int)(free / (double)total * 100);
            FSLog.Info("Memory",
                "total:", total,
                "free:", free, percent);
        }

        // Code to execute when the application is deactivated (sent to background)
        // This code will not execute when the application is closing
        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            FSLog.Info();

#if HAVE_BACKGROUND_RUNNING
            if (RunningInBackground)
            {
                FSLog.Info("Background execution stopping");
                LogMemoryInfo();
            }
#else
            GPSReporter.Instance.StopMonitoringGPS();
#endif

            StartPeriodicAgent();
        }

        // Code to execute when the application is closing (eg, user hit Back)
        // This code will not execute when the application is deactivated
        private void Application_Closing(object sender, ClosingEventArgs e)
        {
            FSLog.Info();
#if HAVE_BACKGROUND_RUNNING
            if (RunningInBackground)
            {
                FSLog.Info("Background execution stopping");
                LogMemoryInfo();
            }
#endif
            StartPeriodicAgent();
        }

        private void RemoveAgent()
        {
            RemoveAgent(PeriodicTaskName);
        }

        private void RemoveAgent(string name)
        {
            if (ScheduledActionService.Find(name) != null)
            {
                ScheduledActionService.Remove(name);
            }
        }

        private void StartPeriodicAgent()
        {

            if (!SettingsManager.IsLocationUploadEnabled)
            {
                FSLog.Info("Locationing not enabled, agent not scheduled");
                return;
            }

            if (!SettingsManager.CurrentUser.IsVisible)
            {
                FSLog.Info("User invisible, agent not scheduled");
                return;
            }

            if (!GPSReporter.Instance.IsServiceEnabled)
            {
                FSLog.Info("Location service disabled, agent not scheduled");
                return;
            }

            StartPeriodicAgent(PeriodicTaskName);
        }

        private void StartPeriodicAgent(string name)
        {
            // Obtain a reference to the period task, if one exists
            PeriodicTask = ScheduledActionService.Find(name) as PeriodicTask;

            // If the task already exists and background agents are enabled for the
            // application, you must remove the task and then add it again to update 
            // the schedule
            if (PeriodicTask != null)
            {
                RemoveAgent(name);
            }

            PeriodicTask = new PeriodicTask(name);

            // TODO: Localize

            // The description is required for periodic agents. This is the string that the user
            // will see in the background services Settings page on the device.
            PeriodicTask.Description = "Background task for sending phone's location to Lokki service.";

            // Place the call to Add in a try block in case the user has disabled agents.
            try
            {
                ScheduledActionService.Add(PeriodicTask);
                FSLog.Info("BG agent scheduled");

#if(DEBUG_AGENT)
                ScheduledActionService.LaunchForTest(name, TimeSpan.FromSeconds(5));
#endif
            }
            catch (InvalidOperationException exception)
            {
                FSLog.Exception(exception);
                if (exception.Message.Contains("BNS Error: The action is disabled"))
                {
                    //MessageBox.Show("Background agents for this application have been disabled by the user.");
                }

                if (exception.Message.Contains("BNS Error: The maximum number of ScheduledActions of this type have already been added."))
                {
                    // No user action required. The system prompts the user when the hard limit of periodic tasks has been reached.
                }

            }
            catch (SchedulerServiceException exception)
            {
                FSLog.Exception(exception);
            }
        }

        private void ClearUnreadMessagesFromLiveTile()
        {
            FSLog.Info();

            ShellTile appTile = ShellTile.ActiveTiles.First();
            if (appTile != null)
            {
                // 2do: check is token 'ringo_wp8Token'
                FlipTileData flipTile = new FlipTileData();
                flipTile.Count = 0;
                flipTile.BackContent = "";
                appTile.Update(flipTile);
            }
        }

        private void Application_RunningInBackground(object sender, RunningInBackgroundEventArgs args)
        {
            FSLog.Info("Entering background");
            LogMemoryInfo();
#if HAVE_BACKGROUND_RUNNING
            RunningInBackground = true;
#endif
        }

        // Code to execute if a navigation fails
        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            FSLog.Exception(e.Exception);

            if (System.Diagnostics.Debugger.IsAttached)
            {
                // A navigation has failed; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        #region Crash dialog

        private const string CRASH_LOG_FILENAME = "crash-log.txt";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void StoreCrashLog()
        {
            try
            {
                var version = "{0} Lang:{1}".FormatLocalized(
                        new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version,                     
                        CultureInfo.CurrentUICulture.EnglishName);
                
                FSLog.Info("Application version:", version);
                
                lock (FSLog.Lock)
                {
                    using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        isoStore.MoveFile("log.txt", CRASH_LOG_FILENAME);
                    }
                }
            }
            catch (Exception)
            {
                // Avoid any new crashes in crash handler
            }
        }

        private void RemoveCrashLog()
        {
            using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isoStore.FileExists(CRASH_LOG_FILENAME))
                {
                    isoStore.DeleteFile(CRASH_LOG_FILENAME);
                }
            }
        }

        /// <summary>
        /// Returns true if crash log is found
        /// </summary>
        /// <returns></returns>
        private bool DidCrash()
        {
            using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
            {
                return isoStore.FileExists(CRASH_LOG_FILENAME);
            }
        }

        /// <summary>
        /// Show crash dialog if crashed and email address is defined for sending log
        /// </summary>
        public void ShowCrashDialog()
        {
            if (!DidCrash())
            {
                return;
            }
            
#if HAVE_CRASH_LOG
            var resp = MessageBox.Show(Localized.CrashDialogMessage.FormatLocalized(Localized.AppName),
                                       Localized.CrashDialogTitle, 
                                       MessageBoxButton.OKCancel);
            if (resp == MessageBoxResult.OK)
            {
                FSLogHelper.EmailSubject = "Lokki WP8 debug log";
                FSLogHelper.EmailReceiver = "lokki-feedback@f-secure.com";
                FSLogHelper.SendEmail(filepath: CRASH_LOG_FILENAME);
            }

            RemoveCrashLog();
            
#endif
        }

        #endregion

        // Code to execute on Unhandled Exceptions
        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            FSLog.Exception(e.ExceptionObject);

            StoreCrashLog();

            if (System.Diagnostics.Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        #region Phone application initialization

        // Avoid double-initialization
        private bool phoneApplicationInitialized = false;

        // Do not add any additional code to this method
        private void InitializePhoneApplication()
        {
            if (phoneApplicationInitialized)
                return;

            // Create the frame but don't set it as RootVisual yet; this allows the splash
            // screen to remain active until the application is ready to render.
            RootFrame = new TransitionFrame();
            RootFrame.Navigated += CompleteInitializePhoneApplication;

            // Template with page title
            RootFrame.Template = Current.Resources["ApplicationBackgroundTemplate"] as ControlTemplate;

            // Handle navigation failures
            RootFrame.NavigationFailed += RootFrame_NavigationFailed;

            // Ensure we don't initialize again
            phoneApplicationInitialized = true;


        }

        // Do not add any additional code to this method
        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            // Set the root visual to allow the application to render
            if (RootVisual != RootFrame)
            {
                RootVisual = RootFrame;
            }

            // Remove this handler since it is no longer needed
            RootFrame.Navigated -= CompleteInitializePhoneApplication;

            ShowCrashDialog();

        }

        // Initialize the app's font and flow direction as defined in its localized resource strings.
        //
        // To ensure that the font of your application is aligned with its supported languages and that the
        // FlowDirection for each of those languages follows its traditional direction, ResourceLanguage
        // and ResourceFlowDirection should be initialized in each resx file to match these values with that
        // file's culture. For example:
        //
        // AppResources.es-ES.resx
        //    ResourceLanguage's value should be "es-ES"
        //    ResourceFlowDirection's value should be "LeftToRight"
        //
        // AppResources.ar-SA.resx
        //     ResourceLanguage's value should be "ar-SA"
        //     ResourceFlowDirection's value should be "RightToLeft"
        //
        // For more info on localizing Windows Phone apps see http://go.microsoft.com/fwlink/?LinkId=262072.
        //
        private void InitializeLanguage()
        {
            try
            {
                // Set the font to match the display language defined by the
                // ResourceLanguage resource string for each supported language.
                //
                // Fall back to the font of the neutral language if the Display
                // language of the phone is not supported.
                //
                // If a compiler error is hit then ResourceLanguage is missing from
                // the resource file.
                RootFrame.Language = XmlLanguage.GetLanguage(Localized.ResourceLanguage);

                // Set the FlowDirection of all elements under the root frame based
                // on the ResourceFlowDirection resource string for each
                // supported language.
                //
                // If a compiler error is hit then ResourceFlowDirection is missing from
                // the resource file.
                FlowDirection flow = (FlowDirection)Enum.Parse(typeof(FlowDirection), Localized.ResourceFlowDirection);
                RootFrame.FlowDirection = flow;
            }
            catch
            {
                // If an exception is caught here it is most likely due to either
                // ResourceLangauge not being correctly set to a supported language
                // code or ResourceFlowDirection is set to a value other than LeftToRight
                // or RightToLeft.

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                throw;
            }
        }

        #endregion

        /// <summary>
        /// Global progress bar
        /// </summary>
        public ProgressBar ProgressIndicator { get; set; }

        private void ProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            ProgressIndicator = sender as ProgressBar;
            ProgressIndicator.Visibility = Visibility.Collapsed;
            ProgressIndicator.Opacity = 1;
            ProgressIndicator.Loaded -= ProgressBar_Loaded;
        }

        public void HideHeaderPanel()
        {
            if (HeaderPanel != null)
            {
                HeaderPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void ShowHeaderPanel() 
        {
            if (HeaderPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            HeaderPanel.Fill = (SolidColorBrush)(this.Resources["Colors"] as ResourceDictionary)["BRUSH_Main"];
            HeaderPanel.Visibility = Visibility.Visible;
            //HeaderPanel.Opacity = 1;
            
            FSAnim.Fade(HeaderPanel,
                from : 0,
                to : 1,
                duration : 100,
                startTime : 500,
                start : true);
        }

        public Rectangle HeaderPanel { get; set; }
        private void HeaderPanel_Loaded(object sender, RoutedEventArgs e)
        {
            HeaderPanel = sender as Rectangle;
            HeaderPanel.Visibility = Visibility.Collapsed;
            HeaderPanel.Opacity = 0;
        }
    }
}
