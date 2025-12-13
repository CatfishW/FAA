using UnityEngine;
using UnityEngine.UI;

namespace FAA.Utilities
{
    /// <summary>
    /// Automatically removes any material attached to an Image component.
    /// This script monitors the Image component and clears the material property
    /// whenever a material is detected.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteInEditMode]
    public class ImageMaterialRemover : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Enable to continuously check and remove materials at runtime.")]
        [SerializeField] private bool _monitorAtRuntime = true;

        [Tooltip("Enable to log when a material is removed.")]
        [SerializeField] private bool _logRemovals = true;

        [Header("Debug Info")]
        [SerializeField, ReadOnly] private int _removalCount = 0;

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            CheckAndRemoveMaterial();
        }

        private void Start()
        {
            CheckAndRemoveMaterial();
        }

        private void Update()
        {
            // In edit mode, always check
            // At runtime, only check if monitoring is enabled
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                CheckAndRemoveMaterial();
                return;
            }
#endif
            if (_monitorAtRuntime)
            {
                CheckAndRemoveMaterial();
            }
        }

        private void OnValidate()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            CheckAndRemoveMaterial();
        }

        /// <summary>
        /// Checks if a material is attached to the Image component and removes it.
        /// </summary>
        public void CheckAndRemoveMaterial()
        {
            if (_image == null)
                return;

            if (_image.material != null && _image.material != _image.defaultMaterial)
            {
                string materialName = _image.material.name;
                _image.material = null;
                _removalCount++;

                if (_logRemovals)
                {
                    Debug.Log($"[ImageMaterialRemover] Removed material '{materialName}' from Image on '{gameObject.name}'. Total removals: {_removalCount}", this);
                }
            }
        }

        /// <summary>
        /// Manually trigger material removal check.
        /// </summary>
        [ContextMenu("Force Remove Material")]
        public void ForceRemoveMaterial()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            if (_image.material != null)
            {
                string materialName = _image.material.name;
                _image.material = null;
                _removalCount++;
                Debug.Log($"[ImageMaterialRemover] Force removed material '{materialName}' from Image on '{gameObject.name}'.", this);
            }
            else
            {
                Debug.Log($"[ImageMaterialRemover] No material to remove on '{gameObject.name}'.", this);
            }
        }

        /// <summary>
        /// Reset the removal counter.
        /// </summary>
        [ContextMenu("Reset Removal Count")]
        public void ResetRemovalCount()
        {
            _removalCount = 0;
        }
    }

    /// <summary>
    /// ReadOnly attribute for displaying fields in the inspector without allowing edits.
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }
    }
#endif
}
