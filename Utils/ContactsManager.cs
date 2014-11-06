/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Logging;
using FSecure.Lokki.Resources;
using FSecure.Lokki.ServerAPI;
using FSecure.Utils;
using FSecure.Utils.ExtensionMethods;
using Microsoft.Phone.Tasks;
using Microsoft.Phone.UserData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FSecure.Lokki.Utils
{

    public class ContactData
    {
        public BitmapImage Photo { get; set; }
        //public System.IO.Stream PhotoStream { get; set; }
        public string DisplayName { get; set; }
        public bool IsFound { get; set; }
    }

    public class ContactsManager
    {

        internal sealed class SearchContext : FSAsyncResult
        {
            public ContactsSearchEventArgs Result { get; set; }

            public SearchContext()
            {
            }
        }

        #region Properties

        public event EventHandler UserInvited;

        private static ContactsManager _Instance;
        public static ContactsManager Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new ContactsManager();
                }
                return _Instance;
            }
        }

        #endregion

        public Task<ContactsSearchEventArgs> SearchContactWithEmail(string email)
        {
            return Task.Run<ContactsSearchEventArgs>(() =>
            {
                SearchContext context = new SearchContext();

                Contacts cons = new Contacts();
                cons.SearchCompleted += Contact_SearchCompleted;
                cons.SearchAsync(email, FilterKind.EmailAddress, context);

                context.Wait();

                return context.Result;
            });
        }

        /// <summary>
        /// Called when email search completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Contact_SearchCompleted(object sender, ContactsSearchEventArgs e)
        {
            ((Contacts)sender).SearchCompleted -= Contact_SearchCompleted;

            var context = e.State as SearchContext;
            context.Result = e;
            context.IsCompleted = true;
        }

        /// <summary>
        /// Display chooser for invitation
        /// </summary>
        public void ShowInviteUserChooser()
        {
            var chooser = new EmailAddressChooserTask();
            chooser.Completed += InvitationContactChooser_Completed;
            chooser.Show();
        }

        /// <summary>
        /// Called when chooser for inviting person completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void InvitationContactChooser_Completed(object sender, EmailResult e)
        {
            FSLog.Info(e.TaskResult);

            (sender as EmailAddressChooserTask).Completed -= InvitationContactChooser_Completed;

            if (e.TaskResult == TaskResult.OK)
            {
                ApplicationStates.InvitationEMailStarted = true;
                ApplicationStates.InvitationEMail = e.Email;
                ApplicationStates.InvitationDisplayName = e.DisplayName;
            }
        }

        /// <summary>
        /// Start inviting another user with confirmation dialog.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task InviteUserAsync(string email)
        {
            SystemTrayProgressIndicator.TaskCount++;

            //Email = e.Email;
            var reply = MessageBox.Show(
                Localized.InvitingConfirmMessage.FormatLocalized(email),
                Localized.Inviting,
                MessageBoxButton.OKCancel);

            if (reply != MessageBoxResult.OK)
            {
                FSLog.Info("User declined");
                return;
            }

            var resp = await ServerAPIManager.Instance.AllowContactToSeeMe(new List<string> { email });
            if (resp.IsSuccessful)
            {
                // Display tutorial popup in map page
                if (UserInvited != null)
                {
                    UserInvited(this, new EventArgs());
                }
            }
            else
            {
                FSLog.Error("Request failed");
                MessageBox.Show(Localized.ApiError);
            }

            SystemTrayProgressIndicator.TaskCount--;
        }


        /// <summary>
        /// In-memory cache of photos
        /// </summary>
        Dictionary<string, ContactData> Contacts = new Dictionary<string, ContactData>();
        
        /// <summary>
        /// Search tasks for email to detect if there's search on-going already for the contact
        /// </summary>
        Dictionary<string, FSAsyncResult> Tasks = new Dictionary<string, FSAsyncResult>();

        public Task<ContactData> GetContactData(string email)
        {
            FSAsyncResult task;

            var started = DateTimeOffset.UtcNow;

            //FSLog.Debug("start:", email);

            lock (Tasks)
            {
                lock (Contacts)
                {
                    if (Contacts.ContainsKey(email))
                    {
                        return Task<ContactData>.Run( () => {
                            FSLog.Debug("end:", email, DateTimeOffset.Now - started);
                            return Contacts[email];
                        });
                    }
                }

                // See if search started for this email
                if (Tasks.ContainsKey(email))
                {
                    task = Tasks[email];
                }
                else // Not running already
                {
                    task = new FSAsyncResult();
                    Tasks[email] = task;

                    //Start the asynchronous search.
                    Contacts cons = new Contacts();

                    //Identify the method that runs after the asynchronous search completes.
                    cons.SearchCompleted += (object sender, ContactsSearchEventArgs e) =>
                    {
                        FSLog.Info("SearchCompleted:", email);

                        ContactData data = new ContactData();
                        data.DisplayName = email;

                        // Grab first match
                        foreach (Contact con in e.Results)
                        {
                            data.IsFound = true;

                            //No results
                            if (!string.IsNullOrWhiteSpace(con.DisplayName))
                            {
                                data.DisplayName = con.DisplayName;
                            }

                            System.IO.Stream stream = con.GetPicture();
                            if (stream != null)
                            {
                                BitmapImage img = new BitmapImage();
                                img.SetSource(stream);
                                data.Photo = img;
                                break;
                            }
                        }

                        lock (Contacts)
                        {
                            Contacts[email] = data;
                        }

                        task.IsCompleted = true;

                    };

                    cons.SearchAsync(email, FilterKind.EmailAddress, task);
                }
            }

            return Task<ContactData>.Run(() =>
            {
                //FSLog.Debug("wait:", email);
                task.Wait();

                lock (Tasks)
                {
                    Tasks.Remove(email);
                    task.Dispose();
                }

                lock (Contacts)
                {
                    //FSLog.Debug("end:", email, DateTimeOffset.Now - started);
                    return Contacts[email];
                }
            });
        }

    }
}
