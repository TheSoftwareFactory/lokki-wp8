/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Lokki.Settings;

using FSecure.Lokki.Controls;
using FSecure.Lokki.ServerAPI;
using System.Windows.Media;
using FSecure.Utils;
using System.Collections.ObjectModel;
using FSecure.Lokki;

namespace lokki_wp8.Pages
{
    public partial class MyPeoplePage : PhoneApplicationPage
    {
        ObservableCollection<PersonViewModel> Model = new ObservableCollection<PersonViewModel>();

        public MyPeoplePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PeopleList.SelectionChanged -= PeopleList_SelectionChanged;
            foreach (PersonViewModel model in Model)
            {
                model.Dispose();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Model.Clear();

            foreach (Person person in SettingsManager.People)
            {
                if (person.IsCurrent) continue;
                if (!person.CanSeeMe) continue;

                Model.Add(new PersonViewModel(person));
            }

            PeopleList.ItemsSource = Model;

            PeopleList.SelectionChanged += PeopleList_SelectionChanged;
        }

        async void PeopleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeopleList.SelectedItem == null) return;

            foreach (var item in e.AddedItems)
            {
                // Maddness
                if (item == null) continue;

                // Value updated by checkbox
                var person = ((PersonViewModel)item).Person;

                SystemTrayProgressIndicator.TaskCount++;
                try
                {
                    if (!person.CanSeeMe)
                    {
                        await ServerAPIManager.Instance.DisallowContactToSeeMe(person);
                        Model.Remove((PersonViewModel)item);
                        ApplicationStates.IsDashboardRequestNeeded = true;
                    }
                    /*
                    else
                    {
                        await ServerAPIManager.Instance.AllowContactToSeeMe(new List<string> { person.Email });
                    }
                    */
                }
                finally
                {
                    SystemTrayProgressIndicator.TaskCount--;
                }
            }

            PeopleList.SelectedItem = null;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            SettingsManager.SavePeople();
        }
    }
}