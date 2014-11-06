/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
///
/// Page for choosing one or multiple contacts.

using FSecure.Logging;
using FSecure.Lokki.Controls;
using FSecure.Lokki.Resources;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.UserData;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Collections.Generic;
using Lokki.Settings;
using System.Globalization;
using FSecure.Utils;
using System.Windows.Threading;
using FSecure.Lokki.ServerAPI;

namespace lokki_wp8.Pages
{
    public partial class ContactsPage : PhoneApplicationPage
    {
        
        private bool SearchTextBoxHasFocus;

        private HashSet<string> SelectedEmails = new HashSet<string>();

        private ObservableCollection<ContactListItemModel> Model;
        
        public ContactsPage()
        {
            InitializeComponent();

            Model = new ObservableCollection<ContactListItemModel>();

            ApplicationBar = new ApplicationBar();
            ApplicationBar.BackgroundColor = (Color)Resources["C6"];
            ApplicationBar.ForegroundColor = (Color)Resources["White"];

            ApplicationBarIconButton button = new ApplicationBarIconButton(new Uri("/Assets/AppBarEmail.png", UriKind.Relative));
            button.Text = Localized.Invite;
            button.Click += button_Click;            
            ApplicationBar.Buttons.Add(button);

            ApplicationBar.IsMenuEnabled = false;
            ApplicationBar.IsVisible = true;
        }

        async void button_Click(object sender, EventArgs e)
        {            
            SystemTrayProgressIndicator.TaskCount++;

            var resp = await ServerAPIManager.Instance.AllowContactToSeeMe(SelectedEmails.ToList<string>());
            if (resp.IsSuccessful)
            {
                NavigationService.GoBack();
            }
            else
            {
                FSLog.Error("Failed to allow contacts");
                MessageBox.Show(Localized.ApiError);
            }

            SystemTrayProgressIndicator.TaskCount--;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ContactsList.SelectionChanged -= ContactsList_SelectionChanged;
        }

        void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContactsList.SelectedItem == null) return;

            foreach (var item in e.AddedItems)
            {
                // Maddness
                if (item == null) continue;

                // Value updated by checkbox
                var model = ((ContactListItemModel)item);
                var selected = ((ContactListItemModel)item).IsSelected;

                if (selected)
                {
                    SelectedEmails.Add(model.Email);
                }
                else
                {
                    SelectedEmails.Remove(model.Email);
                }
            }

            foreach (var item in e.RemovedItems)
            {
                // Maddness
                if (item == null) continue;

                ((ContactListItemModel)item).IsSelected = false;
            }

            ContactsList.SelectedItem = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ContactsList.SelectionChanged += ContactsList_SelectionChanged;
            
            var colors = ((ResourceDictionary)ringo_wp8.App.Current.Resources["Colors"]);
            SystemTray.BackgroundColor = (Color)colors["Main"];

            PopulateContactsList();
        }

        void Contacts_SearchCompleted(object sender, ContactsSearchEventArgs e)
        {
            FSLog.Info();

            ((Contacts)sender).SearchCompleted -= Contacts_SearchCompleted;

            if (!SearchTextBox.Text.Equals((string)e.State, StringComparison.Ordinal))
            {
                FSLog.Info("Old state");
                return;
            }

            var searchText = SearchTextBox.Text;

            foreach (Contact con in (from Contact con in e.Results
                                  orderby con.DisplayName ascending
                                  select con))
            {

                foreach (var email in con.EmailAddresses)
                {

                    bool selected = SelectedEmails.Contains(email.EmailAddress);
                    if (!selected)
                    {
                        if (!(searchText.Length == 0
                            || con.DisplayName.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) > -1
                            || email.EmailAddress.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) > -1))
                        {
                            continue;
                        }
                    }
                    
                    // Filter out people already invited
                    bool invited = false;
                    foreach (var person in SettingsManager.People)
                    {
                        if (person.Email != null 
                            && person.Email.Equals(email.EmailAddress, StringComparison.InvariantCultureIgnoreCase))
                        {
                            invited = true;
                        }
                    }

                    if (invited) continue;

                    var model = new ContactListItemModel();
                    model.Name = con.DisplayName;
                    model.Email = email.EmailAddress;
                    model.IsSelected = selected;

                    Model.Add(model);
                }
            }

            foreach (Contact con in e.Results)
            {
                try
                {
                    /*
                    BitmapImage img = new BitmapImage();
                    img.SetSource(con.GetPicture());
                    ((ImageBrush)this.AvatarArc.Fill).ImageSource = img;
                     * */

                    // Ignore contacts without emails
                    var emails = con.EmailAddresses;
                    if(emails == null) continue;

                }
                catch (System.Exception)
                {
                    //No results
                }
            }
             
        }

        private void PopulateContactsList()
        {
            ContactsList.ItemsSource = Model;
            Search();
        }

        private void Search()
        {
            Model.Clear();

            Contacts cons = new Contacts();

            //Identify the method that runs after the asynchronous search completes.
            cons.SearchCompleted += new EventHandler<ContactsSearchEventArgs>(Contacts_SearchCompleted);

            //Start the asynchronous search.
            cons.SearchAsync(String.Empty, FilterKind.None, SearchTextBox.Text);
        }

        DispatcherTimer SearchTimer;

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (SearchTimer != null)
            {
                SearchTimer.Stop();
            }

            SearchTimer = FSCall.Delayed(() => Search(), 500);            
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchTextBoxHasFocus = true;
            UpdateHintTextVisibility();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchTextBoxHasFocus = false;
            UpdateHintTextVisibility();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateHintTextVisibility();
        }

        private void UpdateHintTextVisibility()
        {
            HintText.Visibility = SearchTextBox.Text.Length > 0 || SearchTextBoxHasFocus ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}