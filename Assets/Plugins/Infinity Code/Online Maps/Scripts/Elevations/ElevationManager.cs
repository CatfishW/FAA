/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Base class - singleton for elevation manager
    /// </summary>
    /// <typeparam name="T">Type of elevation manager</typeparam>
    public abstract class ElevationManager<T> : ElevationManagerBase
        where T : ElevationManager<T>
    {
        /// <summary>
        /// Gets the instance of the elevation manager.
        /// </summary>
        public new static T instance => _instance as T;

        /// <summary>
        /// Elevation manager is enabled
        /// </summary>
        public static bool isEnabled => instance.enabled;

        /// <summary>
        /// Called when the component is disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (map) map.Redraw();
        }

        /// <summary>
        /// Called when the component is destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
        }

        /// <summary>
        /// Called when the component is enabled.
        /// </summary>
        protected virtual void OnEnable()
        {
            _instance = (T)this;
            if (control)
            {
                control.OnUpdateMeshBefore += UpdateSizeInScene;
            }

            if (map) map.Redraw();
        }

        public override void RequestNewElevationData()
        {
            //TODO: Remove this method
        }

        /// <summary>
        /// Sets elevation data
        /// </summary>
        /// <param name="data">Array of elevation data. By default: 32x32.</param>
        public virtual void SetElevationData(short[,] data)
        {
        }

        private void UpdateSizeInScene()
        {
            _sizeInScene = control.sizeInScene;
        }
    }
}