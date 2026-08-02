using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class XPlaneWeatherRadarPresentationTests
    {
        [Test]
        public void NativeWeatherTexture_IsAspectFittedWithoutSquareDistortion()
        {
            Type displayType = Type.GetType("WeatherRadar.XPlaneOriginalWeatherRadarDisplay, WeatherRadar");
            Assert.That(displayType, Is.Not.Null);

            MethodInfo calculate = displayType.GetMethod(
                "CalculateAspectFitSize",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);

            float nativeAspect = 724f / 512f;
            Vector2 fitted = (Vector2)calculate.Invoke(null, new object[]
            {
                new Vector2(352f, 352f),
                nativeAspect
            });

            Assert.That(fitted.x, Is.EqualTo(352f).Within(0.001f));
            Assert.That(fitted.y, Is.EqualTo(352f / nativeAspect).Within(0.001f));
            Assert.That(fitted.x / fitted.y, Is.EqualTo(nativeAspect).Within(0.001f));
        }

        [Test]
        public void WeatherSweep_PingPongsAcrossTheXPlaneSector()
        {
            Type sweepType = Type.GetType("WeatherRadar.XPlaneWeatherRadarSweepOverlay, WeatherRadar");
            Assert.That(sweepType, Is.Not.Null);

            MethodInfo evaluate = sweepType.GetMethod(
                "EvaluateScan",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(evaluate, Is.Not.Null);

            const float cycle = 4f;
            const float halfAngle = 64f;
            float[] times = { 0f, cycle * 0.25f, cycle * 0.5f, cycle * 0.75f, cycle };
            float[] expectedAngles = { -64f, 0f, 64f, 0f, -64f };

            for (int i = 0; i < times.Length; i++)
            {
                Vector2 state = (Vector2)evaluate.Invoke(null, new object[] { times[i], cycle, halfAngle });
                Assert.That(state.x, Is.EqualTo(expectedAngles[i]).Within(0.001f));
            }

            Vector2 outgoing = (Vector2)evaluate.Invoke(null, new object[] { cycle * 0.25f, cycle, halfAngle });
            Vector2 returning = (Vector2)evaluate.Invoke(null, new object[] { cycle * 0.75f, cycle, halfAngle });
            Assert.That(outgoing.y, Is.EqualTo(1f));
            Assert.That(returning.y, Is.EqualTo(-1f));
        }

        [Test]
        public void WeatherSweepShader_IsIncludedAsAResource()
        {
            Shader shader = Resources.Load<Shader>("Shaders/XPlaneWeatherRadarSweep");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("FAA/UI/XPlaneWeatherRadarSweep"));
            Material material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_OuterRadius"), Is.True,
                    "The scan must stop at the aircraft radar's outer range arc.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void NativeWeatherProvider_DoesNotSelectThePointDiagnosticRenderer()
        {
            Type providerType = Type.GetType("WeatherRadar.XPlaneOriginalWeatherRadarProvider, WeatherRadar");
            Assert.That(providerType, Is.Not.Null);

            GameObject root = new GameObject("Native Weather Provider Test");
            try
            {
                Component provider = root.AddComponent(providerType);
                providerType.GetProperty("RadarTextureUrl")?.SetValue(provider, "http://127.0.0.1:12678/v1/render/weather.png");
                providerType.GetProperty("PreferNativePluginTexture")?.SetValue(provider, true);

                MethodInfo buildUrl = providerType.GetMethod("BuildRequestUrl", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildUrl, Is.Not.Null);
                string url = (string)buildUrl.Invoke(provider, null);

                StringAssert.StartsWith("http://127.0.0.1:12678/v1/render/weather.png", url);
                StringAssert.DoesNotContain("range_nm=", url,
                    "range_nm switches the API to its sparse RADR point diagnostic instead of X-Plane's native texture.");

                providerType.GetProperty("PreferNativePluginTexture")?.SetValue(provider, false);
                string diagnosticUrl = (string)buildUrl.Invoke(provider, null);
                StringAssert.Contains("range_nm=", diagnosticUrl);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeatherReferenceOverlay_IsVisibleByDefault()
        {
            Type displayType = Type.GetType("WeatherRadar.XPlaneOriginalWeatherRadarDisplay, WeatherRadar");
            Assert.That(displayType, Is.Not.Null);

            GameObject root = new GameObject(
                "Weather Display Defaults Test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.RawImage));
            try
            {
                Component display = root.AddComponent(displayType);
                PropertyInfo showOverlay = displayType.GetProperty("ShowReferenceOverlay");
                Assert.That(showOverlay, Is.Not.Null);
                Assert.That(showOverlay.GetValue(display), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(0f, 0f, "N")]
        [TestCase(90f, 0f, "E")]
        [TestCase(180f, 0f, "S")]
        [TestCase(270f, 0f, "W")]
        [TestCase(337f, 10f, "35")]
        public void WeatherHeadingArc_UsesLiveAbsoluteHeading(float heading, float relativeBearing, string expected)
        {
            Type overlayType = Type.GetType("WeatherRadar.XPlaneWeatherRadarOverlay, WeatherRadar");
            Assert.That(overlayType, Is.Not.Null);
            MethodInfo format = overlayType.GetMethod("FormatHeadingLabel", BindingFlags.Public | BindingFlags.Static);
            Assert.That(format, Is.Not.Null);
            Assert.That(format.Invoke(null, new object[] { heading, relativeBearing }), Is.EqualTo(expected));
        }

        [Test]
        public void WeatherReferenceOverlay_UsesTwoTimesSupersampling()
        {
            Type overlayType = Type.GetType("WeatherRadar.XPlaneWeatherRadarOverlay, WeatherRadar");
            Assert.That(overlayType, Is.Not.Null);

            MethodInfo calculateScale = overlayType.GetMethod(
                "CalculateRenderScale",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculateScale, Is.Not.Null);
            Assert.That(calculateScale.Invoke(null, new object[] { 1448, 1024 }), Is.EqualTo(2f));

            GameObject root = new GameObject(
                "High Resolution Weather Overlay Test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.RawImage));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                overlayType.GetMethod("Redraw", BindingFlags.Public | BindingFlags.Instance)?.Invoke(overlay, null);
                Vector2Int resolution = (Vector2Int)overlayType.GetProperty("TextureResolution")?.GetValue(overlay);
                Assert.That(resolution, Is.EqualTo(new Vector2Int(1448, 1024)));

                MethodInfo measureLabelHeight = overlayType.GetMethod(
                    "MeasureTinyTextHeight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(measureLabelHeight, Is.Not.Null);
                Assert.That((float)measureLabelHeight.Invoke(overlay, new object[] { 5 }), Is.GreaterThanOrEqualTo(60f),
                    "Bearing and range glyphs must survive the compact display reduction.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeatherMetricIcons_GenerateResolutionIndependentGeometry()
        {
            Type iconType = Type.GetType("FAA.Customization.WeatherMetricIconGraphic, Assembly-CSharp");
            Assert.That(iconType, Is.Not.Null);

            GameObject root = new GameObject(
                "Weather Metric Icon Test",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(24f, 24f);
                Component icon = root.AddComponent(iconType);
                MethodInfo populate = iconType.GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(UnityEngine.UI.VertexHelper) },
                    modifiers: null);
                Assert.That(populate, Is.Not.Null);

                using (var vertices = new UnityEngine.UI.VertexHelper())
                {
                    populate.Invoke(icon, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
                    Assert.That(vertices.currentIndexCount, Is.GreaterThan(0));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeatherInfoStrip_FormatsLiveXPlaneConditionsForPilots()
        {
            Type stripType = Type.GetType("FAA.Customization.XPlaneWeatherInfoStrip, Assembly-CSharp");
            Assert.That(stripType, Is.Not.Null);

            Assert.That(InvokeFormat(stripType, "FormatTemperature", -8.535527f), Is.EqualTo("-9°C"));
            Assert.That(InvokeFormat(stripType, "FormatWind", 265f, 143.84f), Is.EqualTo("265° / 144KT"));
            Assert.That(InvokeFormat(stripType, "FormatVisibility", 2414.016f), Is.EqualTo("1.5 SM"));
            Assert.That(InvokeFormat(stripType, "FormatPressure", 29.921249f), Is.EqualTo("29.92"));
            Assert.That(InvokeFormat(stripType, "FormatPrecipitation", 0.92f), Is.EqualTo("92%"));
        }

        [Test]
        public void WeatherInfoStrip_IsAHighResolutionRailToTheRightOfTheRadar()
        {
            Type stripType = Type.GetType("FAA.Customization.XPlaneWeatherInfoStrip, Assembly-CSharp");
            Assert.That(stripType, Is.Not.Null);

            MethodInfo calculate = stripType.GetMethod(
                "CalculateRightSidePosition",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);
            Vector2 position = (Vector2)calculate.Invoke(null, new object[]
            {
                new Vector2(28f, 28f),
                new Vector2(372f, 372f),
                Vector2.zero,
                new Vector2(240f, 254f),
                12f
            });
            Assert.That(position, Is.EqualTo(new Vector2(412f, 87f)));

            GameObject root = new GameObject(
                "Weather Conditions Rail Test",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                Component strip = root.AddComponent(stripType);
                MethodInfo ensureVisualTree = stripType.GetMethod(
                    "EnsureVisualTree",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensureVisualTree, Is.Not.Null, "Visual-tree builder must remain available for runtime creation.");
                ensureVisualTree.Invoke(strip, null);
                Assert.That(
                    root.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(),
                    Is.Not.Null,
                    "Weather metrics must use a vertical rail layout.");
                Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.That(tmpType, Is.Not.Null, "TextMesh Pro runtime type must be available.");
                Component value = root.transform.Find("Details/OAT/Value")?.GetComponent(tmpType);
                Component label = root.transform.Find("Details/OAT/Label")?.GetComponent(tmpType);
                Assert.That(value, Is.Not.Null);
                Assert.That(label, Is.Not.Null);
                Assert.That(ReadProperty<float>(tmpType, value, "fontSize"), Is.GreaterThanOrEqualTo(17f));
                Assert.That(ReadProperty<float>(tmpType, label, "fontSize"), Is.GreaterThanOrEqualTo(10.5f));
                Assert.That(ReadProperty<bool>(tmpType, value, "extraPadding"), Is.True);
                Assert.That(ReadProperty<bool>(tmpType, label, "extraPadding"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeatherInfoStrip_UsesFoldableHeaderAndMonotonicReducedMotion()
        {
            Type stripType = Type.GetType("FAA.Customization.XPlaneWeatherInfoStrip, Assembly-CSharp");
            Assert.That(stripType, Is.Not.Null);

            MethodInfo ease = stripType.GetMethod("EaseOutQuart", BindingFlags.Public | BindingFlags.Static);
            Assert.That(ease, Is.Not.Null);
            Assert.That((float)ease.Invoke(null, new object[] { 0f }), Is.EqualTo(0f));
            Assert.That((float)ease.Invoke(null, new object[] { 0.5f }), Is.EqualTo(0.9375f).Within(0.0001f));
            Assert.That((float)ease.Invoke(null, new object[] { 1f }), Is.EqualTo(1f));

            GameObject root = new GameObject(
                "Foldable Weather Conditions Rail Test",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                Component strip = root.AddComponent(stripType);
                MethodInfo ensureVisualTree = stripType.GetMethod(
                    "EnsureVisualTree",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensureVisualTree, Is.Not.Null);
                ensureVisualTree.Invoke(strip, null);

                Transform header = root.transform.Find("SummaryHeader");
                Transform details = root.transform.Find("Details");
                Assert.That(header, Is.Not.Null, "SummaryHeader was not created.");
                Assert.That(details, Is.Not.Null, "Details container was not created.");
                Assert.That(header.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null,
                    "The summary header must provide the full-width disclosure target.");
                Assert.That(root.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null,
                    "The detail rows must clip cleanly while the rail folds.");

                stripType.GetProperty("ReducedMotion")?.SetValue(strip, true);
                stripType.GetMethod("SetExpanded")?.Invoke(strip, new object[] { false, false });
                Assert.That(stripType.GetProperty("IsExpanded")?.GetValue(strip), Is.False);
                Assert.That((float)stripType.GetProperty("DisclosureProgress")?.GetValue(strip), Is.EqualTo(0f));
                Assert.That(root.GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(0f),
                    "A closed WX disclosure must leave no collapsed rectangle beside the radar.");
                CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
                Assert.That(rootGroup, Is.Not.Null, "The disclosure root must own a CanvasGroup.");
                Assert.That(rootGroup.alpha, Is.EqualTo(0f));
                Assert.That(rootGroup.blocksRaycasts, Is.False);
                CanvasGroup detailsGroup = details.GetComponent<CanvasGroup>();
                Assert.That(detailsGroup, Is.Not.Null,
                    "The disclosure details container must retain its CanvasGroup.");
                Assert.That(detailsGroup.alpha, Is.EqualTo(0f));

                stripType.GetMethod("SetExpanded")?.Invoke(strip, new object[] { true, false });
                Assert.That(stripType.GetProperty("IsExpanded")?.GetValue(strip), Is.True);
                Assert.That((float)stripType.GetProperty("DisclosureProgress")?.GetValue(strip), Is.EqualTo(1f));
                Assert.That(root.GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(300f));
                Assert.That(rootGroup.alpha, Is.EqualTo(1f));
                Assert.That(rootGroup.blocksRaycasts, Is.True);
                Assert.That(detailsGroup.alpha, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeatherRadar_EssentialTmpReadoutsRemainReadable()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaRadarControlsOverlay, Assembly-CSharp");
            Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Type surfaceType = Type.GetType("FAA.Customization.FaaRadarInteractionSurface, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);
            Assert.That(tmpType, Is.Not.Null);
            Assert.That(surfaceType, Is.Not.Null);

            GameObject root = new GameObject("Weather Radar Labels", typeof(RectTransform));
            GameObject modeObject = new GameObject("ModeLabel", typeof(RectTransform), typeof(CanvasRenderer));
            GameObject statusObject = new GameObject("TextureStatusLabel", typeof(RectTransform), typeof(CanvasRenderer));
            GameObject sourceObject = new GameObject("SourceLabel", typeof(RectTransform), typeof(CanvasRenderer));
            GameObject ageObject = new GameObject("TextureAgeLabel", typeof(RectTransform), typeof(CanvasRenderer));
            GameObject powerObject = new GameObject("WeatherPowerBadge", typeof(RectTransform), typeof(CanvasRenderer));
            GameObject powerTextObject = new GameObject("PowerLabel", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                modeObject.transform.SetParent(root.transform, false);
                modeObject.AddComponent(tmpType);
                statusObject.transform.SetParent(root.transform, false);
                statusObject.AddComponent(tmpType);
                sourceObject.transform.SetParent(root.transform, false);
                sourceObject.AddComponent(tmpType);
                ageObject.transform.SetParent(root.transform, false);
                ageObject.AddComponent(tmpType);
                powerObject.transform.SetParent(root.transform, false);
                powerTextObject.transform.SetParent(powerObject.transform, false);
                Component powerText = powerTextObject.AddComponent(tmpType);
                MethodInfo improve = overlayType.GetMethod(
                    "ImproveWeatherLabelLegibility",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(improve, Is.Not.Null);
                improve.Invoke(null, new object[] { root.transform });

                Assert.That(modeObject.activeSelf, Is.False,
                    "The duplicate ModeLabel must not overlap the live power/mode badge.");
                Assert.That(statusObject.activeSelf, Is.False,
                    "Texture status belongs in the conditions drawer, not over the radar.");
                Assert.That(sourceObject.activeSelf, Is.False,
                    "The source label belongs in the conditions drawer, not over the radar.");
                Assert.That(ageObject.activeSelf, Is.False,
                    "Texture age belongs in the conditions drawer, not over the radar.");
                Assert.That(powerObject.activeSelf, Is.True);
                Assert.That(ReadProperty<float>(tmpType, powerText, "fontSize"), Is.GreaterThanOrEqualTo(16f));
                Assert.That(ReadProperty<bool>(tmpType, powerText, "extraPadding"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject surfaceRoot = new GameObject(
                "Weather Radar Interaction Test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
            try
            {
                Component surface = surfaceRoot.AddComponent(surfaceType);
                Type radarKind = Type.GetType("FAA.Customization.FaaRadarKind, Assembly-CSharp");
                object weatherKind = Enum.Parse(radarKind, "Weather");
                surfaceType.GetMethod("Configure")?.Invoke(surface, new[] { null, weatherKind, false });
                Transform focusFrame = surfaceRoot.transform.Find("RadarFocusFrame");
                Assert.That(focusFrame, Is.Not.Null);
                GameObject legacyEdge = new GameObject(
                    "TopEdge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image));
                legacyEdge.transform.SetParent(focusFrame, false);
                surfaceType.GetMethod("Configure")?.Invoke(surface, new[] { null, weatherKind, false });

                Transform hint = focusFrame.Find("ConfigureHint");
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.gameObject.activeSelf, Is.False,
                    "No persistent rectangle may obscure live weather-radar annotations.");
                Assert.That(legacyEdge.activeSelf, Is.False,
                    "The old full-width focus edge must be retired when the glass is refreshed.");
                string[] cornerSegments =
                {
                    "TopLeftHorizontal", "TopLeftVertical", "TopRightHorizontal", "TopRightVertical",
                    "BottomLeftHorizontal", "BottomLeftVertical", "BottomRightHorizontal", "BottomRightVertical"
                };
                foreach (string segmentName in cornerSegments)
                {
                    Transform segment = focusFrame.Find(segmentName);
                    Assert.That(segment, Is.Not.Null, segmentName);
                    Assert.That(segment.gameObject.activeSelf, Is.True, segmentName);
                    Vector2 size = segment.GetComponent<RectTransform>().sizeDelta;
                    Assert.That(Mathf.Max(size.x, size.y), Is.LessThanOrEqualTo(22.1f),
                        $"{segmentName} must remain a short corner bracket, not a view-blocking edge.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(surfaceRoot);
            }
        }

        [Test]
        public void RadarConfigurationDrawer_UsesMonotonicQuartMotionAndSupportsReducedMotion()
        {
            Type drawerType = Type.GetType("FAA.Customization.FaaRadarConfigurationDrawer, Assembly-CSharp");
            Type surfaceType = Type.GetType("FAA.Customization.FaaRadarInteractionSurface, Assembly-CSharp");
            Assert.That(drawerType, Is.Not.Null);
            Assert.That(surfaceType, Is.Not.Null);
            Assert.That(typeof(UnityEngine.EventSystems.IPointerClickHandler).IsAssignableFrom(surfaceType), Is.True);

            MethodInfo ease = drawerType.GetMethod("EaseOutQuart", BindingFlags.Public | BindingFlags.Static);
            Assert.That(ease, Is.Not.Null);
            Assert.That((float)ease.Invoke(null, new object[] { 0f }), Is.EqualTo(0f));
            Assert.That((float)ease.Invoke(null, new object[] { 0.5f }), Is.EqualTo(0.9375f).Within(0.0001f));
            Assert.That((float)ease.Invoke(null, new object[] { 1f }), Is.EqualTo(1f));

            GameObject root = new GameObject(
                "Radar Drawer Motion Test",
                typeof(RectTransform),
                typeof(CanvasGroup));
            try
            {
                Component drawer = root.AddComponent(drawerType);
                drawerType.GetMethod("Configure")?.Invoke(drawer, new object[] { true });
                Assert.That(((Behaviour)drawer).enabled, Is.False,
                    "The hidden endpoint may sleep until a visibility request arrives.");
                drawerType.GetMethod("SetVisible")?.Invoke(drawer, new object[] { true, false });
                Assert.That(((Behaviour)drawer).enabled, Is.True,
                    "A non-immediate open request must wake the drawer animation.");
                drawerType.GetMethod("SetVisible")?.Invoke(drawer, new object[] { true, true });
                Assert.That(drawerType.GetProperty("TargetVisible")?.GetValue(drawer), Is.True);
                Assert.That((float)drawerType.GetProperty("Progress")?.GetValue(drawer), Is.EqualTo(1f));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RadarPanels_ExposePersistentBoundedResizeControls()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaRadarControlsOverlay, Assembly-CSharp");
            Type surfaceType = Type.GetType("FAA.Customization.FaaRadarInteractionSurface, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);
            Assert.That(surfaceType, Is.Not.Null);
            Assert.That(typeof(UnityEngine.EventSystems.IScrollHandler).IsAssignableFrom(surfaceType), Is.True,
                "The radar glass should support direct pointer-wheel resizing.");
            Assert.That(overlayType.GetMethod("WeatherSizeDown"), Is.Not.Null);
            Assert.That(overlayType.GetMethod("WeatherSizeUp"), Is.Not.Null);
            Assert.That(overlayType.GetMethod("TrafficSizeDown"), Is.Not.Null);
            Assert.That(overlayType.GetMethod("TrafficSizeUp"), Is.Not.Null);

            MethodInfo clamp = overlayType.GetMethod("ClampRadarSize", BindingFlags.Public | BindingFlags.Static);
            Assert.That(clamp, Is.Not.Null);
            Assert.That(clamp.Invoke(null, new object[] { 90f, 220f, 560f }), Is.EqualTo(220f));
            Assert.That(clamp.Invoke(null, new object[] { 320f, 220f, 560f }), Is.EqualTo(320f));
            Assert.That(clamp.Invoke(null, new object[] { 900f, 220f, 560f }), Is.EqualTo(560f));
        }

        [Test]
        public void TrafficGlassPresentation_DoesNotUndoPilotBackgroundToggle()
        {
            Type displayType = Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar");
            Assert.That(displayType, Is.Not.Null);
            GameObject root = new GameObject("Traffic Glass Test", typeof(RectTransform));
            try
            {
                Component display = root.AddComponent(displayType);
                displayType.GetMethod("ConfigureHudPresentation")?.Invoke(display, new object[] { 0.56f, 0.42f });
                PropertyInfo background = displayType.GetProperty("ShowRadarBackground");
                Assert.That(background, Is.Not.Null);
                background.SetValue(display, false);
                MethodInfo normalize = displayType.GetMethod(
                    "NormalizePanelReadability",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(normalize, Is.Not.Null);
                normalize.Invoke(display, null);
                Assert.That(background.GetValue(display), Is.False,
                    "BKG/CLR is a pilot control and must not be forced back on during Update.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RadarGlassPresentation_HasNoOpaqueOfflineOrSquareFallback()
        {
            Type weatherType = Type.GetType("WeatherRadar.XPlaneOriginalWeatherRadarDisplay, WeatherRadar");
            Type trafficType = Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar");
            Assert.That(weatherType, Is.Not.Null);
            Assert.That(trafficType, Is.Not.Null);

            GameObject weatherRoot = new GameObject(
                "Transparent Weather Glass Test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.RawImage));
            GameObject trafficRoot = new GameObject(
                "Transparent Traffic Glass Test",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Mask));
            try
            {
                Component weather = weatherRoot.AddComponent(weatherType);
                weatherType.GetMethod("ConfigureHudPresentation")?.Invoke(weather, new object[] { 0.82f });
                Color offlineTint = ReadField<Color>(weatherType, weather, "offlineTint");
                Assert.That(offlineTint.a, Is.LessThanOrEqualTo(0.061f));

                Component traffic = trafficRoot.AddComponent(trafficType);
                trafficType.GetMethod("ConfigureHudPresentation")?.Invoke(traffic, new object[] { 0.34f, 0.28f });
                Color background = ReadField<Color>(trafficType, traffic, "backgroundColor");
                Assert.That(background.a, Is.LessThanOrEqualTo(0.35f));
                Assert.That(ReadField<bool>(trafficType, traffic, "enforceReadablePanelBackground"), Is.False);
                Color ownship = ReadField<Color>(trafficType, traffic, "ownAircraftColor");
                Assert.That(ownship.g, Is.GreaterThan(ownship.r));

                Assert.That(trafficRoot.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic, Is.False);
                Assert.That(trafficRoot.GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(weatherRoot);
                UnityEngine.Object.DestroyImmediate(trafficRoot);
            }
        }

        [Test]
        public void RadarSizeInitialization_IgnoresEditorPlayerPrefs()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaRadarControlsOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);
            const string preferenceKey = "FAA.HUD.WeatherRadarSize";
            bool hadValue = PlayerPrefs.HasKey(preferenceKey);
            float previousValue = hadValue ? PlayerPrefs.GetFloat(preferenceKey) : 0f;
            GameObject root = new GameObject("Radar Size Preference Test");
            try
            {
                PlayerPrefs.SetFloat(preferenceKey, 372f);
                Component overlay = root.AddComponent(overlayType);
                MethodInfo readInitial = overlayType.GetMethod(
                    "ReadInitialRadarSize",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(readInitial, Is.Not.Null);
                float resolved = (float)readInitial.Invoke(overlay, new object[] { preferenceKey, 280f });
                Assert.That(resolved, Is.EqualTo(280f),
                    "Edit-mode setup must serialize the compact project default, not a workstation preference.");
            }
            finally
            {
                if (hadValue)
                {
                    PlayerPrefs.SetFloat(preferenceKey, previousValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(preferenceKey);
                }
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Sa147HudCapture_SlicesTheLiveHudForTheHeadsetCameraViewports()
        {
            Type compatibilityType = Type.GetType("FAA.Headset.SA147HeadsetCompatibility, Assembly-CSharp");
            Assert.That(compatibilityType, Is.Not.Null);
            MethodInfo calculate = compatibilityType.GetMethod(
                "CalculateHudUvRect",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);

            Rect leftHalf = (Rect)calculate.Invoke(null, new object[] { new Rect(0f, 0f, 0.5f, 1f) });
            Rect rightHalf = (Rect)calculate.Invoke(null, new object[] { new Rect(0.5f, 0f, 0.5f, 1f) });
            Assert.That(leftHalf, Is.EqualTo(new Rect(0f, 0f, 0.5f, 1f)));
            Assert.That(rightHalf, Is.EqualTo(new Rect(0.5f, 0f, 0.5f, 1f)));
            Assert.That(compatibilityType.GetMethod("CreateRightEyeMirror", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null,
                "Headset routing must not clone live X-Plane provider/controller hierarchies per eye.");
        }

        private static string InvokeFormat(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return (string)method.Invoke(null, arguments);
        }

        private static T ReadProperty<T>(Type type, object target, string propertyName)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(Type type, object target, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
