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
using System.Collections.ObjectModel;
using Lokki.Settings;
using FSecure.Utils;
using FSecure.Lokki.ServerAPI;
using FSecure.Lokki.Resources;
using System.Collections.Specialized;
using FSecure.Logging;

namespace FSecure.Lokki.Controls
{

    public class PersonSelectedEventArgs : EventArgs
    {
        public Person Person{ get; set; }
    }


    public partial class MyPeopleList : UserControl
    {

        public event EventHandler<PersonSelectedEventArgs> PersonSelected;

        ObservableCollection<PersonViewModel> Model = new ObservableCollection<PersonViewModel>();

        public MyPeopleList()
        {
            InitializeComponent();

            this.Loaded += MyPeopleList_Loaded;
            this.Unloaded += MyPeopleList_Unloaded;
        }

        void MyPeopleList_Unloaded(object sender, RoutedEventArgs e)
        {
            PeopleList.SelectionChanged -= PeopleList_SelectionChanged;
            SettingsManager.People.CollectionChanged -= People_CollectionChanged;
            foreach (PersonViewModel model in Model)
            {
                model.Person.PropertyChanged -= person_PropertyChanged;
                model.Dispose();
            }
        }

        private void RefreshModel()
        {
            Model.Clear();

            foreach (Person person in SettingsManager.People)
            {
                if (person.IsCurrent) continue;

                person.PropertyChanged += person_PropertyChanged;
                if (!person.CanSeeMe) continue;

                Model.Add(new PersonViewModel(person));
            }
        }

        private void DisallowPerson(Person person)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                SystemTrayProgressIndicator.TaskCount++;
                try
                {
                    if (!person.CanSeeMe)
                    {
                        var resp = await ServerAPIManager.Instance.DisallowContactToSeeMe(person);
                        if (resp.IsSuccessful)
                        {
                            ApplicationStates.IsDashboardRequestNeeded = true;
                            SettingsManager.SavePeople();
                        }
                        else
                        {
                            MessageBox.Show(Localized.ApiError);
                            RefreshModel();
                        }
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
            });
        }

        void person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CanSeeMe")
            {
                var person = sender as Person;
                if (person.CanSeeMe)
                {
                    FSLog.Info("Adding");
                    bool found = false;
                    foreach (PersonViewModel m in Model)
                    {
                        if (m.Person == person)
                        {
                            found = true;
                            break;
                        }
                    }

                    // Not found, add
                    if (!found)
                    {
                        Model.Add(new PersonViewModel(person));
                    }
                }
                
                else
                {
                    FSLog.Info("Removing");

                    PersonViewModel search = null;
                    foreach (PersonViewModel m in Model)
                    {
                        if (m.Person == person)
                        {
                            search = m;
                            break;
                        }
                    }

                    if (search != null)
                    { 
                        Model.Remove(search);
                        DisallowPerson(search.Person);
                    }
                }
                     
            }
        }

        void MyPeopleList_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshModel();

            PeopleList.ItemsSource = Model;

            PeopleList.SelectionChanged += PeopleList_SelectionChanged;
            SettingsManager.People.CollectionChanged += People_CollectionChanged;
        }
         
         
        /// <summary>
        /// Detect new user invites.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void People_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                RefreshModel();
            }
        }

        void PeopleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeopleList.SelectedItem == null) return;

            foreach (var item in e.AddedItems)
            {
                // Maddness
                if (item == null) continue;

                // Value updated by checkbox
                var person = ((PersonViewModel)item).Person;

                if (PersonSelected != null)
                {
                    PersonSelected(this, new PersonSelectedEventArgs { Person = person });
                }

            }

            PeopleList.SelectedItem = null;
        }
    }
}
