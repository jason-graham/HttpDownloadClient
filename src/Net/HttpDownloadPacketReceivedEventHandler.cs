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

namespace System.Net
{
    /// <summary>
    /// Event data for when a packet is received.
    /// </summary>
    public sealed class HttpDownloadPacketReceivedEventHandler : EventArgs
    {
        /// <summary>
        /// Get the packet byte length.
        /// </summary>
        public long PacketLength { get; private set; }

        /// <summary>
        /// Initialize with the packet byte length.
        /// </summary>
        /// <param name="packetLength">The number of bytes received.</param>
        public HttpDownloadPacketReceivedEventHandler(long packetLength)
        {
            PacketLength = packetLength;
        }
    }
}
