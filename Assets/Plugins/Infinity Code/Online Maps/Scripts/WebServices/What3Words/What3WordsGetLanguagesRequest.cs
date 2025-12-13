/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    public class What3WordsGetLanguagesRequest : What3WordsRequestBase<What3WordsLanguagesResult>
    {
        protected override string service => "available-languages";

        public static What3WordsLanguagesResult Parse(string response) => What3WordsLanguagesResult.Parse(response);
        public override What3WordsLanguagesResult ParseResult(string response) => Parse(response);
    }
}