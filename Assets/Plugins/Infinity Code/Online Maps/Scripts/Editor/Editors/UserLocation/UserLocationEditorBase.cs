/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public abstract class UserLocationEditorBase : Editor
    {
        private static GUIStyle _toggleStyle;
    
        private Map _map;
        private CameraOrbit cameraOrbit;
    
        private bool showCommonIssues = false;
        private bool showCreateMarker = true;
        private bool showGPSEmulator = true;
        private bool showUpdateLocation = true;
        private bool selectLocationOnMap = false;

        #region Serialized Properties

        private SerializedProperty pCreateMarkerInUserLocation;
        private SerializedProperty pMarkerType;
        private SerializedProperty pMarker3DPrefab;
        private SerializedProperty pMarker3DSizeType;
        private SerializedProperty pMarker2DTexture;
        private SerializedProperty pMarker2DAlign;
        private SerializedProperty pMarkerScale;
        private SerializedProperty pMarkerTooltip;
        private SerializedProperty pUseCompassForMarker;
        private SerializedProperty pUseGPSEmulator;
        private SerializedProperty pEmulatedCompass;
        private SerializedProperty pEmulatedLocation;
        private SerializedProperty pCompassThreshold;
        private SerializedProperty pFindLocationByIP;
        private SerializedProperty pUpdateLocation;
        private SerializedProperty pAutoStopUpdateOnInput;
        private SerializedProperty pRestoreAfter;
        private SerializedProperty pDisableEmulatorInPublish;
        private SerializedProperty pLerpCompassValue;
        private SerializedProperty pRotateCameraByCompass;
        private SerializedProperty pUpdateEmulatedLocationByMarker;

        #endregion

        protected Map map
        {
            get
            {
                if (!_map)
                {
                    _map = (target as UserLocation).GetComponent<Map>();
                    if (!_map) _map = Map.instance;
                }

                return _map;
            }
        }

        public virtual float minLabelWidth => 170;

        private static GUIStyle toggleStyle
        {
            get
            {
                if (_toggleStyle == null)
                {
                    _toggleStyle = new GUIStyle(GUI.skin.toggle);
                    _toggleStyle.margin.top = 0;
                }
                return _toggleStyle;
            }
        }

        protected virtual void CacheSerializedProperties()
        {
            pCreateMarkerInUserLocation = serializedObject.FindProperty("createMarkerInUserLocation");
            pMarkerType = serializedObject.FindProperty("markerType");
            pMarker3DPrefab = serializedObject.FindProperty("marker3DPrefab");
            pMarker3DSizeType = serializedObject.FindProperty("marker3DSizeType");
            pMarker2DTexture = serializedObject.FindProperty("marker2DTexture");
            pMarker2DAlign = serializedObject.FindProperty("marker2DAlign");
            pMarkerScale = serializedObject.FindProperty("markerScale");
            pMarkerTooltip = serializedObject.FindProperty("markerTooltip");
            pUseCompassForMarker = serializedObject.FindProperty("useCompassForMarker");
            pUseGPSEmulator = serializedObject.FindProperty("useGPSEmulator");
            pDisableEmulatorInPublish = serializedObject.FindProperty("disableEmulatorInPublish");
            pEmulatedLocation = serializedObject.FindProperty("emulatedLocation");
            pEmulatedCompass = serializedObject.FindProperty("emulatedCompass");
            pCompassThreshold = serializedObject.FindProperty("compassThreshold");
            pFindLocationByIP = serializedObject.FindProperty("findLocationByIP");
            pUpdateLocation = serializedObject.FindProperty("updateLocation");
            pAutoStopUpdateOnInput = serializedObject.FindProperty("autoStopUpdateOnInput");
            pRestoreAfter = serializedObject.FindProperty("restoreAfter");
            pLerpCompassValue = serializedObject.FindProperty("lerpCompassValue");
            pRotateCameraByCompass = serializedObject.FindProperty("rotateCameraByCompass");
            pUpdateEmulatedLocationByMarker = serializedObject.FindProperty("updateEmulatedLocationByMarker");

            cameraOrbit = (target as UserLocationBase).GetComponent<CameraOrbit>();
        }

        public virtual void CustomInspectorGUI()
        {
        }

        public virtual void CustomUpdateLocationGUI()
        {
        }

        private void DrawRestoreAfter()
        {
            bool restoreAfter = pRestoreAfter.intValue != 0;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            restoreAfter = GUILayout.Toggle(restoreAfter, "", GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck()) pRestoreAfter.intValue = restoreAfter ? 10 : 0;
            EditorGUI.BeginDisabledGroup(!restoreAfter);
            float labelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth -= 20;
                EditorGUILayout.PropertyField(pRestoreAfter, TempContent.Get("Restore After (sec)"));
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void OnCommonIssues()
        {
            bool lastShowCommonIssues = showCommonIssues;
            if (lastShowCommonIssues)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
            }
            showCommonIssues = GUILayout.Toggle(showCommonIssues, "Common Issues", EditorStyles.foldout);

            if (lastShowCommonIssues) EditorGUILayout.EndHorizontal();

            if (showCommonIssues)
            {
                EditorGUILayout.LabelField("The most common causes of problems on devices:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("- Application does not have a permission for accessing GPS sensor.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("- The user did not give the application permission to use GPS sensor.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("- GPS is disabled on the device.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("- The user tries to use the application inside a building.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("- AGPS is disabled or not supported on the device, and the user did not wait for GPS initialization (cold start - up to two minutes).", EditorStyles.wordWrappedLabel);
            }

            if (lastShowCommonIssues) EditorGUILayout.EndVertical();
        }

        private void OnCreateMarkerGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            bool createMarker = pCreateMarkerInUserLocation.boolValue;
            if (createMarker)
            {
                EditorGUILayout.BeginHorizontal();
                showCreateMarker = GUILayout.Toggle(showCreateMarker, "", EditorStyles.foldout, GUILayout.ExpandWidth(false), GUILayout.Height(16));
            }

            pCreateMarkerInUserLocation.boolValue = GUILayout.Toggle(pCreateMarkerInUserLocation.boolValue, "Create Marker", toggleStyle);

            if (createMarker) EditorGUILayout.EndHorizontal();

            if (pCreateMarkerInUserLocation.boolValue && showCreateMarker)
            {
                pMarkerType.enumValueIndex = EditorGUILayout.Popup("Type", pMarkerType.enumValueIndex, new[] { "2D", "3D" });

                if (pMarkerType.enumValueIndex == (int) UserLocationMarkerType.threeD)
                {
                    EditorGUILayout.PropertyField(pMarker3DPrefab, TempContent.Get("Prefab"));
                    EditorGUILayout.PropertyField(pMarker3DSizeType, TempContent.Get("Size Type"));
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(pMarker2DTexture, TempContent.Get("Texture"));
                    if (EditorGUI.EndChangeCheck() && pMarker2DTexture.objectReferenceValue) EditorUtils.CheckMarkerTextureImporter(pMarker2DTexture);
                    EditorGUILayout.PropertyField(pMarker2DAlign, TempContent.Get("Align"));
                }

                EditorGUILayout.PropertyField(pMarkerTooltip, TempContent.Get("Tooltip"));
                EditorGUILayout.PropertyField(pMarkerScale, TempContent.Get("Scale"));
                EditorGUILayout.PropertyField(pUseCompassForMarker, TempContent.Get("Use Compass"));
            }

            EditorGUILayout.EndVertical();
        }

        private void OnEnable()
        {
            CacheSerializedProperties();
        }

        private void OnGPSEmulatorGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            bool useGPSEmulator = pUseGPSEmulator.boolValue;
            if (useGPSEmulator)
            {
                EditorGUILayout.BeginHorizontal();
                showGPSEmulator = GUILayout.Toggle(showGPSEmulator, "", EditorStyles.foldout, GUILayout.ExpandWidth(false));
            }

            pUseGPSEmulator.boolValue = GUILayout.Toggle(pUseGPSEmulator.boolValue, "Use GPS Emulator", toggleStyle);

            if (useGPSEmulator) EditorGUILayout.EndHorizontal();

            if (pUseGPSEmulator.boolValue && showGPSEmulator)
            {
                EditorGUILayout.PropertyField(pDisableEmulatorInPublish);
                if (pDisableEmulatorInPublish.boolValue) EditorGUILayout.HelpBox("The emulator is automatically disabled on the devices and use the data from the sensors.", MessageType.Info);

                if (GUILayout.Button("Copy Location From Map"))
                {
                    if (map)
                    {
                        (target as UserLocationBase).emulatedLocation = map.view.center;
                        serializedObject.Update();
                    }
                }

                if (EditorApplication.isPlaying)
                {
                    OnSelectLocationGUI();
                }

                if (pCreateMarkerInUserLocation.boolValue)
                {
                    GUIContent content = TempContent.Get("Update From Marker Location");
                    EditorGUILayout.PropertyField(pUpdateEmulatedLocationByMarker, content);
                }

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(pEmulatedLocation);
                EditorGUILayout.PropertyField(pEmulatedCompass, TempContent.Get("Compass (0-360)"));
            }

            EditorGUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            float labelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth < minLabelWidth) EditorGUIUtility.labelWidth = minLabelWidth;

            try
            {
                EditorGUI.BeginChangeCheck();
                CustomInspectorGUI();

                EditorGUILayout.PropertyField(pCompassThreshold);
                EditorGUILayout.PropertyField(pLerpCompassValue);

                if (cameraOrbit) EditorGUILayout.PropertyField(pRotateCameraByCompass);

                EditorGUILayout.PropertyField(pFindLocationByIP);

                OnUpdateLocationGUI();
                OnCreateMarkerGUI();
                OnGPSEmulatorGUI();

                serializedObject.ApplyModifiedProperties();

                OnCommonIssues();

                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(target);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        private void OnSelectLocationGUI()
        {
            if (!selectLocationOnMap)
            {
                if (GUILayout.Button("Select Location On Map"))
                {
                    selectLocationOnMap = true;
                    map.control.OnClick -= SelectLocationOnMap;
                    map.control.OnClick += SelectLocationOnMap;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click on the map to select the location.", MessageType.Info);
            
                if (GUILayout.Button("Cancel"))
                {
                    selectLocationOnMap = false;
                    map.control.OnClick -= SelectLocationOnMap;
                }
            }
        }

        private void OnUpdateLocationGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            bool updateLocation = pUpdateLocation.boolValue;
            if (updateLocation)
            {
                EditorGUILayout.BeginHorizontal();
                showUpdateLocation = GUILayout.Toggle(showUpdateLocation, "", EditorStyles.foldout, GUILayout.ExpandWidth(false), GUILayout.Height(16));
            }

            pUpdateLocation.boolValue = GUILayout.Toggle(pUpdateLocation.boolValue, "Update Map Location", toggleStyle);

            if (updateLocation) EditorGUILayout.EndHorizontal();

            if (pUpdateLocation.boolValue && showUpdateLocation)
            {
                CustomUpdateLocationGUI();
                EditorGUILayout.PropertyField(pAutoStopUpdateOnInput, TempContent.Get("Auto Stop On Input"));
                DrawRestoreAfter();
            }

            EditorGUILayout.EndVertical();
        }

        private void SelectLocationOnMap()
        {
            selectLocationOnMap = false;
            map.control.OnClick -= SelectLocationOnMap;
            serializedObject.Update();
            pEmulatedLocation.vector2Value = map.control.ScreenToLocation();
            serializedObject.ApplyModifiedProperties();
        }
    }
}