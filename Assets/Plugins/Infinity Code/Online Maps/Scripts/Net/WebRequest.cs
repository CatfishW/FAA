/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OnlineMaps
{
    /// <summary>
    /// The wrapper class for UnityWebRequest.<br/>
    /// It allows you to control requests.
    /// </summary>
    public class WebRequest : CustomYieldInstruction, IDataContainer
    {
        #region Actions

        /// <summary>
        /// Called when a request has been created but not yet sent.
        /// </summary>
        public static Action<WebRequest> OnCreateRequest;

        /// <summary>
        /// This event is occurs when URL is initialized, and allows you to modify it.
        /// </summary>
        public static Func<string, string> OnInit;

        /// <summary>
        /// Events to validate the request. Return false if you want to cancel the request.
        /// </summary>
        public static Predicate<string> OnValidate;

        /// <summary>
        /// Event that occurs when a request is completed.
        /// </summary>
        public Action<WebRequest> OnComplete;

        #endregion

        #region Variables

        private byte[] _bytes;
        private string _error;
        private string _id;
        private bool _isDone;
        private string _url;
        private MonoBehaviour currentCoroutineBehaviour;
        private bool isYield;
        private string responseHeadersString;
        private RequestType type;
        private IEnumerator waitResponse;

        private UnityWebRequest www;

        #endregion

        #region Properties

        /// <summary>
        /// Gets / sets custom data value by key
        /// </summary>
        /// <param name="key">Custom data key</param>
        /// <returns>Custom data value</returns>
        public object this[string key]
        {
            get => customData.GetValueOrDefault(key);
            set => customData[key] = value;
        }

        /// <summary>
        /// Returns the contents of the fetched web page as a byte array.
        /// </summary>
        public byte[] bytes => type == RequestType.www ? www.downloadHandler.data : _bytes;

        /// <summary>
        /// The number of bytes downloaded by this query.
        /// </summary>
        public int bytesDownloaded
        {
            get
            {
                if (type == RequestType.www) return (int)www.downloadedBytes;
                return _bytes != null ? _bytes.Length : 0;
            }
        }

        /// <summary>
        /// Gets custom data dictionary
        /// </summary>
        public Dictionary<string, object> customData { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// Returns an error message if there was an error during the download.
        /// </summary>
        public string error => type == RequestType.www ? www.error : _error;

        /// <summary>
        /// This property is true if the request has encountered an error.
        /// </summary>
        public bool hasError => !string.IsNullOrEmpty(error);

        /// <summary>
        /// ID of request.
        /// </summary>
        public string id => _id;

        /// <summary>
        /// Is the download already finished?
        /// </summary>
        public bool isDone
        {
            get
            {
                if (type == RequestType.www) return www?.isDone ?? false;
                return _isDone;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this coroutine should continue executing.
        /// </summary>
        /// <value>True if the coroutine should continue waiting; otherwise, false.</value>
        public override bool keepWaiting
        {
            get
            {
                isYield = true;
                return !isDone;
            }
        }

        /// <summary>
        /// Gets the UnityWebRequest object for this request.
        /// </summary>
        /// <returns>The UnityWebRequest object.</returns>
        public UnityWebRequest request => www;

        /// <summary>
        /// Dictionary of headers returned by the request.
        /// </summary>
        public Dictionary<string, string> responseHeaders
        {
            get
            {
                if (!isDone) throw new UnityException("WWW is not finished downloading yet");

                if (type == RequestType.www) return www.GetResponseHeaders();
                return ParseHTTPHeaderString(responseHeadersString);
            }
        }

        /// <summary>
        /// Returns the contents of the fetched web page as a string.
        /// </summary>
        public string text
        {
            get
            {
                if (type == RequestType.www) return www.downloadHandler.text;
                return _bytes != null ? GetTextEncoder().GetString(_bytes, 0, _bytes.Length) : null;
            }
        }

        /// <summary>
        /// The URL of this request.
        /// </summary>
        public string url => _url;

        #endregion

        #region Methods

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="url">URL of request</param>
        public WebRequest(string url)
        {
            SetURL(url);
            type = RequestType.www;
            currentCoroutineBehaviour = CoroutineBehaviour.Get();

            if (OnValidate != null && !OnValidate(this.url))
            {
                currentCoroutineBehaviour.StartCoroutine(WaitCancel());
                return;
            }

            www = UnityWebRequest.Get(this.url);
            if (OnCreateRequest != null) OnCreateRequest(this);
            www.SendWebRequest();
            
            waitResponse = WaitResponse();
            currentCoroutineBehaviour.StartCoroutine(waitResponse);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="url">URL of request</param>
        public WebRequest(StringBuilder url) : this(url.ToString())
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="url">URL of request</param>
        /// <param name="type">Type of request</param>
        /// <param name="reqID">Request ID</param>
        public WebRequest(string url, RequestType type, string reqID)
        {
            this.type = type;
            SetURL(url);
            
            currentCoroutineBehaviour = CoroutineBehaviour.Get();;

            if (OnValidate != null && !OnValidate(this.url))
            {
                currentCoroutineBehaviour.StartCoroutine(WaitCancel());
                return;
            }

            _id = reqID;
            if (type == RequestType.www)
            {
                www = UnityWebRequest.Get(this.url);
                if (OnCreateRequest != null) OnCreateRequest(this);
                www.SendWebRequest();
                
                waitResponse = WaitResponse();
                currentCoroutineBehaviour.StartCoroutine(waitResponse);
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="www">WWW instance.</param>
        private WebRequest(UnityWebRequest www)
        {
            SetURL(www.url);
            type = RequestType.www;
            
            currentCoroutineBehaviour = CoroutineBehaviour.Get();;

            if (OnValidate != null && !OnValidate(url))
            {
                currentCoroutineBehaviour.StartCoroutine(WaitCancel());
                return;
            }

            this.www = www;
            waitResponse = WaitResponse();
            currentCoroutineBehaviour.StartCoroutine(waitResponse);
        }

        /// <summary>
        /// Constructor for creating a POST request with headers.
        /// </summary>
        /// <param name="url">URL of the request.</param>
        /// <param name="postData">Data to be posted.</param>
        /// <param name="headers">Optional headers for the request.</param>
        public WebRequest(string url, string postData, Dictionary<string, string> headers = null)
        {
            SetURL(url);
            type = RequestType.www;
            currentCoroutineBehaviour = CoroutineBehaviour.Get();;

            if (OnValidate != null && !OnValidate(this.url))
            {
                currentCoroutineBehaviour.StartCoroutine(WaitCancel());
                return;
            }

            byte[] bodyRaw = Encoding.UTF8.GetBytes(postData);

            www = new UnityWebRequest(this.url, "POST");
            if (OnCreateRequest != null) OnCreateRequest(this);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    www.SetRequestHeader(pair.Key, pair.Value);
                }
            }

            www.SendWebRequest();
            waitResponse = WaitResponse();
            currentCoroutineBehaviour.StartCoroutine(waitResponse);
        }

        /// <summary>
        /// Disposes of an existing object.
        /// </summary>
        public void Dispose()
        {
            if (www != null && !www.isDone) www.Dispose();
            www = null;
            customData = null;
            if (waitResponse != null) currentCoroutineBehaviour.StopCoroutine(waitResponse);
        }

        /// <summary>
        /// Escapes characters in a string to ensure they are URL-friendly.
        /// </summary>
        /// <param name="s">A string with characters to be escaped.</param>
        /// <returns>Escaped string.</returns>
        public static string EscapeURL(string s)
        {
            return UnityWebRequest.EscapeURL(s);
        }

        private void Finish()
        {
            if (OnComplete != null) OnComplete(this);

            if (!hasError)
            {
                Log.Info("Response: " + www.responseCode + " from " + url, Log.Type.request);
            }
            else
            {
                Log.Warning("Error: " + error + "\nfrom " + url, Log.Type.request);
            }

            if (!isYield) Dispose();
        }

        /// <summary>
        /// Gets the data by key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The data.</returns>
        public T GetData<T>(string key)
        {
            object val = customData.GetValueOrDefault(key);
            return val != null? (T)val : default;
        }

        private Encoding GetTextEncoder()
        {
            string str;
            if (!responseHeaders.TryGetValue("CONTENT-TYPE", out str)) return Encoding.UTF8;

            int index = str.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
            if (index == -1) return Encoding.UTF8;

            int num2 = str.IndexOf('=', index);
            if (num2 == -1) return Encoding.UTF8;

            char[] trimChars = { '\'', '"' };
            string name = str.Substring(num2 + 1).Trim().Trim(trimChars).Trim();
            int length = name.IndexOf(';');
            if (length > -1) name = name.Substring(0, length);

            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (Exception)
            {
                Debug.Log("Unsupported encoding: '" + name + "'");
            }

            return Encoding.UTF8;
        }

        /// <summary>
        /// Replaces the contents of an existing Texture2D with an image from the downloaded data.
        /// </summary>
        /// <param name="tex">An existing texture object to be overwritten with the image data.</param>
        public void LoadImageIntoTexture(Texture2D tex)
        {
            if (tex == null) throw new Exception("Texture is null");

            if (type == RequestType.www) tex.LoadImage(bytes);
            else tex.LoadImage(_bytes);
        }
        
        internal static Dictionary<string, string> ParseHTTPHeaderString(string input)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(input)) return dictionary;

            StringReader reader = new StringReader(input);
            int num = 0;
            while (true)
            {
                string str = reader.ReadLine();
                if (str == null) return dictionary;

                if (num++ == 0 && str.StartsWith("HTTP"))
                {
                    dictionary["STATUS"] = str;
                }
                else
                {
                    int index = str.IndexOf(": ");
                    if (index == -1) continue;

                    string str2 = str.Substring(0, index).ToUpper();
                    string str3 = str.Substring(index + 2);
                    dictionary[str2] = str3;
                }
            }
        }

        /// <summary>
        /// Sets the contents and headers of the response for type = direct.
        /// </summary>
        /// <param name="responseHeadersString">Headers of response.</param>
        /// <param name="_bytes">Content of response.</param>
        public void SetBytes(string responseHeadersString, byte[] _bytes)
        {
            if (type == RequestType.www) throw new Exception("WebRequest.SetBytes available only for type = direct.");

            this.responseHeadersString = responseHeadersString;
            this._bytes = _bytes;
            _isDone = true;
            Finish();
        }

        /// <summary>
        /// Sets the error for type = direct.
        /// </summary>
        /// <param name="errorStr">Error message.</param>
        public void SetError(string errorStr)
        {
            if (type == RequestType.www) throw new Exception("WebRequest.SetError available only for type = direct.");
            _error = errorStr;
            _isDone = true;
            Finish();
        }

        private void SetURL(string url)
        {
#if UNITY_IOS
            url = url.Replace("|", "%7C");
#endif

            if (OnInit != null) url = OnInit(url);
            Log.Info(url, Log.Type.request);
            _url = url;
        }

        private IEnumerator WaitCancel()
        {
            type = RequestType.direct;
            yield return null;

            _error = "Request canceled.";
            Finish();
        }

        private IEnumerator WaitResponse()
        {
            while (!www.isDone)
            {
                yield return null;
            }

            waitResponse = null;
            Finish();
            
            if (isYield)
            {
                yield return null;
                Dispose();
            }
        }

        #endregion

        /// <summary>
        /// Type of request.
        /// </summary>
        public enum RequestType
        {
            /// <summary>
            /// The request will be processed independently.<br/>
            /// Use Utils.OnGetWWW to process of request.
            /// </summary>
            direct,

            /// <summary>
            /// The request will be processed using WWW or UnityWebClient class.
            /// </summary>
            www
        }
    }
}