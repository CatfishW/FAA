/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// HERE Routing API is a web service API that offers easy and fast routing for several regions in the world. <br/>
    /// https://developer.here.com/rest-apis/documentation/routing/topics/resource-calculate-route.html
    /// </summary>
    public class HereRoutingRequest: TextWebService<HereRoutingResult>
    {
        public string appId;
        public string appCode;
        public string apiKey;
        
        public GeoPoint origin;
        public GeoPoint destination;
        public GeoPoint[] via;
        public Dictionary<string, string> extra;

        public HereRoutingRequest(GeoPoint origin, GeoPoint destination)
        {
            this.origin = origin;
            this.destination = destination;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            ValidateCredentials();

            builder.Append("https://router.hereapi.com/v8/routes?");
            AppendCredentials(builder);
            AppendLocations(builder);
            AppendExtra(builder);
        }

        private void AppendExtra(StringBuilder builder)
        {
            bool hasTransportMode = false;

            if (extra != null)
            {
            
                foreach (KeyValuePair<string, string> pair in extra)
                {
                    builder.Append("&").Append(pair.Key).Append("=").Append(pair.Value);
                    if (pair.Key == "transportMode") hasTransportMode = true;
                }
            }

            if (!hasTransportMode) builder.Append("&transportMode=car");
        }

        private void AppendLocations(StringBuilder builder)
        {
            builder.Append("&origin=");
            AppendLocation(builder, origin);
        
            builder.Append("&destination=");
            AppendLocation(builder, destination);

            if (via == null) return;
            
            builder.Append("&via=");
            for (int i = 0; i < via.Length; i++)
            {
                if (i > 0) builder.Append(",");
                AppendLocation(builder, via[i]);
            }
        }

        private void AppendCredentials(StringBuilder builder)
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                builder.Append("apiKey=").Append(apiKey);
            }
            else
            {
                builder.Append("app_code=").Append(appCode);
                builder.Append("&app_id=").Append(appId);
            }
        }

        public static HereRoutingResult Parse(string response) => HereRoutingResult.Parse(response);
        public override HereRoutingResult ParseResult(string response) => Parse(response);

        private void ValidateCredentials()
        {
            if (string.IsNullOrEmpty(appCode)) appCode = KeyManager.HereAppCode();
            if (string.IsNullOrEmpty(appId)) appId = KeyManager.HereAppID();
            if (string.IsNullOrEmpty(apiKey)) apiKey = KeyManager.HereApiKey();

            if (string.IsNullOrEmpty(apiKey) && 
                (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appCode)))
            {
                throw new Exception("HERE requires App ID + App Code or Api Key.");
            }
        }
    }
}