/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSecure.Utils
{
    /// <summary>
    /// Utility for handling disposing
    /// </summary>
    public abstract class FSDisposable : IDisposable
    {
        abstract protected IEnumerable<IDisposable> GetDisposables();
         
        /// <summary>
        /// Required for disposing logic
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// Disposes native objects.
        /// </summary>
        ~FSDisposable()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    foreach (var disposable in GetDisposables())
                    {
                        if (disposable != null) 
                        {
                            disposable.Dispose();
                        }
                    }
                }
                // Free your own state (unmanaged objects).
                // Set large fields to null.
                IsDisposed = true;
            }
        }

        /// <summary>
        /// CA1001: Types that own disposable fields should be disposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
