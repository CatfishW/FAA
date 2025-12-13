/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Abstract class for web services that handle a response.
    /// </summary>
    public abstract class WebService<TValue> : WebService
    {
        /// <summary>
        /// Event that occurs when the web service is disposed.
        /// </summary>
        public Action<WebService<TValue>> OnDispose;

        /// <summary>
        /// Event that occurs when the web service fails.
        /// </summary>
        public Action<WebService<TValue>> OnFailed;

        /// <summary>
        /// Event that occurs when the web service finishes.
        /// </summary>
        public Action<WebService<TValue>> OnFinish;

        /// <summary>
        /// Event that occurs when the web service succeeds.
        /// </summary>
        public Action<WebService<TValue>> OnSuccess;

        /// <summary>
        /// Event that occurs when a response is received from webservice.
        /// </summary>
        public Action<TValue> OnComplete;

        /// <summary>
        /// Gets a response from webservice.
        /// </summary>
        /// <value>
        /// The response.
        /// </value>
        public TValue response { get; protected set; }

        /// <summary>
        /// Broadcasts the result of the web service request.
        /// </summary>
        protected abstract void BroadcastResult();

        public override void Dispose()
        {
            if (OnDispose != null) OnDispose(this);

            base.Dispose();

            response = default;
            OnDispose = null;
            OnFailed = null;
            OnFinish = null;
            OnSuccess = null;
            OnComplete = null;
        }

        protected abstract TValue GetResponse();
        
        /// <summary>
        /// Adds a callback to be invoked when the web service completes successfully.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the response.</param>
        /// <returns>The current instance of the web service.</returns>
        /// <summary>
        /// Adds a callback to be invoked when the web service completes successfully.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the response.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue> HandleComplete(Action<TValue> callback)
        {
            OnComplete += callback;
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked when the web service is disposed.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the web service instance.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue> HandleDispose(Action<WebService<TValue>> callback)
        {
            OnDispose += callback;
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked when the web service fails.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the web service instance.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue> HandleFailed(Action<WebService<TValue>> callback)
        {
            OnFailed += callback;
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked when the web service finishes.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the web service instance.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue> HandleFinish(Action<WebService<TValue>> callback)
        {
            OnFinish += callback;
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked when the web service succeeds.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the web service instance.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue> HandleSuccess(Action<WebService<TValue>> callback)
        {
            OnSuccess += callback;
            return this;
        }

        private void OnRequestComplete(WebRequest request)
        {
            ProcessResponse();
            if (isYield)
            {
                CoroutineBehaviour.Start(WaitDispose());
            }
        }

        protected override void ProcessResponse()
        {
            status = www.hasError ? RequestStatus.error : RequestStatus.success;
            response = status == RequestStatus.success ? GetResponse() : default;

            if (status == RequestStatus.success)
            {
                if (OnComplete != null) OnComplete(response);
                if (OnSuccess != null) OnSuccess(this);
                BroadcastResult();
            }
            else
            {
                if (OnFailed != null) OnFailed(this);
            }

            if (OnFinish != null) OnFinish(this);
        }

        public override void Send()
        {
            base.Send();
            www.OnComplete += OnRequestComplete;
        }

        private IEnumerator WaitDispose()
        {
            yield return null;
            Dispose();
        }
    }


    /// <summary>
    /// Abstract class for web services that handle a response and a result.
    /// </summary>
    public abstract class WebService<TValue, TResult> : WebService<TValue>
    {
        /// <summary>
        /// Event that occurs when the result is available.
        /// </summary>
        public Action<TResult> OnResult;

        private TResult _result;

        /// <summary>
        /// Gets the result of the web service request.
        /// </summary>
        /// <value>
        /// The result of the web service request.
        /// </value>
        public TResult result
        {
            get
            {
                if (_result == null) _result = ParseResult();
                return _result;
            }
        }

        protected override void BroadcastResult()
        {
            OnResult?.Invoke(result);
        }

        public override void Dispose()
        {
            base.Dispose();

            _result = default;
            OnResult = null;
        }

        /// <summary>
        /// Adds a callback to be invoked when the result is available.
        /// </summary>
        /// <param name="callback">The callback to be invoked with the result.</param>
        /// <returns>The current instance of the web service.</returns>
        public WebService<TValue, TResult> HandleResult(Action<TResult> callback)
        {
            OnResult += callback;
            return this;
        }

        /// <summary>
        /// Parses the result from the response.
        /// </summary>
        /// <returns>The parsed result.</returns>
        protected TResult ParseResult()
        {
            return ParseResult(response);
        }


        /// <summary>
        /// Parses the result from the response.
        /// </summary>
        /// <param name="response">The response from the web service.</param>
        /// <returns>The parsed result.</returns>
        public abstract TResult ParseResult(TValue response);
    }
}