//---------------------------------------------------------------------------- 
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
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The download client allows downloading remote resources to a file or stream and
    /// supports resuming partially completed downloads, progress notification and cancellation.
    /// </summary>
    public sealed class HttpDownloadClient
    {
        #region Constants
        /// <summary>
        /// Defines the exception raised when attempting to download while the download is already in progress or already completed.
        /// </summary>
        private const string DOWNLOAD_STARTED_EXCEPTION = "HttpDownloadClient has already started.";
        /// <summary>
        /// Defines the exception raised when attempting to initialize while the download is already initialized or initializing.
        /// </summary>
        private const string DOWNLOAD_INITIALIZING_EXCEPTION = "HttpDownloadClient is already being initialized.";
        /// <summary>
        /// Defines the exception raised when attempting to access the TargetStream property in file mode. 
        /// </summary>
        private const string DOWNLOAD_STREAMMODE_EXCEPTION = "HttpDownloadClient is in Stream mode.";
        /// <summary>
        /// Defines the exception raised when attempting to access the TargetFile property in stream mode.
        /// </summary>
        private const string DOWNLOAD_FILEMODE_EXCEPTION = "HttpDownloadClient is in File mode.";
        #endregion

        #region HTTPDownloadState Enum
        /// <summary>
        /// Contains HttpDownloadClient state flags.
        /// </summary>
        [Flags]
        private enum HTTPDownloadState
        {
            None = 0,
            /// <summary>
            /// The download has started.
            /// </summary>
            DownloadStarted = 1,
            /// <summary>
            /// The download has completed.
            /// </summary>
            DownloadCompleted = 2,
            /// <summary>
            /// The download has failed.
            /// </summary>
            DownloadFailed = 4,
            /// <summary>
            /// The download is canceled.
            /// </summary>
            DownloadCanceled = 8,
            /// <summary>
            /// The download is initializing.
            /// </summary>
            DownloadInitializing = 16,
            /// <summary>
            /// The download is initialized.
            /// </summary>
            DownloadInitialized = 32,
        }
        #endregion

        #region Fields
        /// <summary>
        /// This is the synchronization point that prevents AsyncInitialize calls 
        /// from running concurrently, allows the AsyncDownload method to wait
        /// for a previously called AsyncInitialize method to complete and prevents the 
        /// AsyncInitialize method from running more than once.
        /// </summary>
        private int initializingSyncPoint;
        /// <summary>
        /// This is the synchronization point that prevents AsyncDownload method from running more than once.
        /// </summary>
        private int downloadingSyncPoint;
        /// <summary>
        /// Defines the current download state.
        /// </summary>
        private HTTPDownloadState DownloadState;
        /// <summary>
        /// Defines a CancellationToken to monitor.
        /// </summary>
        private CancellationToken cancellationToken;
        /// <summary>
        /// Defines the last progress update percent completion. This value can be
        /// null if TotalBytes is null or -1 if never used.
        /// </summary>
        private int? lastProgressPercent;
        #endregion

        #region Properties
        /// <summary>
        /// Get if the download is initialized.
        /// </summary>
        public bool DownloadInitialized
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadInitialized);
            }
            private set
            {
                if (DownloadInitialized != value)
                    SetFlag(HTTPDownloadState.DownloadInitialized, value);
            }
        }

        /// <summary>
        /// Gets or sets if this is being initialized.
        /// </summary>
        public bool DownloadInitializing
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadInitializing);
            }
            private set
            {
                if (DownloadInitializing != value)
                    SetFlag(HTTPDownloadState.DownloadInitializing, value);
            }
        }

        /// <summary>
        /// Gets or sets if the download failed.
        /// </summary>
        public bool DownloadFailed
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadFailed);
            }
            private set
            {
                if (DownloadFailed != value)
                    SetFlag(HTTPDownloadState.DownloadFailed, value);
            }
        }

        /// <summary>
        /// Gets or sets if the download is canceled.
        /// </summary>
        public bool DownloadCanceled
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadCanceled);
            }
            private set
            {
                if (DownloadCanceled != value)
                    SetFlag(HTTPDownloadState.DownloadCanceled, value);
            }
        }

        /// <summary>
        /// Gets or sets if the download has started.
        /// </summary>
        public bool DownloadStarted
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadStarted);
            }
            private set
            {
                if (DownloadStarted != value)
                    SetFlag(HTTPDownloadState.DownloadStarted, value);
            }
        }

        /// <summary>
        /// Gets or sets if the download has completed.
        /// </summary>
        public bool DownloadCompleted
        {
            get
            {
                return DownloadState.HasFlag(HTTPDownloadState.DownloadCompleted);
            }
            private set
            {
                if (DownloadCompleted != value)
                    SetFlag(HTTPDownloadState.DownloadCompleted, value);
            }
        }

        /// <summary>
        /// Sets or unsets a bitwise flag.
        /// </summary>
        /// <param name="flag">The <see cref="HTTPDownloadState"/>.</param>
        /// <param name="value">true to set the flag bits; otherwise, false to unset the flag.</param>
        private void SetFlag(HTTPDownloadState flag, bool value)
        {
            if (value)
                //sets flag
                DownloadState |= flag;
            else
                //unsets flag
                DownloadState &= ~flag;
        }

        /// <summary>
        /// Gets or sets if only percent changes are raised in the <see cref="HttpDownloadClient.HttpDownloadProgressChanged"/>
        /// event rather than full notification.
        /// </summary>
        public bool NotifyProgressPercentChanged
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a time-out in milliseconds when writing to or reading from a stream.
        /// </summary>
        public int ReadWriteTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the time-out value in milliseconds.
        /// </summary>
        public int RequestTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value of the User-agent HTTP header.
        /// </summary>
        public string UserAgent
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets web proxy information.
        /// </summary>
        public IWebProxy Proxy
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the number of download attempts.
        /// </summary>
        public int DownloadAttempts
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the maximum number of download attempts.
        /// </summary>
        public int MaxDownloadAttempts
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets if the download requires initialization. (Download is initialized regardless if AllowResuming is enabled.)
        /// </summary>
        public bool RequiresInitialization
        {
            get;
            set;
        }

        private string targetFile;
        /// <summary>
        /// Gets the destination filename.
        /// </summary>
        public string TargetFile
        {
            get
            {
                if (DownloadMethod != HttpDownloadMethod.ToFile)
                    throw new InvalidOperationException(DOWNLOAD_STREAMMODE_EXCEPTION);

                return targetFile;
            }
        }

        private Stream targetStream;
        /// <summary>
        /// Gets the destination Stream.
        /// </summary>
        public Stream TargetStream
        {
            get
            {
                if (DownloadMethod != HttpDownloadMethod.ToStream)
                    throw new InvalidOperationException(DOWNLOAD_FILEMODE_EXCEPTION);

                return targetStream;
            }
        }

        /// <summary>
        /// Gets the download method.
        /// </summary>
        public HttpDownloadMethod DownloadMethod
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the target file.
        /// </summary>
        public Uri Target
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the repeat delay to wait before trying again.
        /// </summary>
        public int RepeatDelay
        {
            get;
            set;
        }

        /// <summary>
        /// Gets if the download can be resumed.
        /// </summary>
        public bool AllowResuming
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the total number of bytes. This property can return null if 
        /// the data length is unknown or undetermined.
        /// </summary>
        public long? TotalBytes
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the number of bytes downloaded.
        /// </summary>
        public long BytesDownloaded
        {
            get;
            private set;
        }
        #endregion

        #region Events
        /// <summary>
        /// Event raised when the download progress changes.
        /// </summary>
        public event EventHandler<HttpDownloadProgressChangedEventArgs> HttpDownloadProgressChanged;
        /// <summary>
        /// Event raised when a packet is received.
        /// </summary>
        public event EventHandler<HttpDownloadPacketReceivedEventHandler> HttpDownloadPacketReceived;
        /// <summary>
        /// Event raised when the download fails.
        /// </summary>
        public event EventHandler<HttpDownloadExceptionEventHandler> HttpDownloadException;
        /// <summary>
        /// Event raised when the download is complete.
        /// </summary>
        public event EventHandler HttpDownloadComplete;
        /// <summary>
        /// Event raised when the download size is known.
        /// </summary>
        public event EventHandler<HttpDownloadSizeAcquiredEventArgs> HttpDownloadSizeAcquired;
        /// <summary>
        /// Event raised when the download has been initialized.
        /// </summary>
        public event EventHandler HttpDownloadInitialized;
        /// <summary>
        /// Event raised when the download fails to initialize.
        /// </summary>
        public event EventHandler<HttpDownloadExceptionEventHandler> HttpDownloadInitializationFailed;
        /// <summary>
        /// Event raised when the download is canceled.
        /// </summary>
        public event EventHandler HttpDownloadCanceled;
        /// <summary>
        /// Event raised when the initialization is canceled.
        /// </summary>
        public event EventHandler HttpDownloadInititializationCanceled;
        #endregion

        #region Raising Events
        /// <summary>
        /// This method raises the HttpDownloadCanceled event when the download is canceled.
        /// </summary>
        private void RaiseHttpDownloadCanceled()
        {
            if (HttpDownloadCanceled != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadCanceled(this, EventArgs.Empty);
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadInititializationCanceled event when the initialization is canceled.
        /// </summary>
        private void RaiseHttpDownloadInititializationCanceled()
        {
            if (HttpDownloadInititializationCanceled != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadInititializationCanceled(this, EventArgs.Empty);
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadInitialized event when initialization has completed.
        /// </summary>
        private void RaiseHttpDownloadInitialized()
        {
            if (HttpDownloadInitialized != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadInitialized(this, EventArgs.Empty);
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadInitializationFailed event with an Exception.
        /// </summary>
        /// <param name="ex">The Exception that has occurred.</param>
        private void RaiseHttpDownloadInitializationFailed(Exception ex)
        {
            if (HttpDownloadInitializationFailed != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadInitializationFailed(this, new HttpDownloadExceptionEventHandler(ex));
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadProgressChanged event.
        /// </summary>
        private void RaiseHttpDownloadProgressChanged()
        {
            if (HttpDownloadProgressChanged != null)
            {
                //create HttpDownloadProgressChangedEventArgs event data with current bytes downloaded
                //and the total number of bytes downloaded
                HttpDownloadProgressChangedEventArgs progress = new HttpDownloadProgressChangedEventArgs(BytesDownloaded, TotalBytes);

                //get percent complete
                int? percent = progress.PercentComplete;

                //determine if HttpDownloadProgressChanged should be raised by comparing last percent with current percent
                //or if all progress changes are notified
                if (lastProgressPercent != percent || !NotifyProgressPercentChanged)
                {
                    lastProgressPercent = percent;

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        HttpDownloadProgressChanged(this, progress);
                    });
                }
            }
        }

        /// <summary>
        /// This method raises the HttpDownloadPacketReceived event with the length of packet received.
        /// </summary>
        /// <param name="packetLength">The packet length downloaded.</param>
        private void RaiseHttpDownloadPacketReceived(long packetLength)
        {
            if (HttpDownloadPacketReceived != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadPacketReceived(this, new HttpDownloadPacketReceivedEventHandler(packetLength));
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadSizeAcquiredEventArgs event with the number of bytes.
        /// </summary>
        private void RaiseHttpDownloadSizeAcquired()
        {
            if (HttpDownloadSizeAcquired != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadSizeAcquired(this, new HttpDownloadSizeAcquiredEventArgs(TotalBytes));
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadException event with an Exception.
        /// </summary>
        /// <param name="ex">The Exception that has occurred.</param>
        private void RaiseHttpDownloadException(Exception ex)
        {
            if (HttpDownloadException != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadException(this, new HttpDownloadExceptionEventHandler(ex));
                });
        }

        /// <summary>
        /// This method raises the HttpDownloadComplete event when downloading has completed.
        /// </summary>
        private void RaiseHttpDownloadComplete()
        {
            if (HttpDownloadComplete != null)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    HttpDownloadComplete(this, EventArgs.Empty);
                });
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes the HttpDownloadClient with a file to download. This is a private constructor as more arguments are needed to properly initialize the HttpDownloadClient.
        /// </summary>
        /// <param name="downloadFile">The file to download.</param>
        /// <param name="method">The download method.</param>
        /// <param name="allowResuming">A value which indicates if the download can be resumed.</param>
        /// <param name="cancellationToken">An CancellationToken to monitor for cancellation; this parameter can be null.</param>
        private HttpDownloadClient(Uri downloadFile, HttpDownloadMethod method, bool allowResuming, CancellationToken cancellationToken)
        {
            switch (method)
            {
                case HttpDownloadMethod.ToFile:
                case HttpDownloadMethod.ToStream:
                    break;
                default:
                    throw new InvalidEnumArgumentException(string.Format("The System.Enum Type: '{0}' Value: '{1}' is not defined.", method.GetType(), method));
            }

            //set the percent starting point
            lastProgressPercent = -1;
            //set the initializing synchronization point
            initializingSyncPoint = 0;
            //set the downloading synchronization point
            downloadingSyncPoint = 0;
            //the total bytes to download
            TotalBytes = null;
            //the number of bytes downloaded
            BytesDownloaded = 0;
            //the http proxy to use
            Proxy = WebRequest.DefaultWebProxy;
            //the download method, to a stream or file
            DownloadMethod = method;
            //the download target file
            Target = downloadFile;
            //if the download can be resumed
            AllowResuming = allowResuming;
            //delay between attempts
            RepeatDelay = 1000;
            //read/write timeout of 5 minutes
            ReadWriteTimeout = 300000;
            //request timeout of 100 seconds
            RequestTimeout = 100000;
            //current download attempts
            DownloadAttempts = 0;
            //maximum download attempts
            MaxDownloadAttempts = 3;
            //download requires initialization
            RequiresInitialization = true;
            //set the CancellationToken
            this.cancellationToken = cancellationToken;
            //use percent changed notifications
            NotifyProgressPercentChanged = true;
        }

        /// <summary>
        /// Initializes the HttpDownloadClient to download a file to a Stream.
        /// </summary>
        /// <param name="downloadFile">The file to download.</param>
        /// <param name="destinationStream">The Stream to download to.</param>
        /// <param name="allowResuming">A value which indicates if the download can be resumed.</param>
        /// <param name="cancellationToken">An CancellationToken to monitor for cancellation; this parameter can be null.</param>
        public HttpDownloadClient(Uri downloadFile, Stream destinationStream, bool allowResuming, CancellationToken cancellationToken)
            : this(downloadFile, HttpDownloadMethod.ToStream, allowResuming, cancellationToken)
        {
            if (downloadFile == null)
                throw new ArgumentNullException("downloadFile");

            if (destinationStream == null)
                throw new ArgumentNullException("destinationStream");

            if (!destinationStream.CanWrite)
                throw new ArgumentException("The stream does not support writing.");

            if (!destinationStream.CanSeek)
                throw new ArgumentException("The stream does not support seeking.");

            targetStream = destinationStream;
        }

        /// <summary>
        /// Initializes the HttpDownloadClient to download a file.
        /// </summary>
        /// <param name="downloadFile">The file to download.</param>
        /// <param name="destinationFileName">The location to save the file.</param>
        /// <param name="allowResuming">A value which indicates if the download can be resumed.</param>
        /// <param name="cancellationToken">An CancellationToken to monitor for cancellation; this parameter can be null.</param>
        public HttpDownloadClient(Uri downloadFile, string destinationFileName, bool allowResuming, CancellationToken cancellationToken)
            : this(downloadFile, HttpDownloadMethod.ToFile, allowResuming, cancellationToken)
        {
            if (string.IsNullOrEmpty(destinationFileName))
                throw new ArgumentNullException("destinationFileName");

            targetFile = destinationFileName;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Clears the buffers when a file cannot be resumed.
        /// </summary>
        private void ClearBuffers()
        {
            switch (DownloadMethod)
            {
                case HttpDownloadMethod.ToStream:
                    //clears the stream
                    targetStream.Seek(0, SeekOrigin.Begin);
                    targetStream.SetLength(0L);
                    break;
                case HttpDownloadMethod.ToFile:
                    //deletes the file
                    if (File.Exists(targetFile))
                        File.Delete(targetFile);
                    break;
            }

            //reset bytes downloaded
            BytesDownloaded = 0L;
        }

        /// <summary>
        /// Creates a HttpWebRequest from the specified Uri.
        /// </summary>
        /// <param name="requestUri">The Uri to create a request from.</param>
        /// <returns>A request to a remote file.</returns>
        private HttpWebRequest CreateRequest(Uri requestUri)
        {
            //create the request
            HttpWebRequest request = WebRequest.Create(requestUri) as HttpWebRequest;

            //set credentials
            request.Credentials = null;
            //set authorization header
            request.PreAuthenticate = false;
            //create persistent connection
            request.KeepAlive = true;
            //set user agent
            request.UserAgent = UserAgent;
            //set read/write timeout
            request.ReadWriteTimeout = ReadWriteTimeout;
            //set timeout
            request.Timeout = RequestTimeout;
            //set proxy
            request.Proxy = Proxy;

            //return web request
            return request;
        }

        /// <summary>
        /// Initializes the download.
        /// </summary>
        public void Initialize()
        {
            Task t = AsyncInitialize();

            //wait for task to complete
            t.Wait();

            //throw any exception
            if (t.IsFaulted)
                throw t.Exception;
        }

        /// <summary>
        /// Begins the download.
        /// </summary>
        public void Download()
        {
            Task t = AsyncDownload();

            //wait for task to complete
            t.Wait();

            //throw any exception
            if (t.IsFaulted)
                throw t.Exception;
        }

        /// <summary>
        /// Initializes the download.
        /// </summary>
        public async Task AsyncInitialize()
        {
            // CompareExchange is used to take control of _InitializingSyncPoint,  
            // and to determine whether the attempt was successful.  
            // CompareExchange attempts to put 1 into _InitializingSyncPoint, but 
            // only if the current value of _InitializingSyncPoint is zero  
            // (specified by the third parameter). If another thread 
            // has set _InitializingSyncPoint to 1 or -1, an exception is raised.
            if (Interlocked.CompareExchange(ref initializingSyncPoint, 1, 0) == 0)
            {
                try
                {
                    await AsyncInitializeDownload();
                }
                finally
                {
                    //release control of _InitializingSyncPoint,
                    //allows waiting download to commence
                    initializingSyncPoint = -1;
                }
            }
            else
                throw new NotSupportedException(DOWNLOAD_INITIALIZING_EXCEPTION);
        }

        /// <summary>
        /// Begins the download.
        /// </summary>
        public async Task AsyncDownload()
        {
            // CompareExchange is used to take control of _DownloadSyncPoint,  
            // and to determine whether the attempt was successful.  
            // CompareExchange attempts to put 1 into _DownloadSyncPoint, but 
            // only if the current value of _DownloadSyncPoint is zero  
            // (specified by the third parameter). If another thread 
            // has set _DownloadSyncPoint to 1, an exception is raised.
            if (Interlocked.CompareExchange(ref downloadingSyncPoint, 1, 0) == 0)
            {
                //set DownloadStarted flag to true to indicate the download has started
                DownloadStarted = true;

                try
                {
                    //this allows the Download method to wait for a previously called
                    //AsyncInitialize to complete
                    for (; ; )
                    {
                        int pt = Interlocked.CompareExchange(ref initializingSyncPoint, 1, 0);

                        //if original value was 0 (no initialization) or 
                        //-1 (already initialized) break out of loop
                        if (pt == 0 || pt == -1)
                            break;

                        //if a CancellationToken was not provided, delay;
                        //otherwise, delay until the _CancellationToken is signaled or delay is elapsed
                        if (cancellationToken == null)
                            await Task.Delay(1);
                        else
                        {
                            await Task.Delay(1, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }

                    //initialize download
                    if (!DownloadInitialized && (RequiresInitialization || AllowResuming))
                    {
                        await AsyncInitializeDownload();

                        //if DownloadFailed or Canceled is true, initialization failed
                        if (DownloadFailed || DownloadCanceled || DownloadCompleted)
                            return;
                    }

                    //after initialization cancel check
                    if (cancellationToken != null)
                        cancellationToken.ThrowIfCancellationRequested();

                    //reset DownloadAttempts to 0
                    DownloadAttempts = 0;

                    //infinite loop allows repeated download attempts
                    for (; ; )
                    {
                        //increment DownloadAttempts
                        DownloadAttempts++;

                        HttpWebRequest request = null;

                        try
                        {
                            //preemptive cancel check
                            if (cancellationToken != null)
                                cancellationToken.ThrowIfCancellationRequested();

                            //create a HttpWebRequest from the Uri
                            request = CreateRequest(Target);

                            //if download can be resumed
                            if (AllowResuming && BytesDownloaded > 0)
                                //set the requested range
                                request.AddRange(BytesDownloaded);
                            else
                                //no resuming, clear the buffers
                                ClearBuffers();

                            using (HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse)
                            {
                                //look for partial content
                                if (AllowResuming && BytesDownloaded > 0 &&
                                    response.StatusCode != HttpStatusCode.PartialContent)
                                    //cannot resume, no partial content
                                    ClearBuffers();

                                if (BytesDownloaded > 0)
                                {
                                    //resuming, notify we got a packet (previously)
                                    RaiseHttpDownloadPacketReceived(BytesDownloaded);
                                    //raises HttpDownloadProgressChanged event if percent completed changed
                                    RaiseHttpDownloadProgressChanged();
                                }

                                //get the response stream
                                using (Stream downloadStream = response.GetResponseStream())
                                {
                                    //defines the stream to write to
                                    Stream str = null;

                                    //indicates the number of bytes read in the last packet
                                    int readCount;

                                    try
                                    {
                                        //if downloading to a stream, use the target stream;
                                        //otherwise, use the file
                                        if (DownloadMethod == HttpDownloadMethod.ToStream)
                                        {
                                            //use target stream
                                            str = targetStream;

                                            //set the stream position to the end
                                            str.Seek(0, SeekOrigin.End);
                                        }
                                        else
                                            //open target file
                                            str = File.Open(targetFile, FileMode.Append, FileAccess.Write, FileShare.None);

                                        //buffer for data
                                        byte[] buffer = new byte[0x1000];

                                        //read from the download stream
                                        while ((readCount = await downloadStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                        {
                                            //write the data to the file
                                            await str.WriteAsync(buffer, 0, readCount, cancellationToken);

                                            //increment the bytes count
                                            BytesDownloaded += readCount;

                                            //raises HttpDownloadPacketReceived event
                                            RaiseHttpDownloadPacketReceived(readCount);

                                            //raises HttpDownloadProgressChanged event if percent completed changed
                                            RaiseHttpDownloadProgressChanged();

                                            //cancel check
                                            if (cancellationToken != null)
                                                cancellationToken.ThrowIfCancellationRequested();
                                        }
                                    }
                                    finally
                                    {
                                        //dispose the stream if target file
                                        if (str != null && DownloadMethod == HttpDownloadMethod.ToFile)
                                            str.Dispose();
                                    }

                                    //download completed only if no more bytes are returned
                                    if (readCount == 0)
                                        //set DownloadCompleted flag indicating successful download
                                        DownloadCompleted = true;
                                }
                            }

                            //break the infinite loop
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            //catch and throw OperationCanceledException to outer try-catch
                            throw;
                        }
                        catch (Exception)
                        {
                            //if maximum download attempts are exceeded, throw the exception
                            if (DownloadAttempts >= MaxDownloadAttempts)
                                //throw exception to outer try-catch
                                throw;
                        }
                        finally
                        {
                            //finally clause aborts request
                            if (request != null)
                                request.Abort();
                        }

                        //determine if suitable delay defined
                        if (RepeatDelay > 0)
                        {
                            //if a CancellationToken was not provided, delay; 
                            //otherwise, delay until _CancellationToken is signaled or RepeatDelay is elapsed
                            if (cancellationToken == null)
                                await Task.Delay(RepeatDelay);
                            else
                            {
                                await Task.Delay(RepeatDelay, cancellationToken);
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }

                    //raise download complete event
                    RaiseHttpDownloadComplete();
                }
                catch (OperationCanceledException)
                {
                    //set download canceled
                    DownloadCanceled = true;

                    //raise download canceled event
                    RaiseHttpDownloadCanceled();
                }
                catch (Exception ex)
                {
                    //set download failed
                    DownloadFailed = true;

                    //raise download error event
                    RaiseHttpDownloadException(ex);
                }
                finally
                {
                    //set download ended
                    DownloadStarted = false;
                }
            }
            else
                throw new NotSupportedException(DOWNLOAD_STARTED_EXCEPTION);
        }

        /// <summary>
        /// Initializes the download without synchronization.
        /// </summary>
        private async Task AsyncInitializeDownload()
        {
            //download initializing
            DownloadInitializing = true;

            try
            {
                //determine the maximum number of initialization attempts
                int maxInitializationAttempts = MaxDownloadAttempts,
                    initializationAttempts = 0;

                for (; ; )
                {
                    //increment download attempts
                    initializationAttempts++;

                    HttpWebRequest request = null;

                    try
                    {
                        //preemptive cancellation check
                        if (cancellationToken != null)
                            cancellationToken.ThrowIfCancellationRequested();

                        //create a request to the target file
                        request = CreateRequest(Target);

                        //get the response
                        using (HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse)
                        {
                            //if the Content-Length header is not set in the response
                            //set TotalBytes to null
                            if (response.ContentLength < 0)
                                //unknown data length
                                TotalBytes = null;
                            else
                                //known data length
                                TotalBytes = response.ContentLength;

                            //if resuming is allowed, determine if the file can be resumed
                            if (AllowResuming)
                            {
                                //download can be resumed if saving to a stream or if
                                //the remote file was not modified after the file was modified
                                if (DownloadMethod == HttpDownloadMethod.ToStream)
                                    BytesDownloaded = targetStream.Length; //how much we have already downloaded
                                else
                                {
                                    FileInfo fi = new FileInfo(targetFile);

                                    if (fi.Exists)
                                        //how much we have already downloaded
                                        BytesDownloaded = fi.Length;
                                    else
                                        BytesDownloaded = 0L;
                                }

                                //attempt to resume
                                if (BytesDownloaded > 0)
                                {
                                    if (!TotalBytes.HasValue || TotalBytes < BytesDownloaded)
                                        //Cannot retrieve remote size or wrong size so delete/clear what we have so far.
                                        ClearBuffers();
                                    else if (DownloadMethod == HttpDownloadMethod.ToFile)
                                    {
                                        //determine if the time is wrong
                                        DateTime lastModifiedRemote = (response as HttpWebResponse).LastModified.ToUniversalTime();
                                        DateTime lastModifiedLocal = File.GetLastWriteTimeUtc(targetFile);

                                        if (lastModifiedRemote > lastModifiedLocal) //compare the local and remote timestamps
                                        {
                                            //Remote file is newer then local.
                                            ClearBuffers();
                                        }
                                    }

                                    if (BytesDownloaded == TotalBytes)
                                    {
                                        //download has completed
                                        DownloadCompleted = true;
                                        //push the whole download length as a single packet received
                                        RaiseHttpDownloadPacketReceived(BytesDownloaded);
                                        //raises HttpDownloadProgressChanged event if percent completed changed
                                        RaiseHttpDownloadProgressChanged();
                                        //raise download complete event
                                        RaiseHttpDownloadComplete();

                                        return;
                                    }
                                }
                            }
                            else
                                ClearBuffers(); //no resuming, clear the buffers
                        }

                        //break out of infinite loop
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        //catch and throw OperationCanceledException to outer try-catch
                        throw;
                    }
                    catch
                    {
                        //if maximum initialization attempts are exceeded
                        if (initializationAttempts >= maxInitializationAttempts)
                            throw; //throw exception to outer try-catch
                    }
                    finally
                    {
                        //finally clause aborts request
                        if (request != null)
                            request.Abort();
                    }

                    //determine if suitable delay defined
                    if (RepeatDelay > 0)
                    {
                        //if a CancellationToken was not provided, delay;
                        //otherwise, delay until _CancellationToken is signaled or RepeatDelay is elapsed
                        if (cancellationToken == null)
                            await Task.Delay(RepeatDelay);
                        else
                        {
                            await Task.Delay(RepeatDelay, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }

                //download initialized
                DownloadInitialized = true;
                //raises HttpDownloadSizeAcquired event
                RaiseHttpDownloadSizeAcquired();
                //raises HttpDownloadInitialized event
                RaiseHttpDownloadInitialized();
            }
            catch (OperationCanceledException) //catch inner throw
            {
                //download has been canceled
                DownloadCanceled = true;
                //raise HttpDownloadInititializationCanceled event
                RaiseHttpDownloadInititializationCanceled();
            }
            catch (Exception ex)
            {
                //download has failed
                DownloadFailed = true;
                //raise HttpDownloadInitializationFailed event with the exception
                RaiseHttpDownloadInitializationFailed(ex);
            }
            finally
            {
                //download initialization completed
                DownloadInitializing = false;
            }
        }
        #endregion
    }
}