/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FSecure.Utils
{
    public class FSAsyncResult : FSDisposable, IAsyncResult
    {
        public FSAsyncResult()
        {
        }

        public void Wait()
        {
            if (IsCompleted) return;

            AsyncWaitHandle.WaitOne();
        }

        #region IAsyncResult

        /// <summary>
        /// Event triggered once request has completed
        /// </summary>
        private ManualResetEvent CompletionEvent = new ManualResetEvent(false);

        private bool _IsCompleted;

        public object AsyncState
        {
            get
            {
                return _IsCompleted;
            }
        }

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { return CompletionEvent; }
        }

        public bool CompletedSynchronously
        {
            get { return false; } // never 
        }

        /// <summary>
        /// Return true if completion event is set
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                return _IsCompleted;
            }
            set
            {
                _IsCompleted = value;
                if (value)
                {
                    CompletionEvent.Set();
                }
            }
        }

        #endregion

        protected override IEnumerable<IDisposable> GetDisposables()
        {
            yield return CompletionEvent;
        }
    }
}
