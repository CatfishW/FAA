/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(PlaneControl), true)]
    public class PlaneControlEditor : ControlBase3DEditor<PlaneControl>
    {
        private SerializedProperty pTexture;

        protected override void CacheSerializedFields()
        {
            base.CacheSerializedFields();
            
            pTexture = serializedObject.FindProperty("_texture");
        }
        
        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();
            
            rootLayoutItem.Create("Texture", () => ControlBase2DEditor<ControlBase2D>.DrawTexturePropsGUI(pTexture, map));
        }
    }
}