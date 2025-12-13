/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps.Webservices
{
    public abstract class TextWebService<TResult>: WebService<string, TResult>
    {
        protected override string GetResponse()
        {
            return www.text;
        }
    }
}