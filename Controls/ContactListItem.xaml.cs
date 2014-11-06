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
using FSecure.Utils;

namespace FSecure.Lokki.Controls
{
    public class ContactListItemModel : NotifiedPropertyContainer
    {
        /** The name of the contact */
        public string Name { get; set; }

        /** Email address of contact */
        public string Email { get; set; }

        /** True if contact is selected */
        public bool IsSelected
        {
            get { return GetValue<bool>(); }
            set { SetValue(value); }
        }
    }

    public partial class ContactListItem : UserControl
    {
        public ContactListItem()
        {
            InitializeComponent();
        }
    }
}
