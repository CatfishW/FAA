/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(ControlBase2D), true)]
    public abstract class ControlBase2DEditor<T> : ControlBaseEditor<T>
        where T: ControlBase2D
    {
        private SerializedProperty pTexture;
        
        private string textureFilename = "OnlineMaps";
        private int textureHeight = 512;
        private int textureWidth = 512;
        private bool showCreateTexture;

        protected override void CacheSerializedFields()
        {
            base.CacheSerializedFields();
            
            pTexture = serializedObject.FindProperty("_texture");
        }
        
        public static void CheckAPITextureImporter(SerializedProperty property)
        {
            Texture2D texture = property.objectReferenceValue as Texture2D;
            CheckAPITextureImporter(texture);
        }

        private static void CheckAPITextureImporter(Texture2D texture)
        {
            if (!texture) return;

            string textureFilename = AssetDatabase.GetAssetPath(texture.GetInstanceID());
            TextureImporter textureImporter = AssetImporter.GetAtPath(textureFilename) as TextureImporter;
            if (!textureImporter) return;

            bool needReimport = false;
            if (textureImporter.mipmapEnabled)
            {
                textureImporter.mipmapEnabled = false;
                needReimport = true;
            }
            if (!textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                needReimport = true;
            }
            if (textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                needReimport = true;
            }
            if (textureImporter.maxTextureSize < 256)
            {
                textureImporter.maxTextureSize = 256;
                needReimport = true;
            }

            if (needReimport) AssetDatabase.ImportAsset(textureFilename, ImportAssetOptions.ForceUpdate);
        }
        
        private void CreateTexture()
        {
            string texturePath = $"Assets/{textureFilename}.png";
        
            Texture2D texture = new Texture2D(textureWidth, textureHeight);
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            AssetDatabase.Refresh();
            UpdateTextureImporter(texturePath);

            Utils.Destroy(texture);
            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        private void DrawCreateTextureGUI()
        {
            textureFilename = EditorGUILayout.TextField("Filename", textureFilename);

            textureWidth = EditorGUILayout.IntPopup("Width", textureWidth, EditorUtils.availableSizesStr, EditorUtils.availableSizes);
            textureHeight = EditorGUILayout.IntPopup("Height", textureHeight, EditorUtils.availableSizesStr, EditorUtils.availableSizes);

            if (GUILayout.Button("Create"))
            {
                CreateTexture();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
        }

        private void DrawCreateTextureBlock()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showCreateTexture = GUILayout.Toggle(showCreateTexture, "Create texture", EditorStyles.foldout);
            if (showCreateTexture) DrawCreateTextureGUI();
            EditorGUILayout.EndVertical();
        }

        public static void DrawTexturePropsGUI(SerializedProperty pTexture, Map map)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            Object oldValue = pTexture.objectReferenceValue;
            EditorGUILayout.PropertyField(pTexture);
            bool changed = EditorGUI.EndChangeCheck();
            EditorUtils.HelpButton("The texture where the map will be drawn.\nImportant: must have Read / Write Enabled - ON.");
            EditorGUILayout.EndHorizontal();
            if (!changed) return;

            Texture2D texture = pTexture.objectReferenceValue as Texture2D;
            if (texture != null && (!Mathf.IsPowerOfTwo(texture.width) || !Mathf.IsPowerOfTwo(texture.height)))
            {
                EditorUtility.DisplayDialog("Error", "Texture width and height must be power of two!!!", "OK");
                pTexture.objectReferenceValue = oldValue;
            }
            else CheckAPITextureImporter(texture);

            texture = pTexture.objectReferenceValue as Texture2D;
            if (texture != null)
            {
                map.view.SetSize(texture.width, texture.height);
            }
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();
            
            rootLayoutItem.Create("Texture", () => DrawTexturePropsGUI(pTexture, map));
            rootLayoutItem.Create("Create Texture", DrawCreateTextureBlock);
        }

        private void UpdateTextureImporter(string texturePath)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!textureImporter) return;
            
            textureImporter.mipmapEnabled = false;
            textureImporter.isReadable = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.maxTextureSize = Mathf.Max(textureWidth, textureHeight);

            ControlBase control = map.GetComponent<ControlBase>();
            if (control is UIImageControl || control is SpriteRendererControl)
            {
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.npotScale = TextureImporterNPOTScale.None;
            }

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            Texture2D newTexture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
            pTexture.objectReferenceValue = newTexture;

            if (control is SpriteRendererControl)
            {
                SpriteRenderer spriteRenderer = map.GetComponent<SpriteRenderer>();
                spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Sprite)) as Sprite;
            }
            else if (control is UIImageControl)
            {
                Image img = map.GetComponent<Image>();
                img.sprite = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Sprite)) as Sprite;
            }
            else if (control is UIRawImageControl)
            {
                RawImage img = map.GetComponent<RawImage>();
                img.texture = newTexture;
            }
            else if (control is PlaneControl)
            {
                Renderer renderer = map.GetComponent<Renderer>();
                renderer.sharedMaterial.mainTexture = newTexture;
            }
            else if (control is NGUITextureControl)
            {
#if NGUI
                UITexture uiTexture = map.GetComponent<UITexture>();
                uiTexture.mainTexture = newTexture;
#endif
            }
        }
    }
}