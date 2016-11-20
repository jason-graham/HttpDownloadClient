//---------------------------------------------------------------------------- 
//
//  Copyright (C) CSharp Labs.  All rights reserved.
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

namespace System
{
    /// <summary>
    /// Event data for when progress changes.
    /// </summary>
    public class ProgressChangedEventArgs : EventArgs
    {
        #region Properties
        /// <summary>
        /// Gets the total completed.
        /// </summary>
        public long? TotalCompleted
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets the total.
        /// </summary>
        public long? TotalToComplete
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets the remaining.
        /// </summary>
        public long? TotalRemaining
        {
            get
            {
                return TotalToComplete - TotalCompleted;
            }
        }

        /// <summary>
        /// Get the percent complete.
        /// </summary>
        public int? PercentComplete
        {
            get
            {
                //check if a suitable percent can be created
                if (TotalToComplete <= 0)
                    return null;

                //check if value completed and total to complete match
                if (TotalCompleted == TotalToComplete)
                    return 100;

                return (int)(TotalCompleted * 100 / TotalToComplete); //return a percent completion
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes with the total completed and the total to complete.
        /// </summary>
        /// <param name="totalCompleted">The total number completed.</param>
        /// <param name="totalToComplete">The total number to complete.</param>
        public ProgressChangedEventArgs(long? totalCompleted, long? totalToComplete)
        {
            if (totalCompleted.HasValue && totalCompleted.Value < 0)
                throw new ArgumentException("The total completed cannot be less than 0.", "totalCompleted");

            if (totalToComplete.HasValue && totalToComplete.Value < 0)
                throw new ArgumentException("The total to complete cannot be less than 0.", "totalToComplete");

            TotalCompleted = totalCompleted;
            TotalToComplete = totalToComplete;
        }
        #endregion
    }
}
