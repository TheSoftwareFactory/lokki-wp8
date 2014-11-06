/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using FSecure.Utils;
using Lokki.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Lokki.Controls
{
    class PlaceViewModel : NotifiedPropertyContainer, IDisposable
    {
        public Place Place { get; private set; }

        PlaceViewModel(Place place)
        {
            this.Place = place;
            this.Place.PropertyChanged += Place_PropertyChanged;
        }

        void Place_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Notify(e.PropertyName);
        }

        public string Name
        {
            get
            {
                return Place.Name;
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
        ~PlaceViewModel()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (this.Place!= null)
                    {
                        this.Place.PropertyChanged -= Place_PropertyChanged;
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
