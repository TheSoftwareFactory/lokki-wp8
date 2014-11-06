/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Logging;
using FSecure.Lokki.Resources;
using Lokki.Settings;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using FSecure.Utils.ExtensionMethods;

namespace FSecure.Utils
{
    public static class AppRater
    {
        private static int MinimumAppStarts = 0;

        public static void Initialize(TimeSpan checkTimeSpan, int minimumAppStarts)
        {
            MinimumAppStarts = minimumAppStarts;
            if (SettingsManager.AppRaterQueryDate == null
            || SettingsManager.AppRaterQueryDate == default(DateTimeOffset) )
            {
                SettingsManager.AppRaterQueryDate = DateTimeOffset.UtcNow + checkTimeSpan;
                FSLog.Info(SettingsManager.AppRaterQueryDate);
            }

        }

        public static void IncreaseLaunchCount()
        {
            SettingsManager.AppStartCount++;
        }

        public static void Check()
        {
            // For testing
            //SettingsManager.AppRaterShown = false;
            //SettingsManager.AppRaterDoYouLikeQuestionShown = false;
            //SettingsManager.AppRaterQueryDate = DateTimeOffset.UtcNow;

            if (SettingsManager.AppRaterShown) return;

            if (SettingsManager.AppStartCount < MinimumAppStarts)
            {
                FSLog.Debug("Not enough app starts", SettingsManager.AppStartCount, "<", MinimumAppStarts);
                return;
            }

            var queryDate = SettingsManager.AppRaterQueryDate;
            if (queryDate.CompareTo(DateTimeOffset.UtcNow) <= 0)
            {
                if (!SettingsManager.AppRaterDoYouLikeQuestionShown)
                {
                    ShowDoYouLikeDialog();
                }
                else
                {
                    ShowUserLikesRateDialog();
                }
            }

        }

        private static void ShowDoYouLikeDialog()
        {
            FSLog.Info();
            var messageBox = MessageBoxQueue.Instance.Show(Localized.AppRaterDoYouLikeTitle,
                                                           Localized.AppRaterDoYouLikeQuestion,
                                                           leftButtonContent: Localized.AppRaterYes,
                                                           rightButtonContent: Localized.AppRaterNo);
            messageBox.Dismissed += ShowUserDislikesDialog_Dismissed;
        }

        private static ToggleSwitch CreateRemindMeSwitch()
        {
            var toggle = new ToggleSwitch();
            toggle.IsChecked = true;
            toggle.Header = Localized.AppRaterRemindLater;

            return toggle;
        }

        private static void ShowUserLikesRateDialog()
        {
            FSLog.Info();

            ToggleSwitch sw = CreateRemindMeSwitch();
            var messageBox = MessageBoxQueue.Instance.Show(Localized.AppRaterReviewQuestionTitle,
                                                           Localized.AppRaterReviewQuestion,
                                                           content: sw,
                                                           leftButtonContent: Localized.AppRaterReviewQuestionTitle,
                                                           rightButtonContent: Localized.AppRaterNoThanks);
            //sw.Foreground = messageBox.Background;
            sw.SwitchForeground = messageBox.Background;

            messageBox.Dismissed += (object sender, DismissedEventArgs e) =>
            {
                if (e.Result == CustomMessageBoxResult.LeftButton)
                {
                    FSLog.Info("Display marketplace task");

                    // Don't ask again
                    SettingsManager.AppRaterShown = true;

                    MarketplaceReviewTask marketplaceReviewTask = new MarketplaceReviewTask();
                    marketplaceReviewTask.Show();
                }
                else
                {
                    if (sw.IsChecked.HasValue && sw.IsChecked.Value)
                    {
                        ResetQueryDateToRemind();
                    }
                    else
                    {
                        FSLog.Info("Don't show app rater anymore");
                        SettingsManager.AppRaterShown = true;
                    }
                }
            };
        }

        /// <summary>
        /// Remind the next day
        /// </summary>
        private static void ResetQueryDateToRemind()
        {
            var date = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(1));
            FSLog.Info(date);

            SettingsManager.AppRaterQueryDate = date;
        }

        private static void ShowUserDislikesDialog()
        {
            FSLog.Info();
            var messageBox = MessageBoxQueue.Instance.Show(Localized.AppRaterReviewQuestionTitle,
                                                                Localized.AppRaterSendFeedbackQuestion,
                                                                leftButtonContent: Localized.AppRaterYes,
                                                                rightButtonContent: Localized.AppRaterNo);

            messageBox.Dismissed += (object s2, DismissedEventArgs e2) =>
            {
                SettingsManager.AppRaterShown = true;

                if (e2.Result == CustomMessageBoxResult.LeftButton)
                {
                    var version = XDocument.Load("WMAppManifest.xml").Root.Element("App").Attribute("Version").Value;

                    EmailComposeTask emailTask = new EmailComposeTask();
                    emailTask.Subject = Localized.AppRaterFeedBackEmailSubject.FormatLocalized(version);
                    emailTask.Body = Localized.AppRaterSendFeedbackBodyWhenUserDoesNotLike;
                    emailTask.To = "lokki-feedback@f-secure.com";
                    emailTask.Show();
                }
            };
        }

        private static void ShowUserDislikesDialog_Dismissed(object sender, DismissedEventArgs e)
        {
            FSLog.Info();

            SettingsManager.AppRaterDoYouLikeQuestionShown = true;

            if (e.Result == CustomMessageBoxResult.LeftButton)
            {
                ShowUserLikesRateDialog();
            }
            else
            {
                ShowUserDislikesDialog();
            }
        }
    }
}
