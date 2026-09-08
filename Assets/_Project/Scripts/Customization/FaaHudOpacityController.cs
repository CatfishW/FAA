using System;
using System.Collections.Generic;
using UnityEngine;

namespace FAA.Customization
{
    /// <summary>
    /// Pilot-facing opacity control for the flight symbology layer.
    ///
    /// HUD opacity is applied with CanvasGroup rather than rewriting every
    /// graphic color.  That keeps animated/vector instruments, TextMeshPro,
    /// and runtime-created elements in sync and makes a setting change
    /// reversible without losing each element's authored alpha.
    /// </summary>
    [AddComponentMenu("FAA/Customization/HUD Opacity Controller")]
    public sealed class FaaHudOpacityController : MonoBehaviour
    {
        public const float MinimumPilotOpacity = 0.35f;
        public const float MaximumPilotOpacity = 1f;

        private const string PreferenceKey = "faa.hud.opacity.v1";

        [Header("Pilot presentation")]
        [SerializeField, Range(MinimumPilotOpacity, MaximumPilotOpacity)]
        private float defaultOpacity = 0.82f;

        [Tooltip("HUD roots are discovered by name when this list is empty. Keep radar/map canvases out of this list so their independent opacity controls remain usable.")]
        [SerializeField] private List<Transform> hudRoots = new List<Transform>();

        [SerializeField] private bool persistPreference = true;
        [SerializeField] private bool includeHeadingTape = true;

        private readonly List<CanvasGroup> canvasGroups = new List<CanvasGroup>();
        private float opacity = 0.82f;
        private bool initialized;

        public event Action<float> OpacityChanged;

        public float Opacity => opacity;
        public int OpacityPercent => Mathf.RoundToInt(opacity * 100f);
        public string OpacityLabel => $"HUD {OpacityPercent}%";

        private void Awake()
        {
            ResolveHudRoots();
            float saved = persistPreference ? PlayerPrefs.GetFloat(PreferenceKey, defaultOpacity) : defaultOpacity;
            SetOpacity(saved, false);
            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                return;
            }

            ResolveHudRoots();
            ApplyOpacity();
        }

        /// <summary>
        /// Set opacity as a normalized value.  Values below the pilot-safe
        /// floor are clamped; use the separate HUD hide command when the
        /// pilot needs the symbology completely out of the way.
        /// </summary>
        public void SetOpacity(float value)
        {
            SetOpacity(value, true);
        }

        public void SetOpacityPercent(int percent)
        {
            SetOpacity(percent / 100f);
        }

        public void SetFullOpacity() => SetOpacity(1f);
        public void SetHighOpacity() => SetOpacity(0.8f);
        public void SetMediumOpacity() => SetOpacity(0.6f);
        public void SetLowOpacity() => SetOpacity(0.4f);

        public void IncreaseOpacity() => SetOpacity(opacity + 0.1f);
        public void DecreaseOpacity() => SetOpacity(opacity - 0.1f);

        public void RefreshTargets()
        {
            ResolveHudRoots();
            ApplyOpacity();
        }

        private void SetOpacity(float value, bool notify)
        {
            float clamped = Mathf.Clamp(value, MinimumPilotOpacity, MaximumPilotOpacity);
            bool changed = !Mathf.Approximately(opacity, clamped);
            opacity = clamped;
            ApplyOpacity();

            if (persistPreference && Application.isPlaying)
            {
                PlayerPrefs.SetFloat(PreferenceKey, opacity);
                PlayerPrefs.Save();
            }

            if (notify && changed)
            {
                OpacityChanged?.Invoke(opacity);
            }
        }

        private void ApplyOpacity()
        {
            for (int i = canvasGroups.Count - 1; i >= 0; i--)
            {
                CanvasGroup group = canvasGroups[i];
                if (group == null)
                {
                    canvasGroups.RemoveAt(i);
                    continue;
                }

                group.alpha = opacity;
            }
        }

        private void ResolveHudRoots()
        {
            canvasGroups.Clear();

            if (hudRoots == null)
            {
                hudRoots = new List<Transform>();
            }

            if (hudRoots.Count == 0)
            {
                AddNamedRoot("Second Interation GUI");
                if (includeHeadingTape)
                {
                    AddNamedRoot("FAAHeadingTapeCanvas");
                }
            }

            // De-duplicate nested roots: applying the same alpha twice makes
            // the HUD unexpectedly dim when a scene has both a canvas and a
            // screen-HUD child in the serialized list.
            var uniqueRoots = new HashSet<Transform>();
            foreach (Transform root in hudRoots)
            {
                if (root == null || !uniqueRoots.Add(root))
                {
                    continue;
                }

                bool nested = false;
                foreach (Transform other in uniqueRoots)
                {
                    if (other != root && root.IsChildOf(other))
                    {
                        nested = true;
                        break;
                    }
                }

                if (nested)
                {
                    continue;
                }

                CanvasGroup group = root.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = root.gameObject.AddComponent<CanvasGroup>();
                }

                group.interactable = false;
                group.blocksRaycasts = false;
                canvasGroups.Add(group);
            }
        }

        private void AddNamedRoot(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                {
                    hudRoots.Add(candidate);
                    return;
                }
            }
        }
    }
}
