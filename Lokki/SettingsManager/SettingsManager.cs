/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
///
/// Application specific settings

using System;
using System.IO.IsolatedStorage;

using Windows.Phone.ApplicationModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Devices.Geolocation;

using FSecure.Utils.ExtensionMethods;
using FSecure.Logging;
using System.Runtime.Serialization;

namespace Lokki.Settings
{
    /// <summary>
    /// Arguments for change events
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {

        /// <summary>
        /// SettingsManager.SettingItem, defines what setting item was changed
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Object, new value of the setting
        /// </summary>
        public object Value { get; set; }
    }

    public struct Geolocation
    {
        // NOTE: Private setters as hash should never change for object if fields mutate.
        public double Latitude{get; private set;}
        public double Longitude{get; private set;}
        public double Accuracy{get; private set;}
        public DateTimeOffset Time{get; private set;}

        public Geolocation(Geoposition position) 
            : this()
        {
            this.Latitude = position.Coordinate.Latitude;
            this.Longitude = position.Coordinate.Longitude;
            this.Accuracy = position.Coordinate.Accuracy;
            this.Time = position.Coordinate.Timestamp;
        }

        public Geolocation(double lat, double lon, double acc, DateTimeOffset time)
            : this()
        {
            this.Latitude = lat;
            this.Longitude = lon;
            this.Accuracy = acc;
            this.Time = time;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2231:OverloadOperatorEqualsOnOverridingValueTypeEquals")]
        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if ( !(obj is Geolocation) ) return false;

            var geo = (Geolocation)obj;
            return Latitude == geo.Latitude 
                && Longitude == geo.Longitude 
                && Accuracy == geo.Accuracy
                && Time == geo.Time;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            hash += (int)this.Latitude;
            hash *= 251; // prime number
            hash += (int)this.Longitude;
            hash *= 251;
            hash += (int)this.Accuracy;
            hash *= 251;
            hash += Time.GetHashCode();
            return hash;
        }
    }

    public class Place : FSecure.Utils.NotifiedPropertyContainer
    {
        // Empty constructor for serialization
        public Place()
        {

        }

        public Place(string id, string name, double lat, double lon, double radius)
        {
            this.Id = id;
            this.Name = name;
            this.Latitude = lat;
            this.Longitude = lon;
            this.Radius = radius;
        }

        public string Id
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public string Name
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public string Image
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public double Latitude
        {
            get { return GetValue<double>(); }
            set { SetValue(value); }
        }

        public double Longitude
        {
            get { return GetValue<double>(); }
            set { SetValue(value); }
        }

        public double Radius
        {
            get { return GetValue<double>(); }
            set { SetValue(value); }
        }

        // Run-time list of people at this place
        [System.Xml.Serialization.XmlIgnore]
        [IgnoreDataMember]
        public ObservableCollection<Person> People = new ObservableCollection<Person>();

    }

    public class Person : FSecure.Utils.NotifiedPropertyContainer
    {
        public Person()
        {
            IsShownOnMap = true;
            IsVisible = true;
            ICanSee = false;
        }

        public bool IsCurrent
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public string BackendId
        {
            get { 
                var v = GetValue<string>();
                if (v == null) return "";
                return v;
            }
            set { SetValue(value); }
        }

        public string Email
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public string Name
        {
            get { 
                var v = GetValue<string>();
                if (string.IsNullOrWhiteSpace(v))
                {
                    return Email;
                }
                return v;
            }
            set { SetValue(value); }
        }

        public Geolocation Position
        {
            get { return GetValue<Geolocation>(); }
            set { SetValue(value); }
        }
        
        public DateTime LastSeen
        {
            get {
                return new DateTime(Position.Time.Ticks);
            }
        }

        public bool CanSeeMe
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public bool ICanSee
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public bool IsShownOnMap
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public bool IsVisible
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        /** Copy the values from another object */
        public void Copy(Person another)
        {
            if (this == another) return;

            this.Fields.Clear();
            foreach (var key in another.Fields.Keys)
            {
                this.Fields[key] = another.Fields[key];
            }
        }
         
    }

    public enum MapMode {
        Default,
        Road,
        Aerial,
        Hybrid,
        Terrain
    }

    /// <summary>
    /// Handles the settings values in IsolatedStorageSettings. Simple values are saved immediately but bigger values like list of visited pages
    /// should contain their own saving method.
    /// </summary>
    public sealed class SettingsManager
    {
        /// <summary>
        /// This is so that we can simulate kid's corner in TA.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2211:NonConstantFieldsShouldNotBeVisible")]
        public static ApplicationProfileModes ApplicationProfileMode = ApplicationProfile.Modes;

        /// <summary>
        /// CA1053: Static holder types should not have constructors
        /// </summary>
        private SettingsManager()
        {

        }

        static SettingsManager()
        {
            if (Places == null)
            {
                Places = new ObservableCollection<Place>();
            }

            if (People == null)
            {
                People = new ObservableCollection<Person>();
            }
            
            if(CurrentUser == null) 
            {
                CurrentUser = new Person
                {
                    BackendId = "",
                    Email = "",
                    IsCurrent = true,
                    Position = new Geolocation(),
                    ICanSee = true
                };
            }
        }

        /// <summary>
        /// This event is used to notify listener delegates when a setting item is changed.
        /// If any setting item is changed, the listener will be notified.
        /// The event is called in the thread causing the change thus any UI actions must
        /// be dispatched to UI thread.
        /// </summary>
        public static event EventHandler<SettingsChangedEventArgs> SettingChanged;

        #region Settings


        public static ObservableCollection<Place> Places
        {
            get { return GetValue<ObservableCollection<Place>>(); }
            set { SetValue(value); }
        }

        public static ObservableCollection<Person> People
        {
            get { return GetValue<ObservableCollection<Person>>(); }
            set { SetValue(value); }
        }

        public static Person CurrentUser
        {
            get
            {
                foreach (Person p in People)
                {
                    if (p.IsCurrent)
                    {
                        return p;
                    }
                }

                return null;
            }
            set
            {

                Person current = null;
                foreach (var p in People)
                {
                    if (p.IsCurrent)
                    {
                        current = p;
                        break;
                    }
                }

                if (current == null)
                {
                    People.Add(value);
                }
                else
                {
                    current.Copy(value);
                }

                SavePeople();
            }
        }

        public static void SavePeople()
        {
            People = People;
        }

        public static bool IsLocationUploadEnabled
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        /** Authentication token to backend */
        public static string AuthToken
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public static string NotificationChannelUriString
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public static string NotificationMessageString
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public static string DeviceId
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }

        public static bool IsWelcomeBubbleShown
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static bool IsNewPeopleTutorialShown
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static bool IsAddFriendTutorialShown
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static bool IsPrivacyPolicyAccepted
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static bool IsNotificationUrlRegistered
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static MapMode CartographicMode
        {
            get { return GetValue<MapMode>(); }
            set { SetValue(value); }
        }

        public static bool AppRaterDoYouLikeQuestionShown
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static bool AppRaterShown
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }

        public static DateTimeOffset AppRaterQueryDate
        {
            get { return GetValue<DateTimeOffset>(); }
            set { SetValue(value); }
        }
        
        public static int AppStartCount
        {
            get { return GetValue<int>(); }
            set { SetValue(value); }
        }

        /// <summary>
        /// Initializes the default values for settings which do not exist. There's no need to call this more than once
        /// when app is started but it doesn't do any harm.
        /// </summary>
        public static void Initialize()
        {

        }

        #endregion

        #region internals

        /// <summary>
        /// We must use this object to lock saving settings
        /// </summary>
        public static readonly Mutex ThreadLocker = new Mutex(false, "f-secure.settings");

        private static void WithLocking(Action action)
        {
            // http://stackoverflow.com/questions/15456986/how-to-gracefully-get-out-of-abandonedmutexexception
            // "if you can assure the integrity of the data structures protected by the mutex you can simply ignore the exception and continue executing your application normally."
            // The bg agent can terminate and the mutex is not released properly
            try
            {
                ThreadLocker.WaitOne();
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
                action();
            }
            finally
            {
                ThreadLocker.ReleaseMutex();
            }
        }

        /// <summary>
        /// This is the place where settings are written and read from.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly IsolatedStorageSettings Store = IsolatedStorageSettings.ApplicationSettings;

        /// <summary>
        /// Checks if the given setting value already exists.
        /// </summary>
        /// <param name="item">SettingItem item, the settingitem which is to be checked.</param>
        /// <returns>True, if value already exists in isolatedStorage.
        /// False otherwise</returns>
        private static bool Exists(string key)
        {
            bool exists = false;
            WithLocking(() =>
            {
                exists = Store.Contains(key);
            });

            return exists;
        }

        /// <summary>
        /// Reads the value of SettingItem from IsolatedStorage. 
        /// If value does not exists, returns default value of type(null).
        /// </summary>
        /// <param name="item">Enum SettingItem, defines what setting item is read from settings</param>
        /// <typeparam name="T">Type of the object</typeparam>
        /// <returns>object, the value of given setting</returns>
        public static T GetValue<T>([CallerMemberName] string key = "")
        {
            T value = default(T);

            WithLocking(() =>
            {
                if (!Store.TryGetValue<T>(key, out value))
                {
                    value = default(T);
                }
            });
            return value;
        }

        /// <summary>
        /// Sets one value to IsolatedStorage and saves it immediately.
        /// </summary>
        /// <param name="item">Defines which setting item is set</param>
        /// <param name="value">The value of the setting item</param>
        public static void SetValue(object value, [CallerMemberName] string key = "")
        {
            WithLocking(() =>
            {
                Store[key] = value;
                Store.Save();

                OnSettingChanged(key, value);
            });
        }

        /// <summary>
        /// Saves the isolated storage.
        /// </summary>
        public static void Save()
        {
            WithLocking(() =>
            {
                Store.Save();
            });
        }

        /// <summary>
        /// This method notifies all SettingChanged delegates. Should be called when any value changes on settings.
        /// </summary>
        /// <param name="item">SettingItem enum, tells what item is changed </param>
        /// <param name="value">New value of the changed setting item</param>
        private static void OnSettingChanged(string key, Object value)
        {
            if (SettingChanged != null)
            {
                var args = new SettingsChangedEventArgs { Key = key, Value = value };
                SettingChanged(null, args);
            }
        }

        #endregion
    }
}
