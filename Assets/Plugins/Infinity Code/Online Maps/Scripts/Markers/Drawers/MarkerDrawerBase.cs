/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Base class for drawing markers
    /// </summary>
    public abstract class MarkerDrawerBase
    {
        protected Map map;

        private bool elevationManagerInitialized;
        protected ElevationManagerBase _elevationManager;

        protected ElevationManagerBase elevationManager
        {
            get
            {
                if (elevationManagerInitialized) return _elevationManager;
                
                elevationManagerInitialized = true;

                ControlBaseDynamicMesh control = map.control as ControlBaseDynamicMesh;
                if (control) _elevationManager = control.elevationManager;

                return _elevationManager;
            }
        }

        protected bool hasElevation => elevationManager && elevationManager.enabled;

        /// <summary>
        /// Dispose the current drawer
        /// </summary>
        public virtual void Dispose()
        {
            map = null;
            _elevationManager = null;
        }
    }
}