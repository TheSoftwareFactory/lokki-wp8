/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Logging;
using Lokki.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Lokki
{
    /// <summary>
    /// Class dedicated to putting people in places.
    /// 
    /// Observes SettingsManager.Places:
    ///     Observes SettingsManager.Places for new and deleted places.
    /// 
    /// Updates the People member of Place:
    ///     Observes SettingsManager.People for any new or removed persons and removes
    ///     or adds them to respective Place. Observes People's location and visibility 
    ///     and shows them in respective place list item.
    ///     
    /// </summary>
    public class PlacesManager
    {
        private static PlacesManager _Instance;
        public static PlacesManager Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new PlacesManager();
                }
                return _Instance;
            }
        }

        private PlacesManager()
        {

        }

        public void Initialize()
        {
            SettingsManager.Places.CollectionChanged += Places_CollectionChanged;
            SettingsManager.People.CollectionChanged += People_CollectionChanged;

            foreach (Person person in SettingsManager.People)
            {
                person.PropertyChanged += person_PropertyChanged;
                FindAndAddToPlaces(person);
            }
        }

        double DistanceBetween(Person p1, Place p2)
        {
            // TODO: allocation is slow, use static variables. Doesn't need to be thread safe.
            GeoCoordinate coord1 = new GeoCoordinate(p1.Position.Latitude, p1.Position.Longitude);
            GeoCoordinate coord2 = new GeoCoordinate(p2.Latitude, p2.Longitude);
            return coord1.GetDistanceTo(coord2);
        }

        private bool IsPersonAtPlace(Person person, Place place)
        {
            return DistanceBetween(person, place) <= place.Radius;
        }

        /// <summary>
        /// Find places for person
        /// </summary>
        /// <param name="person"></param>
        /// <returns></returns>
        void FindAndAddToPlaces(Person person)
        {
            foreach (Place place in SettingsManager.Places)
            {
                AddPersonToPlace(person, place);
            }
        }

        /// <summary>
        /// Remove person from all places
        /// </summary>
        /// <param name="person"></param>
        void FindAndRemoveFromPlaces(Person person)
        {
            foreach (Place place in SettingsManager.Places)
            {
                place.People.Remove(person);
            }
        }

        private static bool CanSeePerson(Person person)
        {
            return person.IsCurrent || (person.IsVisible && person.ICanSee && person.CanSeeMe);
        }

        /// <summary>
        /// Add person to place if there
        /// </summary>
        /// <param name="person"></param>
        /// <param name="listControl"></param>
        private void AddPersonToPlace(Person person, Place place)
        {
            // Don't show hidden people
            if (!CanSeePerson(person)) return;

            if (IsPersonAtPlace(person, place) && !place.People.Contains(person))
            {
                place.People.Add(person);
            }
        }

        void person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var person = sender as Person;
            if (e.PropertyName == "IsVisible" || e.PropertyName == "ICanSee" || e.PropertyName == "CanSeeMe")
            {
                // Remove people who don't want to be visible except current user
                if (!CanSeePerson(person))
                {
                    FindAndRemoveFromPlaces(person);
                }
                else
                {
                    FindAndAddToPlaces(person);
                }
            }
            // Update places for person
            else if (e.PropertyName == "Position")
            {
                FSLog.Info("Updating position");

                // Find places where the person is no more and remove from respective control
                foreach (Place place in SettingsManager.Places)
                {
                    if (!IsPersonAtPlace(person, place))
                    {
                        FSLog.Info("Removing from place");
                        place.People.Remove(person);
                    }
                }

                // Find if there are any new places.
                FindAndAddToPlaces(person);
            }
        }

        void Places_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Add people at this place
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Place place in e.NewItems)
                {
                    var people = SettingsManager.People;
                    foreach (Person person in people)
                    {
                        AddPersonToPlace(person, place);
                    }
                }
            }
        }

        void People_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (Person person in e.OldItems)
                {
                    person.PropertyChanged -= person_PropertyChanged;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Person person in e.NewItems)
                {
                    person.PropertyChanged += person_PropertyChanged;
                    FindAndAddToPlaces(person);
                }
            }
        }

    }
}
