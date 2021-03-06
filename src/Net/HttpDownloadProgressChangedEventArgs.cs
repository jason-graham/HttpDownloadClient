﻿//---------------------------------------------------------------------------- 
//
//  Copyright (C) Jason Graham.  All rights reserved.
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
// 
// History
//  07/19/13    Created 
//
//---------------------------------------------------------------------------

namespace System.Net
{
    /// <summary>
    /// Event data for when download progress changes.
    /// </summary>
    public sealed class HttpDownloadProgressChangedEventArgs : ProgressChangedEventArgs
    {
        #region Properties
        /// <summary>
        /// Get the total number of bytes downloaded.
        /// </summary>
        public long? TotalDownloaded
        {
            get
            {
                return TotalCompleted;
            }
            private set
            {
                TotalCompleted = value;
            }
        }

        /// <summary>
        /// Get the total number of bytes to download.
        /// </summary>
        public long? TotalToDownload
        {
            get
            {
                return TotalToComplete;
            }
            private set
            {
                TotalToComplete = value;
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes with the total downloaded and the total to download.
        /// </summary>
        /// <param name="totalDownloaded">The total number of bytes downloaded.</param>
        /// <param name="totalToDownload">The total number of bytes to download.</param>
        public HttpDownloadProgressChangedEventArgs(long? totalDownloaded, long? totalToDownload)
            : base(totalDownloaded, totalToDownload)
        {

        }
        #endregion
    }
}
