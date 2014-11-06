/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using FSecure.Lokki.Resources;
using FSecure.Utils;
using Lokki.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FSecure.Utils.ExtensionMethods;
using FSecure.Logging;

namespace FSecure.Lokki.Controls
{
    /** This class is used as a common datamodel with controls displaying information about user.
     * 
     * Currently used by MapCallout and MyPeopleListItem.
     */
    public sealed class PersonViewModel: NotifiedPropertyContainer, IDisposable
    { 
        public PersonViewModel(Person person)
        {
            this.Person = person;
            this.Person.PropertyChanged += Person_PropertyChanged;
        }

        void Person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            Notify(e.PropertyName); 
            
            if (e.PropertyName.Equals("IsVisible"))
            {
                Notify("LastSeen");
            }
        }

        public Person Person { get; private set; }

        /** The name of the contact */
        public string Name
        {
            get
            {
                return Person.Name;
            }
        }

        /** Email address of contact */
        public string Email
        {
            get
            {
                return Person.Email;
            }
        }

        /** True if contact is selected */
        public bool CanSeeMe
        {
            get
            {
                return Person.CanSeeMe;
            }
            set
            {
                Person.CanSeeMe = value;
                SetValue(value);
            }
        }

        public bool IsShownOnMap
        {
            get
            {
                return Person.IsShownOnMap;
            }
            set
            {
                Person.IsShownOnMap = value;
                SetValue(value);
            }
        }

        public bool ICanSee
        {
            get
            {
                return Person.ICanSee;
            }
            set
            {
                Person.ICanSee = value;
                SetValue(value);

                Notify("IsVisible");
            }
        }

        public bool IsVisible
        {
            get
            {
                return Person.IsVisible && ( ICanSee || Person.IsCurrent);
            }
            set
            {
                Person.IsVisible = value;
                SetValue(value);
            }
        }

        public string LastSeen
        {
            get
            {
                if (!IsVisible)
                {
                    return Localized.Invisible;
                }

                var local = Person.LastSeen.ToLocalTime();
                var diff = DateTime.UtcNow - Person.LastSeen;
                if (diff.TotalHours <= 1)
                {
                    string timeString = local.ToShortTimeString();
                    long minutes = (long)Math.Max(0, diff.TotalMinutes);
                    timeString += Localized.MinutesAgo.FormatLocalized(minutes);
                    return timeString;
                }
                else
                {
                    return local.ToShortDateString() + " " + local.ToShortTimeString();
                }
            }
        }

        #region IDisposable

        /// <summary>
        /// Required for disposing logic
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// Disposes native objects.
        /// </summary>
        ~PersonViewModel()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (this.Person != null)
                    {
                        this.Person.PropertyChanged -= Person_PropertyChanged;
                    }
                }
                // Free your own state (unmanaged objects).
                // Set large fields to null.
                IsDisposed = true;
            }
        }

        /// <summary>
        /// CA1001: Types that own disposable fields should be disposable
        /// We must dispose CompletionEvent
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
