/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof (TileSetControl), true)]
    public class TileSetControlEditor : ControlBaseDynamicMeshEditor<TileSetControl>
    {
        protected override float minLabelWidth => 180;

        private SerializedProperty checkMarker2DVisibility;
        private SerializedProperty colliderType;
        private SerializedProperty compressTextures;
        private SerializedProperty drawingShader;
        private SerializedProperty elevationResolution;
        private SerializedProperty markerMaterial;
        private SerializedProperty markerShader;
        private SerializedProperty mipmapForTiles;
        private SerializedProperty overlayFromParentTiles;
        private SerializedProperty sizeInScene;
        private SerializedProperty tileMaterial;
        private SerializedProperty tilesetShader;
        private SerializedProperty width;
        private SerializedProperty height;
        
        private GUIContent cWidth;
        private GUIContent cHeight;

        private Shader defaultTilesetShader;
        private Shader defaultTilesetShader2;
        private bool showShaders;

        protected override void CacheSerializedFields()
        {
            base.CacheSerializedFields();

            checkMarker2DVisibility = serializedObject.FindProperty("checkMarker2DVisibility");
            tileMaterial = serializedObject.FindProperty("tileMaterial");
            markerMaterial = serializedObject.FindProperty("markerMaterial");
            tilesetShader = serializedObject.FindProperty("tilesetShader");
            markerShader = serializedObject.FindProperty("markerShader");
            drawingShader = serializedObject.FindProperty("drawingShader");
            colliderType = serializedObject.FindProperty("colliderType");
            mipmapForTiles = serializedObject.FindProperty("_mipmapForTiles");
            sizeInScene = serializedObject.FindProperty("sizeInScene");
            compressTextures = serializedObject.FindProperty("compressTextures");
            elevationResolution = serializedObject.FindProperty("elevationResolution");
            overlayFromParentTiles = serializedObject.FindProperty("overlayFromParentTiles");
            
            width = serializedObject.FindProperty("_width");
            height = serializedObject.FindProperty("_height");

            cWidth = new GUIContent("Width (pixels)", "Width of the map. It works as a resolution.\nImportant: the map must have a side size of N * 256.");
            cHeight = new GUIContent("Height (pixels)", "Height of the map. It works as a resolution.\nImportant: the map must have a side size of N * 256.");
        }

        private void CheckCameraDistance()
        {
            if (EditorApplication.isPlaying) return;

            Camera tsCamera = pActiveCamera.objectReferenceValue ? pActiveCamera.objectReferenceValue as Camera : Camera.main;

            if (!tsCamera) return;

            Vector3 mapCenter = map.transform.position + control.center;
            float distance = (tsCamera.transform.position - mapCenter).magnitude * 3f;
            if (distance <= tsCamera.farClipPlane) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Potential problem detected:\n\"Camera - Clipping Planes - Far\" is too small.", MessageType.Warning);

            if (GUILayout.Button("Fix Clipping Planes - Far")) tsCamera.farClipPlane = distance;

            EditorGUILayout.EndVertical();
        }

        private void CheckColliderType()
        {
            if (EditorApplication.isPlaying) return;

            if (colliderType.enumValueIndex != (int) ColliderType.box && colliderType.enumValueIndex != (int) ColliderType.flatBox) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Potential problem detected:\nWhen using BoxCollider, can be a problem in interaction with a map with elevation.", MessageType.Warning);
            if (GUILayout.Button("Set Collider Type - Full Mesh")) colliderType.enumValueIndex = (int) ColliderType.fullMesh;

            EditorGUILayout.EndVertical();
        }

        private void CheckSRP()
        {
            if (EditorApplication.isPlaying) return;

            if (!Compatibility.GetRenderPipelineAsset()) return;
            bool wrongTileset = tilesetShader.objectReferenceValue == defaultTilesetShader || tilesetShader.objectReferenceValue == defaultTilesetShader2;
            bool wrongMarker = markerShader.objectReferenceValue && (markerShader.objectReferenceValue as Shader).name == "Legacy Shaders/Transparent/Diffuse";
            bool wrongDrawing = drawingShader.objectReferenceValue && (drawingShader.objectReferenceValue as Shader).name == "Infinity Code/Online Maps/Tileset DrawingElement";

            if (!wrongTileset && !wrongMarker && !wrongDrawing) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Potential problem detected:\nUsed Scriptable Render Pipeline with a standard shader. The map may not be displayed correctly.", MessageType.Warning);
            if (GUILayout.Button("Fix")) FixSRP(wrongTileset, wrongMarker, wrongDrawing);
            EditorGUILayout.EndVertical();
        }

        private void DrawElevationResolution()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(elevationResolution, TempContent.Get("Displayed Elevation Resolution"));
            if (EditorGUI.EndChangeCheck()) elevationResolution.intValue = Mathf.Clamp(elevationResolution.intValue, 16, 128);
        }

        private void DrawMoveCamera()
        {
            if (!GUILayout.Button("Move camera to center of TileSet")) return;
            if (!map) return;

            Camera tsCamera = pActiveCamera.objectReferenceValue != null ? pActiveCamera.objectReferenceValue as Camera : Camera.main;

            if (!tsCamera)
            {
                Debug.Log("Camera is null");
                return;
            }

            GameObject go = tsCamera.gameObject;
            float minSide = Mathf.Min(control.sizeInScene.x * map.transform.lossyScale.x, control.sizeInScene.y * map.transform.lossyScale.z);
            Vector3 position = map.transform.position + map.transform.rotation * new Vector3(control.sizeInScene.x / -2 * map.transform.lossyScale.x, minSide, control.sizeInScene.y / 2 * map.transform.lossyScale.z);
            go.transform.position = position;
            go.transform.rotation = map.transform.rotation * Quaternion.Euler(90, 180, 0);
        }

        private void DrawSize()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            EditorGUILayout.PropertyField(width, cWidth);
            EditorGUILayout.PropertyField(height, cHeight);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(16));
            GUILayout.Space(10);
            EditorUtils.HelpButton("Width / height of the map. It works as a resolution.\nImportant: the map must have a side size of N * 256.");
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            int dts = Constants.TileSize * 2;

            if (width.intValue % 256 != 0)
            {
                EditorGUILayout.HelpBox("Width is not equal to 256 * N, the map will not work correctly.", MessageType.Error);
                if (GUILayout.Button("Fix Width")) width.intValue = Mathf.NextPowerOfTwo(width.intValue);
            }
            else if (height.intValue % 256 != 0)
            {
                EditorGUILayout.HelpBox("Height is not equal to 256 * N, the map will not work correctly.", MessageType.Error);
                if (GUILayout.Button("Fix Height")) height.intValue = Mathf.NextPowerOfTwo(height.intValue);
            }

            if (width.intValue < 256) width.intValue = dts;
            if (height.intValue < 256) height.intValue = dts;
            EditorGUILayout.Space(2);
        }

        private void FixSRP(bool wrongTileset, bool wrongMarker, bool wrongDrawing)
        {
            if (wrongTileset)
            {
                string[] assets = AssetDatabase.FindAssets("TilesetPBRShader");
                if (assets.Length > 0) tilesetShader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            }
            if (wrongMarker)
            {
                string[] assets = AssetDatabase.FindAssets("TilesetPBRMarkerShader");
                if (assets.Length > 0) markerShader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            }
            if (wrongDrawing)
            {
                string[] assets = AssetDatabase.FindAssets("TilesetPBRDrawingElement");
                if (assets.Length > 0) drawingShader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            }
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            warningLayoutItem.Create("checkCameraDistance", CheckCameraDistance);

            rootLayoutItem.Create("size", DrawSize).priority = -2;
            rootLayoutItem.Create(sizeInScene).priority = -1;
            rootLayoutItem["marker2DMode"].Create(checkMarker2DVisibility).OnValidateDraw += () => pMarker2DMode.enumValueIndex == (int)Marker2DMode.flat;
            rootLayoutItem.Create(colliderType).disabledInPlaymode = true;
            rootLayoutItem.Create("colliderWarning", CheckColliderType);
            rootLayoutItem.Create("SRPWarning", CheckSRP).priority = -2;
            rootLayoutItem.Create(overlayFromParentTiles);
            rootLayoutItem.Create("elevationResolution", DrawElevationResolution);

            GenerateMaterialsLayout();

            rootLayoutItem.Create("moveCamera", DrawMoveCamera);
        }

        private void GenerateMaterialsLayout()
        {
            LayoutItem mats = rootLayoutItem.Create("materialsAndShaders");
            mats.drawGroup = LayoutItem.Group.validated;
            mats.drawGroupBorder = true;
            mats.OnValidateDrawChildren += () => showShaders;
            mats.action += () => { showShaders = GUILayout.Toggle(showShaders, "Materials & Shaders", EditorStyles.foldout); };
            mats.Create(tileMaterial);
            mats.Create(markerMaterial);
            mats.Create(tilesetShader).OnChanged += () =>
            {
                if (!tilesetShader.objectReferenceValue) tilesetShader.objectReferenceValue = defaultTilesetShader;
            };
            mats.Create(markerShader).OnChanged += () =>
            {
                if (!markerShader.objectReferenceValue) markerShader.objectReferenceValue = RenderPipelineHelper.GetTransparentShader();
            };
            mats.Create(drawingShader).OnChanged += () =>
            {
                if (!drawingShader.objectReferenceValue) drawingShader.objectReferenceValue = Shader.Find("Infinity Code/Online Maps/Tileset DrawingElement");
            };
            mats.Create(mipmapForTiles);
            mats.Create(compressTextures);
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            defaultTilesetShader = Shader.Find("Infinity Code/Online Maps/Tileset Cutout");
            defaultTilesetShader2 = Shader.Find("Infinity Code/Online Maps/Tileset");

            if (!tilesetShader.objectReferenceValue) tilesetShader.objectReferenceValue = defaultTilesetShader;
            if (!markerShader.objectReferenceValue) markerShader.objectReferenceValue = RenderPipelineHelper.GetTransparentShader();
            if (!drawingShader.objectReferenceValue) drawingShader.objectReferenceValue = Shader.Find("Infinity Code/Online Maps/Tileset DrawingElement");
        }

        private void OnSceneGUI()
        {
            if (Utils.isPlaying) return;
            
            Quaternion rotation = control.transform.rotation;
            Vector3 scale = control.transform.lossyScale;
            Vector2 size = control.sizeInScene;
            
            Vector3[] points = new Vector3[5];
            points[0] = points[4] = control.transform.position;
            points[1] = points[0] + rotation * new Vector3(-size.x * scale.x, 0, 0);
            points[2] = points[1] + rotation * new Vector3(0, 0, size.y * scale.z);
            points[3] = points[0] + rotation * new Vector3(0, 0, size.y * scale.z);
            Handles.DrawSolidRectangleWithOutline(points, new Color(1, 1, 1, 0.3f), Color.black);

            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = {textColor = Color.black}
            };

            Vector3 localCenter = new Vector3(size.x / -2 * scale.x, 0, size.y / 2 * scale.z);
            Handles.Label(points[0] + rotation * localCenter, "Tileset Map", style);
        }
    }
}