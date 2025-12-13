/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Controls map using User Location.
    /// </summary>
    public abstract class UserLocationGenericBase<T> : UserLocationBase where T : UserLocationGenericBase<T>
    {
        private static T _instance;

        /// <summary>
        /// Instance of User Location
        /// </summary>
        public static T instance => _instance;

        protected override void OnEnable()
        {
            _instance = (T)this;
            base.OnEnable();
        }
    }
}