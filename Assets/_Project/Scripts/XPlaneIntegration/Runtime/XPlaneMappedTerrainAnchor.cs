using FAA.Geo;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    [ExecuteAlways]
    [AddComponentMenu("X-Plane Integration/Runtime/X-Plane Mapped Terrain Anchor")]
    public sealed class XPlaneMappedTerrainAnchor : MonoBehaviour
    {
        private enum TerrainPlacementMode
        {
            Georeferenced = 0,
            FollowProjectionOriginWhenOutsideCoverage = 1,
            AlwaysFollowProjectionOrigin = 2
        }

        [Header("References")]
        [SerializeField] private GeoPosUnityPosProjectManager geoProjection;
        [SerializeField] private Transform terrainRoot;

        [Header("Terrain Georeference")]
        [SerializeField] private double terrainReferenceLatitude = 41.633248682195;
        [SerializeField] private double terrainReferenceLongitude = -91.56000253843;
        [SerializeField] private float terrainReferenceAltitudeMeters = 0f;
        [SerializeField] private Vector3 terrainReferenceLocalOffsetMeters = Vector3.zero;

        [Header("Axis Alignment")]
        [Tooltip("OPL terrain tiles are authored with local +X west and +Z south. A 180 degree yaw maps them to the project east/north geo frame.")]
        [SerializeField] private bool enforceTerrainYaw = true;
        [SerializeField] private float terrainYawDegrees = 180f;
        [SerializeField] private Vector3 worldOffsetMeters = Vector3.zero;

        [Header("Runtime Placement")]
        [Tooltip("Keeps the imported terrain visible when the live X-Plane aircraft is outside the terrain source coverage.")]
        [SerializeField] private TerrainPlacementMode placementMode = TerrainPlacementMode.FollowProjectionOriginWhenOutsideCoverage;
        [SerializeField] private float maxGeoreferencedDistanceKm = 120f;
        [SerializeField] private Vector3 followOriginWorldOffsetMeters = Vector3.zero;
        [SerializeField] private bool centerOnViewTransformWhenFollowing = true;
        [SerializeField] private bool autoCenterTerrainBoundsWhenFollowing = true;
        [SerializeField] private bool keepTerrainNearViewAltitudeWhenFollowing = true;
        [SerializeField] private float followTerrainBelowViewMeters = 450f;
        [SerializeField] private Transform followTransform;
        [SerializeField] private string followTransformFallbackName = "X-Plane Ownship";

        [Header("Terrain Rendering")]
        [SerializeField] private bool enhanceTerrainVisibility = true;
        [SerializeField] private TerrainLayer visibleTerrainLayer;
        [SerializeField] private float visibleLayerTileSizeMeters = 850f;
        [SerializeField] private float visibleHeightmapPixelError = 1.2f;
        [SerializeField] private float visibleBasemapDistanceMeters = 20000f;
        [SerializeField] private bool createVisibleTerrainUnderlay = true;
        [SerializeField] private float visibleUnderlaySizeMeters = 100000f;
        [SerializeField] private float visibleUnderlayYOffsetMeters = 2f;
        [SerializeField] private float visibleUnderlayTextureRepeats = 80f;
        [SerializeField] private bool alignVisibleUnderlayToFollowView = true;
        [SerializeField] private float visibleUnderlayForwardOffsetMeters = 0f;
        [SerializeField] private float visibleUnderlayBelowViewMeters = 650f;
        [SerializeField] private Color visibleUnderlayTint = new Color(0.78f, 0.86f, 0.72f, 1f);
        [SerializeField] private bool applyRenderingInEditMode = false;

        [Header("Runtime")]
        [SerializeField] private bool updateWhenProjectionChanges = true;
        [SerializeField] private bool updateInEditMode = true;
        [SerializeField] private bool verboseLogging = false;

        private TerrainLayer _runtimeVisibleLayer;
        private GameObject _visualUnderlay;
        private Material _visualUnderlayMaterial;

        private void Awake()
        {
            FindDependencies();
        }

        private void OnEnable()
        {
            FindDependencies();
            SubscribeProjection();
            ApplyGeoreference();
        }

        private void OnDisable()
        {
            if (geoProjection != null)
            {
                geoProjection.OnProjectionParametersChanged -= ApplyGeoreference;
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && updateInEditMode)
            {
                FindDependencies();
                ApplyGeoreference();
            }
        }

        [ContextMenu("Apply X-Plane Terrain Georeference")]
        public void ApplyGeoreference()
        {
            if (!Application.isPlaying && !updateInEditMode)
            {
                return;
            }

            FindDependencies();
            if (geoProjection == null)
            {
                return;
            }

            Transform target = terrainRoot != null ? terrainRoot : transform;
            if (enforceTerrainYaw)
            {
                target.rotation = Quaternion.Euler(0f, terrainYawDegrees, 0f);
            }

            var origin = geoProjection.GetOrigin();
            bool followOrigin = ShouldFollowProjectionOrigin(origin.latitude, origin.longitude);
            double anchorLatitude = followOrigin ? origin.latitude : terrainReferenceLatitude;
            double anchorLongitude = followOrigin ? origin.longitude : terrainReferenceLongitude;
            Vector3 placementOffset = followOrigin ? followOriginWorldOffsetMeters : worldOffsetMeters;
            Vector3 referenceWorld = geoProjection.GeoToUnityPosition(
                anchorLatitude,
                anchorLongitude,
                terrainReferenceAltitudeMeters);
            Vector3? followWorldPosition = null;
            if (followOrigin && centerOnViewTransformWhenFollowing && TryGetFollowWorldPosition(out Vector3 followWorld))
            {
                followWorldPosition = followWorld;
                referenceWorld.x = followWorld.x;
                if (keepTerrainNearViewAltitudeWhenFollowing)
                {
                    referenceWorld.y = followWorld.y - Mathf.Max(50f, followTerrainBelowViewMeters);
                }
                referenceWorld.z = followWorld.z;
            }

            Vector3 localOffset = GetTerrainReferenceLocalOffset(target, followOrigin);
            target.position = referenceWorld + placementOffset - (target.rotation * localOffset);
            ApplyTerrainRendering(target);
            ApplyVisualTerrainUnderlay(target, localOffset, followOrigin);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[XPlaneMappedTerrainAnchor] Mapped terrain to lat={anchorLatitude:F9}, " +
                    $"lon={anchorLongitude:F9}; source lat={terrainReferenceLatitude:F9}, " +
                    $"source lon={terrainReferenceLongitude:F9}; projection origin lat={origin.latitude:F9}, " +
                    $"lon={origin.longitude:F9}; followOrigin={followOrigin}; world={target.position}");
                Debug.Log(
                    $"[XPlaneMappedTerrainAnchor] followWorld={FormatNullableVector(followWorldPosition)}, " +
                    $"referenceWorld={referenceWorld}, localOffset={localOffset}, target={target.position}");
            }
        }

        private void OnDestroy()
        {
            if (_visualUnderlayMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_visualUnderlayMaterial);
                }
                else
                {
                    DestroyImmediate(_visualUnderlayMaterial);
                }
            }
        }

        public bool TrySampleHeightAtGeo(double latitude, double longitude, out float heightMeters)
        {
            heightMeters = 0f;
            FindDependencies();
            if (geoProjection == null)
            {
                return false;
            }

            Vector3 world = geoProjection.GeoToUnityPosition(latitude, longitude, terrainReferenceAltitudeMeters);
            Terrain[] terrains = (terrainRoot != null ? terrainRoot : transform).GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 size = terrain.terrainData.size;
                Vector3 local = terrain.transform.InverseTransformPoint(world);
                bool inside =
                    local.x >= 0f &&
                    local.x <= size.x &&
                    local.z >= 0f &&
                    local.z <= size.z;
                if (!inside)
                {
                    continue;
                }

                heightMeters = terrain.SampleHeight(world) + terrain.transform.position.y;
                return true;
            }

            return false;
        }

        private void ApplyTerrainRendering(Transform target)
        {
            if (!enhanceTerrainVisibility || (!Application.isPlaying && !applyRenderingInEditMode))
            {
                return;
            }

            TerrainLayer layer = CreateVisibleRuntimeLayer();
            Terrain[] terrains = target.GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null)
                {
                    continue;
                }

                terrain.drawHeightmap = true;
                terrain.drawInstanced = true;
                terrain.heightmapPixelError = Mathf.Max(0.5f, visibleHeightmapPixelError);
                terrain.basemapDistance = Mathf.Max(500f, visibleBasemapDistanceMeters);
                terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                if (layer != null && terrain.terrainData != null)
                {
                    TerrainLayer[] layers = terrain.terrainData.terrainLayers;
                    if (layers == null || layers.Length != 1 || layers[0] != layer)
                    {
                        terrain.terrainData.terrainLayers = new[] { layer };
                    }
                }
            }
        }

        private void ApplyVisualTerrainUnderlay(Transform target, Vector3 localCenter, bool followOrigin)
        {
            if (!Application.isPlaying || !enhanceTerrainVisibility || !createVisibleTerrainUnderlay)
            {
                return;
            }

            Texture2D terrainTexture = visibleTerrainLayer != null ? visibleTerrainLayer.diffuseTexture : null;
            if (terrainTexture == null)
            {
                return;
            }

            MeshRenderer renderer = EnsureVisualUnderlay(target);
            if (renderer == null)
            {
                return;
            }

            float size = Mathf.Max(1000f, visibleUnderlaySizeMeters);
            float repeats = Mathf.Max(1f, visibleUnderlayTextureRepeats);
            _visualUnderlay.transform.SetParent(target, true);
            if (followOrigin && alignVisibleUnderlayToFollowView && TryGetFollowWorldPosition(out Vector3 followWorld))
            {
                Vector3 forward = GetHorizontalForward(target);
                Vector3 underlayWorld = followWorld + forward * Mathf.Max(0f, visibleUnderlayForwardOffsetMeters);
                underlayWorld.y = followWorld.y - Mathf.Max(50f, visibleUnderlayBelowViewMeters);
                _visualUnderlay.transform.SetPositionAndRotation(
                    underlayWorld,
                    Quaternion.LookRotation(forward, Vector3.up));
            }
            else
            {
                _visualUnderlay.transform.localPosition = new Vector3(
                    localCenter.x,
                    localCenter.y + visibleUnderlayYOffsetMeters,
                    localCenter.z);
                _visualUnderlay.transform.localRotation = Quaternion.identity;
            }

            _visualUnderlay.transform.localScale = new Vector3(size, 1f, size);

            Material material = renderer.sharedMaterial;
            if (material == null || material.mainTexture != terrainTexture)
            {
                material = CreateVisualUnderlayMaterial(terrainTexture);
                renderer.sharedMaterial = material;
            }

            SetMaterialTextureScale(material, new Vector2(repeats, repeats));
            SetMaterialColor(material, visibleUnderlayTint);
        }

        private Vector3 GetHorizontalForward(Transform fallback)
        {
            Camera viewCamera = GetBestGameCamera();
            Transform view = viewCamera != null ? viewCamera.transform : followTransform;
            Vector3 forward = view != null ? view.forward : fallback.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = fallback.forward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        private MeshRenderer EnsureVisualUnderlay(Transform target)
        {
            if (_visualUnderlay == null)
            {
                Transform existing = target.Find("FAA_OPL_Terrain_Visual_Surface");
                _visualUnderlay = existing != null ? existing.gameObject : new GameObject("FAA_OPL_Terrain_Visual_Surface");
            }

            MeshFilter meshFilter = _visualUnderlay.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = _visualUnderlay.AddComponent<MeshFilter>();
            }

            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = CreateUnitPlaneMesh();
            }

            MeshRenderer renderer = _visualUnderlay.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = _visualUnderlay.AddComponent<MeshRenderer>();
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        private static Mesh CreateUnitPlaneMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "FAA OPL Runtime Visual Terrain Plane",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private Material CreateVisualUnderlayMaterial(Texture2D terrainTexture)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Standard");
            if (_visualUnderlayMaterial == null || _visualUnderlayMaterial.shader != shader)
            {
                if (_visualUnderlayMaterial != null)
                {
                    Destroy(_visualUnderlayMaterial);
                }

                _visualUnderlayMaterial = new Material(shader)
                {
                    name = "FAA OPL Runtime Visual Terrain"
                };
            }

            SetMaterialTexture(_visualUnderlayMaterial, terrainTexture);
            SetMaterialColor(_visualUnderlayMaterial, visibleUnderlayTint);
            return _visualUnderlayMaterial;
        }

        private static void SetMaterialTexture(Material material, Texture texture)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static void SetMaterialTextureScale(Material material, Vector2 scale)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", scale);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", scale);
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private TerrainLayer CreateVisibleRuntimeLayer()
        {
            if (visibleTerrainLayer == null)
            {
                return null;
            }

            float tileSize = Mathf.Max(32f, visibleLayerTileSizeMeters);
            if (_runtimeVisibleLayer != null && Mathf.Approximately(_runtimeVisibleLayer.tileSize.x, tileSize))
            {
                return _runtimeVisibleLayer;
            }

            _runtimeVisibleLayer = new TerrainLayer
            {
                diffuseTexture = visibleTerrainLayer.diffuseTexture,
                normalMapTexture = visibleTerrainLayer.normalMapTexture,
                maskMapTexture = visibleTerrainLayer.maskMapTexture,
                tileSize = new Vector2(tileSize, tileSize),
                tileOffset = visibleTerrainLayer.tileOffset,
                specular = visibleTerrainLayer.specular,
                metallic = visibleTerrainLayer.metallic,
                smoothness = visibleTerrainLayer.smoothness,
                normalScale = visibleTerrainLayer.normalScale,
                diffuseRemapMin = visibleTerrainLayer.diffuseRemapMin,
                diffuseRemapMax = visibleTerrainLayer.diffuseRemapMax,
                maskMapRemapMin = visibleTerrainLayer.maskMapRemapMin,
                maskMapRemapMax = visibleTerrainLayer.maskMapRemapMax
            };
            return _runtimeVisibleLayer;
        }

        private Vector3 GetTerrainReferenceLocalOffset(Transform target, bool followOrigin)
        {
            if (!followOrigin || !autoCenterTerrainBoundsWhenFollowing || !IsNearlyZero(terrainReferenceLocalOffsetMeters))
            {
                return terrainReferenceLocalOffsetMeters;
            }

            if (!TryGetTerrainLocalBounds(target, out Bounds localBounds))
            {
                return terrainReferenceLocalOffsetMeters;
            }

            Vector3 center = localBounds.center;
            center.y = terrainReferenceLocalOffsetMeters.y;
            return center;
        }

        private bool TryGetTerrainLocalBounds(Transform target, out Bounds localBounds)
        {
            localBounds = default;
            bool hasBounds = false;
            Terrain[] terrains = target.GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 size = terrain.terrainData.size;
                EncapsulateTerrainCorner(target, terrain.transform, Vector3.zero, ref localBounds, ref hasBounds);
                EncapsulateTerrainCorner(target, terrain.transform, new Vector3(size.x, 0f, 0f), ref localBounds, ref hasBounds);
                EncapsulateTerrainCorner(target, terrain.transform, new Vector3(0f, 0f, size.z), ref localBounds, ref hasBounds);
                EncapsulateTerrainCorner(target, terrain.transform, new Vector3(size.x, 0f, size.z), ref localBounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateTerrainCorner(
            Transform root,
            Transform terrainTransform,
            Vector3 localCorner,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            Vector3 rootLocal = root.InverseTransformPoint(terrainTransform.TransformPoint(localCorner));
            if (!hasBounds)
            {
                localBounds = new Bounds(rootLocal, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(rootLocal);
        }

        private static bool IsNearlyZero(Vector3 value)
        {
            return value.sqrMagnitude < 0.0001f;
        }

        private static string FormatNullableVector(Vector3? value)
        {
            return value.HasValue ? value.Value.ToString("F2") : "none";
        }

        private bool TryGetFollowWorldPosition(out Vector3 worldPosition)
        {
            Camera viewCamera = GetBestGameCamera();
            if (viewCamera != null)
            {
                followTransform = viewCamera.transform;
            }

            if (followTransform == null && !string.IsNullOrWhiteSpace(followTransformFallbackName))
            {
                GameObject fallback = GameObject.Find(followTransformFallbackName);
                if (fallback != null)
                {
                    followTransform = fallback.transform;
                }
            }

            if (followTransform == null)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = followTransform.position;
            return true;
        }

        private static Camera GetBestGameCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                return mainCamera;
            }

            Camera bestCamera = null;
            float bestDepth = float.NegativeInfinity;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture != null)
                {
                    continue;
                }

                if (camera.depth >= bestDepth)
                {
                    bestDepth = camera.depth;
                    bestCamera = camera;
                }
            }

            return bestCamera;
        }

        private bool ShouldFollowProjectionOrigin(double originLatitude, double originLongitude)
        {
            switch (placementMode)
            {
                case TerrainPlacementMode.AlwaysFollowProjectionOrigin:
                    return true;
                case TerrainPlacementMode.Georeferenced:
                    return false;
                default:
                    double distanceMeters = geoProjection.CalculateDistance(
                        terrainReferenceLatitude,
                        terrainReferenceLongitude,
                        originLatitude,
                        originLongitude);
                    return distanceMeters > Mathf.Max(0.1f, maxGeoreferencedDistanceKm) * 1000f;
            }
        }

        private void FindDependencies()
        {
            if (terrainRoot == null)
            {
                terrainRoot = transform;
            }

            if (geoProjection == null)
            {
                geoProjection = GeoPosUnityPosProjectManager.Instance ??
                    FindAnyObjectByType<GeoPosUnityPosProjectManager>(FindObjectsInactive.Include);
            }
        }

        private void SubscribeProjection()
        {
            if (!updateWhenProjectionChanges || geoProjection == null)
            {
                return;
            }

            geoProjection.OnProjectionParametersChanged -= ApplyGeoreference;
            geoProjection.OnProjectionParametersChanged += ApplyGeoreference;
        }
    }
}
