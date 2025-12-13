/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public partial class MapEditor
    {
        private List<SavableItem> savableItems;
        private bool showSave;

        private void CacheRuntimeProperties()
        {
            savableItems = map.GetComponents<ISavable>()
                .SelectMany(Prefs.GetSavableItems)
                .OrderByDescending(s => s.priority)
                .ThenBy(s => s.label)
                .ToList();
        }
        
        private void CacheTiles()
        {
            lock (Tile.lockTiles)
            {
                string resPath = "Assets/Resources";
                if (pSource.enumValueIndex == (int) MapSource.StreamingAssetsAndOnline) resPath = Application.streamingAssetsPath;

                foreach (Tile tile in map.tileManager.tiles)
                {
                    if (tile.status != TileStatus.loaded) continue;

                    string tilePath = Path.Combine(resPath, tile.resourcesPath + ".png");
                    FileInfo info = new FileInfo(tilePath);
                    if (!info.Directory.Exists) info.Directory.Create();

                    if (!control.resultIsTexture)
                    {
                        if (tile.texture) File.WriteAllBytes(tilePath, tile.texture.EncodeToPNG());
                    }
                    else
                    {
                        RasterTile rasterTile = tile as RasterTile;
                        if (rasterTile != null && rasterTile.colors != null)
                        {
                            Texture2D texture = new Texture2D(Constants.TileSize, Constants.TileSize, TextureFormat.ARGB32, false);
                            texture.SetPixels32(rasterTile.colors);
                            texture.Apply();
                            File.WriteAllBytes(tilePath, texture.EncodeToPNG());
                        }
                    }
                }
            }
        }
        
        private void DrawCacheGUI()
        {
            if (pSource.enumValueIndex == (int)MapSource.Resources || pSource.enumValueIndex == (int)MapSource.StreamingAssets) return;

            string targetSource = "Resources";
            if (pSource.enumValueIndex == (int) MapSource.StreamingAssetsAndOnline) targetSource = "Streaming Assets";

            if (!GUILayout.Button("Cache tiles to " + targetSource)) return;

            TileSetControl tsControl = control as TileSetControl;
            if (tsControl && tsControl.compressTextures)
            {
                if (EditorUtility.DisplayDialog("Error", "To cache tiles, do the following:\n1.Enter to edit mode (stop the game).\n2.Tileset / Materials & Shaders / Compress Textures - OFF.\n3.Run the game and press the button again.\nAfter caching, you can enable texture compression again.", "Stop Game", "Cancel"))
                {
                    EditorApplication.isPlaying = false;
                }
                return;
            }

            CacheTiles();

            EditorPrefs.SetBool("OnlineMapsRefreshAssets", true);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Cache complete", $"Stop playback and select 'Source - {targetSource} And Online'.", "OK");

            isDirty = true;
        }
        
        private void DrawPlaymodeButtons()
        {
            if (!Utils.isPlaying) return;
            
            if (GUILayout.Button("Redraw")) map.Redraw();

            DrawCacheGUI();

            if (!showSave) 
            {
                if (GUILayout.Button("Save state"))
                {
                    showSave = true;
                    isDirty = true;
                }
            }
            else DrawSaveGUI();
        }
        
        private void DrawSaveGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Save state:");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", GUILayout.ExpandWidth(false))) foreach (SavableItem item in savableItems) item.enabled = true;
            if (GUILayout.Button("None", GUILayout.ExpandWidth(false))) foreach (SavableItem item in savableItems) item.enabled = false;
            EditorGUILayout.Space();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            foreach (SavableItem item in savableItems)
            {
                item.enabled = EditorGUILayout.Toggle(item.label, item.enabled);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save state")) TrySaveState();

            if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
            {
                showSave = false;
                isDirty = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        
        private void TrySaveState()
        {
            try
            {
                JSONObject obj = new JSONObject();
                foreach (SavableItem item in savableItems)
                {
                    if (!item.enabled) continue;
                    if (item.jsonCallback != null) obj.Add(item.name, item.jsonCallback());
                    if (item.invokeCallback != null) item.invokeCallback();
                }
                
                Prefs.Save(obj.ToString());
            }
            catch
            {
                // ignored
            }


            showSave = false;
            isDirty = true;
        }
    }
}