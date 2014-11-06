/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
///
/// Helper class for triggering multiple message boxes at the same time.
/// Stores the message boxes in a FIFO queue. When a message is closed,
/// next message is displayed from queue.


using System.Collections.Generic;
using System.Windows.Media;

#if UNITTEST
using FSecure.Mocks.System.Windows;
using FSecure.Mocks.Microsoft.Phone.Controls;
#else
using System.Windows;
using Microsoft.Phone.Controls;
#endif

namespace FSecure.Utils
{
    public class MessageBoxQueue : Queue<CustomMessageBox>
    {
        private static MessageBoxQueue _instance;
        public static MessageBoxQueue Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MessageBoxQueue();
                }
                return _instance;
            }
        }
         
        /// <summary>
        /// Allows closing message box from TA.
        /// </summary>
        public static CustomMessageBox CurrentMessageBox
        {
            get
            {
                if (Instance.Count == 0) return null;

                return Instance.Peek();
            }
        }

        private MessageBoxQueue() { }

#if UNITTEST
#else
        private ResourceDictionary Colors
        {
            get
            {
                return Application.Current.Resources["Colors"] as ResourceDictionary;
            }
        }
#endif
        public CustomMessageBox Show(string caption = "",
            string message = "",
            object content = null,
            object leftButtonContent = null,
            object rightButtonContent = null)
        {
            var dialog = new CustomMessageBox()
            {
                Caption = caption,
                Message = message,
                Content = content,
                LeftButtonContent = leftButtonContent,
                RightButtonContent = rightButtonContent,
#if UNITTEST
#else

                Background = (SolidColorBrush)(Colors["BRUSH_Main"]),
                Foreground = (SolidColorBrush)(Colors["BRUSH_C8"])
#endif
            };
            this.Enqueue(dialog);
#if UNITTEST
#else
            dialog.Style = (Application.Current.Resources["CustomMessageBox"] as ResourceDictionary)["CustomMessageBoxStyle"] as Style;
#endif
            // Using Unloaded event to avoid double dimmed background when
            // next dialog is shown. With Unload the previous dialog is 
            // removed from visual tree before next is shown.
            dialog.Unloaded += dialog_Unloaded;

            // Show if first and only one, otherwise shown after current closed.
            if (Count == 1)
            {
                dialog.Show();
            }

            return dialog;
        }

        void dialog_Unloaded(object sender, RoutedEventArgs e)
        {
            (sender as CustomMessageBox).Unloaded -= dialog_Unloaded;

            this.Dequeue();

            if (this.Count > 0)
            {
                this.Peek().Show();
            }
        }
    }
}
