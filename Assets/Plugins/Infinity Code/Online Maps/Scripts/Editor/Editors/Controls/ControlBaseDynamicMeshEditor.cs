/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(ControlBaseDynamicMesh), true)]
    public abstract class ControlBaseDynamicMeshEditor<T> : ControlBase3DEditor<T>
        where T : ControlBaseDynamicMesh
    {
    
    }
}