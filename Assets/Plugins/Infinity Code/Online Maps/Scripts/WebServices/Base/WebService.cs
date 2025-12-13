/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The base class for working with the web services.
    /// </summary>
    public abstract class WebService : CustomYieldInstruction, IDataContainer
    {
        /// <summary>
        /// The URL of the web service.
        /// </summary>
        protected string _url;

        protected bool isYield;

        /// <summary>
        /// Gets custom data dictionary
        /// </summary>
        public Dictionary<string, object> customData { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets a value indicating whether the web service request is completed.
        /// </summary>
        public bool IsCompleted => isDone;

        /// <summary>
        /// Is the download already finished?
        /// </summary>
        public bool isDone => www?.isDone ?? false;

        public override bool keepWaiting
        {
            get
            {
                isYield = true;
                return !isDone;
            }
        }

        /// <summary>
        /// Gets the current status of the request to webservice.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public RequestStatus status { get; protected set; } = RequestStatus.idle;

        /// <summary>
        /// Gets the URL of the web service.
        /// </summary>
        public virtual string url
        {
            get
            {
                if (!string.IsNullOrEmpty(_url)) return _url;

                StringBuilder builder = StaticStringBuilder.Start();
                GenerateUrl(builder);
                _url = builder.ToString();
                return _url;
            }
        }


        /// <summary>
        /// Gets or sets the web request associated with the web service.
        /// </summary>
        public WebRequest www { get; protected set; }

        /// <summary>
        /// Gets / sets custom fields value by key
        /// </summary>
        /// <param name="key">Custom field key</param>
        /// <returns>Custom field value</returns>
        public object this[string key]
        {
            get => customData.GetValueOrDefault(key);
            set => customData[key] = value;
        }

        /// <summary>
        /// Appends the location to the StringBuilder in the specified format.
        /// </summary>
        /// <param name="builder">The StringBuilder to append to.</param>
        /// <param name="location">The GeoPoint location to append.</param>
        /// <param name="reverse">If set to true, appends the location in reverse order (x,y).</param>
        protected static void AppendLocation(StringBuilder builder, GeoPoint location, bool reverse = false)
        {
            if (reverse)
            {
                builder.Append(location.x.ToString(Culture.numberFormat))
                    .Append(",").Append(location.y.ToString(Culture.numberFormat));
            }
            else
            {
                builder.Append(location.y.ToString(Culture.numberFormat))
                    .Append(",").Append(location.x.ToString(Culture.numberFormat));
            }
        }

        /// <summary>
        /// Creates the web request using the specified URL.
        /// </summary>
        protected virtual void CreateRequest()
        {
            www = new WebRequest(url);
        }

        /// <summary>
        /// Disposes of the resources used by the web service.
        /// </summary>
        /// <remarks>
        /// This method sets the status to disposed, nullifies the web request and custom fields.
        /// </remarks>
        public virtual void Dispose()
        {
            status = RequestStatus.disposed;
            www = null;
            customData = null;
        }

        /// <summary>
        /// Generates the URL for the web service request.
        /// </summary>
        /// <param name="builder">The StringBuilder to append the URL to.</param>
        protected abstract void GenerateUrl(StringBuilder builder);

        /// <summary>
        /// Gets the value from the custom data dictionary by key.
        /// </summary>
        /// <param name="key">Field key.</param>
        /// <typeparam name="T">Type of value.</typeparam>
        /// <returns>Field value.</returns>
        public T GetData<T>(string key)
        {
            object val = customData.GetValueOrDefault(key);
            return val != null? (T)val: default;
        }
        
        /// <summary>
        /// Gets the result of the web service.
        /// </summary>
        public void GetResult()
        {
        }

        /// <summary>
        /// Processes the response from the web service.
        /// </summary>
        protected abstract void ProcessResponse();

        /// <summary>
        /// Sends the web service request.
        /// </summary>
        public virtual void Send()
        {
            if (status != RequestStatus.idle) return;
            status = RequestStatus.downloading;
            CreateRequest();
        }
    }
}