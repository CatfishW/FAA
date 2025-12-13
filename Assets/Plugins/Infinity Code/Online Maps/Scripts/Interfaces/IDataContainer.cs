/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    public interface IDataContainer
    {
        object this[string key] { get; set; }
        T GetData<T>(string key);
    }
}