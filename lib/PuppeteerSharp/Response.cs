﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PuppeteerSharp
{
    /// <summary>
    /// <see cref="Response"/> class represents responses which are received by page.
    /// </summary>
    public class Response
    {
        private readonly CDPSession _client;
        private bool _fromDiskCache;
        private string _buffer;

        internal Response(
            CDPSession client,
            Request request,
            HttpStatusCode status,
            Dictionary<string, object> headers,
            bool fromDiskCache,
            bool fromServiceWorker,
            SecurityDetails securityDetails)
        {
            _client = client;
            Request = request;
            Status = status;
            Url = request.Url;
            _fromDiskCache = fromDiskCache;
            FromServiceWorker = fromServiceWorker;

            Headers = new Dictionary<string, object>();
            if (headers != null)
            {
                foreach (var keyValue in headers)
                {
                    Headers[keyValue.Key] = keyValue.Value;
                }
            }
            SecurityDetails = securityDetails;
            BodyLoadedTaskWrapper = new TaskCompletionSource<bool>();
        }

        #region Properties

        /// <summary>
        /// Contains the URL of the response.
        /// </summary>
        public string Url { get; }
        /// <summary>
        /// An object with HTTP headers associated with the response. All header names are lower-case.
        /// </summary>
        /// <value>The headers.</value>
        public Dictionary<string, object> Headers { get; }
        /// <summary>
        /// Contains the status code of the response
        /// </summary>
        /// <value>The status.</value>
        public HttpStatusCode Status { get; }
        /// <summary>
        /// Contains a boolean stating whether the response was successful (status in the range 200-299) or not.
        /// </summary>
        /// <value><c>true</c> if ok; otherwise, <c>false</c>.</value>
        public bool Ok => Status == 0 || ((int)Status >= 200 && (int)Status <= 299);
        /// <summary>
        /// A matching <see cref="Request"/> object.
        /// </summary>
        /// <value>The request.</value>
        public Request Request { get; }
        /// <summary>
        /// True if the response was served from either the browser's disk cache or memory cache.
        /// </summary>
        public bool FromCache => _fromDiskCache || (Request?.FromMemoryCache ?? false);
        /// <summary>
        /// Gets or sets the security details.
        /// </summary>
        /// <value>The security details.</value>
        public SecurityDetails SecurityDetails { get; }
        /// <summary>
        /// Gets a value indicating whether the <see cref="Response"/> was served by a service worker.
        /// </summary>
        /// <value><c>true</c> if the <see cref="Response"/> was served by a service worker; otherwise, <c>false</c>.</value>
        public bool FromServiceWorker { get; }

        internal TaskCompletionSource<bool> BodyLoadedTaskWrapper { get; }
        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a Task which resolves to a buffer with response body
        /// </summary>
        /// <returns>A Task which resolves to a buffer with response body</returns>
        public async ValueTask<string> BufferAsync()
        {
            if (_buffer == null)
            {
                await BodyLoadedTaskWrapper.Task.ConfigureAwait(false);

                try
                {
                    var response = await _client.SendAsync("Network.getResponseBody", new Dictionary<string, object>
                    {
                        {"requestId", Request.RequestId}
                    }).ConfigureAwait(false);

                    _buffer = response.body.ToString();
                }
                catch (Exception ex)
                {
                    throw new BufferException("Unable to get response body", ex);
                }
            }

            return _buffer;
        }

        /// <summary>
        /// Returns a Task which resolves to a text representation of response body
        /// </summary>
        /// <returns>A Task which resolves to a text representation of response body</returns>
        public ValueTask<string> TextAsync() => BufferAsync();

        /// <summary>
        /// Returns a Task which resolves to a <see cref="JObject"/> representation of response body
        /// </summary>
        /// <seealso cref="JsonAsync{T}"/>
        /// <returns>A Task which resolves to a <see cref="JObject"/> representation of response body</returns>
        public async Task<JObject> JsonAsync() => JObject.Parse(await TextAsync().ConfigureAwait(false));

        /// <summary>
        /// Returns a Task which resolves to a <typeparamref name="T"/> representation of response body
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <seealso cref="JsonAsync"/>
        /// <returns>A Task which resolves to a <typeparamref name="T"/> representation of response body</returns>
        public async Task<T> JsonAsync<T>() => (await JsonAsync().ConfigureAwait(false)).ToObject<T>();

        #endregion
    }
}