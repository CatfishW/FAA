using System;
using System.Collections.Generic;
using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Core
{
    /// <summary>
    /// Main HUD Controller that orchestrates all HUD elements.
    /// Subscribes to AircraftController and distributes state updates to registered elements.
    /// </summary>
    [AddComponentMenu("HUD Control/HUD Controller")]
    public class HUDController : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Data Source")]
        [Tooltip("Reference to the aircraft controller providing flight data")]
        [SerializeField] private AircraftController aircraftController;
        
        [Tooltip("Auto-find AircraftController in scene if not assigned")]
        [SerializeField] private bool autoFindController = true;
        
        [Header("Update Settings")]
        [Tooltip("Update elements every frame (true) or only on state change events (false)")]
        [SerializeField] private bool updateEveryFrame = true;
        
        [Tooltip("Enable all elements on start")]
        [SerializeField] private bool enableOnStart = true;
        
        [Header("Elements")]
        [Tooltip("List of HUD elements to manage (auto-populated by Setup Wizard)")]
        [SerializeField] private List<HUDElementBase> elements = new List<HUDElementBase>();
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly Dictionary<string, IHUDElement> elementLookup = new Dictionary<string, IHUDElement>();
        private AircraftState currentState;
        private bool isInitialized = false;
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// Current aircraft state being displayed
        /// </summary>
        public AircraftState CurrentState => currentState;
        
        /// <summary>
        /// Number of registered elements
        /// </summary>
        public int ElementCount => elements.Count;
        
        /// <summary>
        /// Whether the controller is initialized and running
        /// </summary>
        public bool IsInitialized => isInitialized;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when HUD visibility changes
        /// </summary>
        public event Action<bool> OnVisibilityChanged;
        
        /// <summary>
        /// Fired when an element is registered
        /// </summary>
        public event Action<IHUDElement> OnElementRegistered;
        
        /// <summary>
        /// Fired when an element is unregistered
        /// </summary>
        public event Action<IHUDElement> OnElementUnregistered;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            BuildElementLookup();
        }
        
        private void Start()
        {
            Initialize();
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            if (updateEveryFrame && aircraftController != null)
            {
                currentState = aircraftController.State;
                UpdateAllElements(currentState);
            }
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromController();
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the HUD controller
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            
            // Find controller if needed
            if (aircraftController == null && autoFindController)
            {
                aircraftController = FindObjectOfType<AircraftController>();
                
                if (aircraftController == null)
                {
                    Debug.LogWarning("[HUDController] No AircraftController found in scene!");
                }
            }
            
            // Subscribe to state changes
            SubscribeToController();
            
            // Initialize all elements
            InitializeElements();
            
            // Set initial visibility
            if (enableOnStart)
            {
                SetAllEnabled(true);
            }
            
            isInitialized = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[HUDController] Initialized with {elements.Count} elements");
            }
        }
        
        private void SubscribeToController()
        {
            if (aircraftController != null)
            {
                aircraftController.OnStateChanged += OnAircraftStateChanged;
            }
        }
        
        private void UnsubscribeFromController()
        {
            if (aircraftController != null)
            {
                aircraftController.OnStateChanged -= OnAircraftStateChanged;
            }
        }
        
        private void InitializeElements()
        {
            foreach (var element in elements)
            {
                if (element != null)
                {
                    element.Initialize();
                }
            }
        }
        
        private void BuildElementLookup()
        {
            elementLookup.Clear();
            
            foreach (var element in elements)
            {
                if (element != null && !string.IsNullOrEmpty(element.ElementId))
                {
                    elementLookup[element.ElementId] = element;
                }
            }
        }
        
        #endregion
        
        #region State Updates
        
        private void OnAircraftStateChanged(AircraftState state)
        {
            currentState = state;
            
            // Only update here if not using frame-based updates
            if (!updateEveryFrame)
            {
                UpdateAllElements(state);
            }
        }
        
        private void UpdateAllElements(AircraftState state)
        {
            if (state == null) return;
            
            foreach (var element in elements)
            {
                if (element != null && element.IsEnabled)
                {
                    element.UpdateElement(state);
                }
            }
        }
        
        #endregion
        
        #region Element Management
        
        /// <summary>
        /// Register a new HUD element
        /// </summary>
        public void RegisterElement(HUDElementBase element)
        {
            if (element == null) return;
            
            if (!elements.Contains(element))
            {
                elements.Add(element);
                elementLookup[element.ElementId] = element;
                
                if (isInitialized)
                {
                    element.Initialize();
                }
                
                OnElementRegistered?.Invoke(element);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[HUDController] Registered element: {element.ElementId}");
                }
            }
        }
        
        /// <summary>
        /// Unregister a HUD element
        /// </summary>
        public void UnregisterElement(HUDElementBase element)
        {
            if (element == null) return;
            
            if (elements.Remove(element))
            {
                elementLookup.Remove(element.ElementId);
                OnElementUnregistered?.Invoke(element);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[HUDController] Unregistered element: {element.ElementId}");
                }
            }
        }
        
        /// <summary>
        /// Get element by ID
        /// </summary>
        public IHUDElement GetElement(string elementId)
        {
            elementLookup.TryGetValue(elementId, out var element);
            return element;
        }
        
        /// <summary>
        /// Get typed element by ID
        /// </summary>
        public T GetElement<T>(string elementId) where T : class, IHUDElement
        {
            return GetElement(elementId) as T;
        }
        
        /// <summary>
        /// Get first element of type
        /// </summary>
        public T GetElementOfType<T>() where T : HUDElementBase
        {
            foreach (var element in elements)
            {
                if (element is T typed)
                {
                    return typed;
                }
            }
            return null;
        }
        
        #endregion
        
        #region Visibility Control
        
        /// <summary>
        /// Enable or disable all elements
        /// </summary>
        public void SetAllEnabled(bool enabled)
        {
            foreach (var element in elements)
            {
                if (element != null)
                {
                    element.SetEnabled(enabled);
                }
            }
            
            OnVisibilityChanged?.Invoke(enabled);
        }
        
        /// <summary>
        /// Enable or disable specific element by ID
        /// </summary>
        public void SetElementEnabled(string elementId, bool enabled)
        {
            var element = GetElement(elementId);
            element?.SetEnabled(enabled);
        }
        
        /// <summary>
        /// Toggle element visibility by ID
        /// </summary>
        public void ToggleElement(string elementId)
        {
            var element = GetElement(elementId);
            if (element != null)
            {
                element.SetEnabled(!element.IsEnabled);
            }
        }
        
        /// <summary>
        /// Show all elements
        /// </summary>
        public void ShowAll() => SetAllEnabled(true);
        
        /// <summary>
        /// Hide all elements
        /// </summary>
        public void HideAll() => SetAllEnabled(false);
        
        #endregion
        
        #region Manual Data Injection
        
        /// <summary>
        /// Manually update with specific state (for testing or external data)
        /// </summary>
        public void InjectState(AircraftState state)
        {
            currentState = state;
            UpdateAllElements(state);
        }
        
        /// <summary>
        /// Set aircraft controller reference
        /// </summary>
        public void SetAircraftController(AircraftController controller)
        {
            UnsubscribeFromController();
            aircraftController = controller;
            SubscribeToController();
        }
        
        #endregion
        
        #region Editor Helpers
        
#if UNITY_EDITOR
        [ContextMenu("Find All Elements In Children")]
        private void FindAllElementsInChildren()
        {
            elements.Clear();
            elements.AddRange(GetComponentsInChildren<HUDElementBase>(true));
            BuildElementLookup();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[HUDController] Found {elements.Count} elements");
        }
        
        [ContextMenu("Find Aircraft Controller")]
        private void FindAircraftController()
        {
            aircraftController = FindObjectOfType<AircraftController>();
            if (aircraftController != null)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[HUDController] Found AircraftController: {aircraftController.name}");
            }
            else
            {
                Debug.LogWarning("[HUDController] No AircraftController found in scene");
            }
        }
        
        private void OnValidate()
        {
            BuildElementLookup();
        }
#endif
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== HUD Controller ===");
            GUILayout.Label($"Elements: {elements.Count}");
            GUILayout.Label($"Controller: {(aircraftController != null ? "Connected" : "Missing")}");
            
            if (currentState != null)
            {
                GUILayout.Label($"Heading: {currentState.Heading:F0}°");
                GUILayout.Label($"Pitch: {currentState.Pitch:F1}° | Roll: {currentState.Roll:F1}°");
                GUILayout.Label($"IAS: {currentState.IndicatedAirspeedKnots:F0} kts");
                GUILayout.Label($"Alt: {currentState.AltitudeFeet:F0} ft");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
