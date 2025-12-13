/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The base class for working with the web services returns text response.
    /// </summary>
    public abstract class TextWebService: WebService<string>
    {
        protected override string GetResponse()
        {
            return www.text;
        }
    }
}