/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The Google Maps Roads API identifies the roads a vehicle was traveling along and provides additional metadata about those roads, such as speed limits.<br/>
    /// https://developers.google.com/maps/documentation/roads/intro?hl=en
    /// </summary>
    public abstract class GoogleRoadsRequestBase<T> : TextWebService<T>
    {
        /// <summary>
        /// Your application's API key. This key identifies your application for purposes of quota management. <br/>
        /// Visit the Google APIs Console to select an API Project and obtain your key.
        /// </summary>
        public string key;
    }
}