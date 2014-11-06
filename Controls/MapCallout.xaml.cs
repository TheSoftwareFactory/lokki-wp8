/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using FSecure.Utils;
using FSecure.Lokki.Resources;
using FSecure.Utils.ExtensionMethods;
using Lokki.Settings;
using FSecure.Logging;
using Microsoft.Phone.UserData;
using Microsoft.Phone.Tasks;

namespace FSecure.Lokki.Controls
{

    public partial class MapCallout : UserControl
    {
        private Contact Contact = null;

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
          "Model",
          typeof(PersonViewModel),
          typeof(MapCallout),
          new PropertyMetadata(null, new PropertyChangedCallback(OnModelChanged))
        );

        /// <summary>
        /// Is checked or not
        /// </summary>
        public PersonViewModel Model
        {
            get { return (PersonViewModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        private static void OnModelChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var self = sender as MapCallout;
            self.DataContext = self.Model;
            self.RefreshButtons();
        }

        public MapCallout()
        {
            InitializeComponent();

            VisualStateManager.GoToState(this, "Hidden", false);
        }

        public bool IsVisible
        {
            get
            {
                return this.VisualStateGroup.CurrentState.Name != "Hidden";
            }
            set
            {
                VisualStateManager.GoToState(this, value ? "Visible" : "Hidden", true);
            }
        }

        public void SearchContact(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                FSLog.Error("Invalid email");
                return;
            }

            Contacts cons = new Contacts();
            cons.SearchCompleted += new EventHandler<ContactsSearchEventArgs>(Email_SearchCompleted);
            cons.SearchAsync(email, FilterKind.EmailAddress, this);
        }

        /** Called when email search completes */
        void Email_SearchCompleted(object sender, ContactsSearchEventArgs e)
        {
            ((Contacts)sender).SearchCompleted -= Email_SearchCompleted;

            if (e.Results.Count() > 0)
            {
                foreach (Contact con in e.Results)
                {
                    if (con.PhoneNumbers.Count() > 0)
                    {
                        CallButton.Opacity = 1;
                        MessageButton.Opacity = 1;
                        this.Contact = con;
                        break;
                    }
                }
            }

            if (this.Contact == null)
            {
                FSLog.Warning("Contact for calling not found");
            }
        }

        private void RefreshButtons()
        {
            // Show disabled when we don't have the phonenumber
            CallButton.Opacity = 0.2;
            MessageButton.Opacity = 0.2;

            this.Contact = null;
            SearchContact((this.Model as PersonViewModel).Person.Email);
        }

        private void CallButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FSLog.Info();

            if (this.Contact == null)
            {
                FSLog.Info("Contact not found");
                return;
            }

            var task = new PhoneCallTask();
            task.DisplayName = Contact.DisplayName;
            task.PhoneNumber = Contact.PhoneNumbers.First().PhoneNumber;
            task.Show();
        }

        private void MessageButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            /*
            FSLog.Info();

            var task = new EmailComposeTask();
            task.To = (this.Model as PersonViewModel).Person.Email;
            task.Show();
             * */

            FSLog.Info();

            if (this.Contact == null)
            {
                FSLog.Info("Contact not found");
                return;
            }

            SmsComposeTask smstask = new SmsComposeTask();
            smstask.To = this.Contact.PhoneNumbers.First().PhoneNumber;
            smstask.Show();

        }
    }
}
