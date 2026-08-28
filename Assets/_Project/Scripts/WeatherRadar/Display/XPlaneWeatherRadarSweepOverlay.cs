using UnityEngine;
using UnityEngine.UI;

namespace WeatherRadar
{
    /// <summary>
    /// Lightweight phosphor sweep drawn over the unmodified X-Plane weather image.
    /// The effect is visual only: it never creates, recolors, or delays radar returns.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Weather Radar/Display/X-Plane Weather Radar Sweep Overlay")]
    public sealed class XPlaneWeatherRadarSweepOverlay : MonoBehaviour
    {
        private const string SweepShaderResourcePath = "Shaders/XPlaneWeatherRadarSweep";

        [Header("References")]
        [SerializeField] private RawImage overlayImage;
        [SerializeField] private XPlaneOriginalWeatherRadarDisplay sourceDisplay;
        [SerializeField] private WeatherRadarDataProvider dataProvider;

        [Header("Sweep")]
        [SerializeField, Min(0.5f)] private float roundTripSeconds = 3.7f;
        [SerializeField, Range(35f, 85f)] private float sectorHalfAngleDegrees = 55f;
        [SerializeField, Range(0f, 0.2f)] private float originHeightRatio = 0.07f;
        [SerializeField, Range(0.4f, 1f)] private float outerRadius = 0.86f;
        [SerializeField, Range(0.25f, 4f)] private float beamWidthDegrees = 0.85f;
        [SerializeField, Range(1f, 12f)] private float glowWidthDegrees = 3f;
        [SerializeField, Range(4f, 40f)] private float trailWidthDegrees = 12f;
        [SerializeField, Range(0f, 1f)] private float trailStrength = 0.18f;
        [SerializeField] private Color sweepColor = new Color(0.12f, 1f, 0.52f, 0.68f);

        private Material _material;
        private float _currentScanAngle;

        public float CurrentScanAngle => _currentScanAngle;
        public RawImage OverlayImage => overlayImage;

        private void Awake()
        {
            AutoFindReferences();
            EnsureMaterial();
            MatchSourceRect();
        }

        private void OnEnable()
        {
            AutoFindReferences();
            EnsureMaterial();
            MatchSourceRect();
            UpdateSweepVisual();
        }

        private void Update()
        {
            if (overlayImage == null || sourceDisplay == null || dataProvider == null)
            {
                AutoFindReferences();
            }

            EnsureMaterial();
            MatchSourceRect();
            UpdateSweepVisual();
        }

        private void OnDestroy()
        {
            if (overlayImage != null && overlayImage.material == _material)
            {
                overlayImage.material = null;
            }

            if (_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_material);
            }

            _material = null;
        }

        public void Configure(
            RawImage sourceImage,
            XPlaneOriginalWeatherRadarDisplay display,
            WeatherRadarDataProvider provider)
        {
            sourceDisplay = display;
            dataProvider = provider;

            if (overlayImage == null)
            {
                overlayImage = GetComponent<RawImage>();
            }

            if (sourceImage != null && transform.parent != sourceImage.transform)
            {
                transform.SetParent(sourceImage.transform, false);
            }

            EnsureMaterial();
            MatchSourceRect();
            UpdateSweepVisual();
        }

        /// <summary>
        /// Returns X = angle and Y = direction for a smooth left/right sector scan.
        /// This is deterministic so the presentation can be regression tested.
        /// </summary>
        public static Vector2 EvaluateScan(float elapsedSeconds, float cycleSeconds, float halfAngleDegrees)
        {
            float safeCycle = Mathf.Max(0.1f, cycleSeconds);
            float halfAngle = Mathf.Abs(halfAngleDegrees);
            float cyclePosition = Mathf.Repeat(elapsedSeconds * 2f / safeCycle, 2f);
            bool movingRight = cyclePosition <= 1f;
            float normalized = movingRight ? cyclePosition : 2f - cyclePosition;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, normalized);
            return new Vector2(angle, movingRight ? 1f : -1f);
        }

        private void AutoFindReferences()
        {
            if (overlayImage == null)
            {
                overlayImage = GetComponent<RawImage>();
            }

            if (sourceDisplay == null)
            {
                sourceDisplay = GetComponentInParent<XPlaneOriginalWeatherRadarDisplay>();
            }

            if (dataProvider == null)
            {
                dataProvider = GetComponentInParent<WeatherRadarDataProvider>();
            }

            if (overlayImage != null)
            {
                overlayImage.texture = Texture2D.whiteTexture;
                overlayImage.color = Color.white;
                overlayImage.raycastTarget = false;
            }
        }

        private void EnsureMaterial()
        {
            if (_material != null || overlayImage == null)
            {
                return;
            }

            Shader shader = Resources.Load<Shader>(SweepShaderResourcePath);
            if (shader == null)
            {
                shader = Shader.Find("FAA/UI/XPlaneWeatherRadarSweep");
            }

            if (shader == null)
            {
                overlayImage.enabled = false;
                Debug.LogWarning("[XPlaneWeatherRadarSweepOverlay] Sweep shader was not found.", this);
                return;
            }

            _material = new Material(shader)
            {
                name = "X-Plane Weather Radar Sweep (Runtime)",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            overlayImage.material = _material;
        }

        private void MatchSourceRect()
        {
            if (overlayImage == null)
            {
                return;
            }

            RectTransform rect = overlayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            transform.SetAsLastSibling();
        }

        private void UpdateSweepVisual()
        {
            if (overlayImage == null)
            {
                return;
            }

            bool shouldShow = _material != null && IsSweepAllowed();
            if (overlayImage.enabled != shouldShow)
            {
                overlayImage.enabled = shouldShow;
            }

            if (!shouldShow)
            {
                return;
            }

            Vector2 scan = EvaluateScan(Time.unscaledTime, roundTripSeconds, sectorHalfAngleDegrees);
            _currentScanAngle = scan.x;

            Texture sourceTexture = sourceDisplay != null ? sourceDisplay.CurrentTexture : null;
            float aspect = sourceTexture != null && sourceTexture.height > 0
                ? sourceTexture.width / (float)sourceTexture.height
                : 724f / 512f;

            _material.SetColor("_Color", sweepColor);
            _material.SetVector("_OriginUV", new Vector4(0.5f, originHeightRatio, 0f, 0f));
            _material.SetFloat("_Aspect", aspect);
            _material.SetFloat("_SectorHalfAngle", sectorHalfAngleDegrees);
            _material.SetFloat("_OuterRadius", outerRadius);
            _material.SetFloat("_ScanAngle", scan.x);
            _material.SetFloat("_ScanDirection", scan.y);
            _material.SetFloat("_BeamWidth", beamWidthDegrees);
            _material.SetFloat("_GlowWidth", Mathf.Max(beamWidthDegrees, glowWidthDegrees));
            _material.SetFloat("_TrailWidth", Mathf.Max(beamWidthDegrees, trailWidthDegrees));
            _material.SetFloat("_TrailStrength", trailStrength);
        }

        private bool IsSweepAllowed()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (sourceDisplay != null)
            {
                if (!sourceDisplay.HasUsableTexture)
                {
                    return false;
                }

                if (sourceDisplay.HasRadarPowerState && !sourceDisplay.IsRadarPowered)
                {
                    return false;
                }
            }

            WeatherRadarData radarData = dataProvider != null ? dataProvider.RadarData : null;
            return radarData == null || radarData.currentMode != RadarMode.STBY;
        }
    }
}
