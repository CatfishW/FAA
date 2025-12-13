/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/ZoomArea")]
    public class ZoomArea : MonoBehaviour
    {
        public Map map;

        private void Start()
        {
            if (!map) map = Map.instance;
        }
        
        public void ZoomIn()
        {
            map.view.zoom++;
        }

        public void ZoomOut()
        {
            map.view.zoom--;
        }

        public void SetZoom(int zoom)
        {
            map.view.zoom = zoom;
        }
    }
}