/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Utils
{
    public class NotifiedPropertyContainer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected Dictionary<string, object> Fields = new Dictionary<string,object>();

        protected void Notify(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// Use this to set the value if you want to manage the private fields yourself
        /// </summary>
        /// <typeparam name="T">Type of the value to set</typeparam>
        /// <param name="field">Reference to the field to be updated</param>
        /// <param name="value">The new value for the field.</param>
        /// <param name="name">Name of value for notifications. Defaults to the caller property</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
        protected void SetValue<T>(ref T field, T value, [CallerMemberName] string name = "") where T: class
        {
            if (field == value) return;
            if (!(field != null && field.Equals(value)))
            {
                field = value;
                Notify(name);
            }
        }

        /// <summary>
        /// Use this to set the value if you wish to let NotifyPropertyChanged handle the private fields
        /// in internal dictionary
        /// </summary>
        /// <param name="value">The new value for the field.</param>
        /// <param name="name">Name of value for notifications. Defaults to the caller property</param>
        protected void SetValue(object value, [CallerMemberName] string name = "")
        {
            object field = null;
            Fields.TryGetValue(name, out field);

            if (field == value ) return;
            if (!(field != null && field.Equals(value)))
            {
                Fields[name] = value;
                Notify(name);
            }
        }

        /// <summary>
        /// Get value from internal dictionary
        /// </summary>
        /// <typeparam name="T">Type of the value to get</typeparam>
        /// <param name="name">Name of value for notifications. Defaults to the caller property</param>
        /// <returns></returns>
        protected T GetValue<T>([CallerMemberName] string name = "")
        {
            object value = default(T);
            if (Fields.TryGetValue(name, out value))
            {
                return (T)value;
            };

            return default(T);
        }
        
    }
}
