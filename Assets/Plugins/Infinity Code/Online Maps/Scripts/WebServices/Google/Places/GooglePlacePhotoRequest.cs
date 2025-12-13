/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The Place Photo service, part of the Google Places API Web Service, is a read-only API that allows you to add high quality photographic content to your application. <br/>
    /// The Place Photo service gives you access to the millions of photos stored in the Places and Google+ Local database. <br/>
    /// When you get place information using a Place Details request, photo references will be returned for relevant photographic content. <br/>
    /// The Nearby Search and Text Search requests also return a single photo reference per place, when relevant. <br/>
    /// Using the Photo service you can then access the referenced photos and resize the image to the optimal size for your application.
    /// </summary>
    public class GooglePlacePhotoRequest : WebService<byte[], Texture2D>
    {
        public readonly string photoReference;
        public string key;
        public int? maxWidth;
        public int? maxHeight;

        public GooglePlacePhotoRequest(string photoReference)
        {
            this.photoReference = photoReference;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/place/photo?key=");
            
            if (!string.IsNullOrEmpty(key)) builder.Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append(KeyManager.GoogleMaps());
            else throw new Exception("Please specify Google API Key");
            
            builder.Append("&photo_reference=").Append(photoReference);
            if (maxWidth.HasValue) builder.Append("&maxwidth=").Append(maxWidth);
            if (maxHeight.HasValue) builder.Append("&maxheight=").Append(maxHeight);

            if (!maxWidth.HasValue && !maxHeight.HasValue) builder.Append("&maxwidth=").Append(800);
        }

        protected override byte[] GetResponse()
        {
            return www.bytes;
        }
        
        public static Texture2D Parse(byte[] response)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.LoadImage(response);
            return texture;
        }

        public override Texture2D ParseResult(byte[] response)
        {
            return Parse(response);
        }
    }
}