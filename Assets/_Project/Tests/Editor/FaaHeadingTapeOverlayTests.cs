using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public class FaaHeadingTapeOverlayTests
    {
        [Test]
        public void EditorUpdate_PreservesManuallyEditedRectTransformLayout()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaHeadingTapeOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null, "FaaHeadingTapeOverlay type was not loaded.");

            GameObject root = new GameObject("Heading Tape Test", typeof(RectTransform));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                RectTransform clipRect = root.transform.Find("Heading Tape Clip") as RectTransform;
                Assert.That(clipRect, Is.Not.Null, "The generated clip RectTransform is missing.");

                Vector2 expectedRootPosition = new Vector2(123f, 456f);
                Vector2 expectedRootSize = new Vector2(712f, 44f);
                Vector2 expectedClipPosition = new Vector2(-42f, 17f);
                rootRect.anchoredPosition = expectedRootPosition;
                rootRect.sizeDelta = expectedRootSize;
                clipRect.anchoredPosition = expectedClipPosition;

                MethodInfo update = overlayType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null, "Could not locate the overlay update method.");
                update.Invoke(overlay, null);

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(expectedRootPosition));
                Assert.That(rootRect.sizeDelta, Is.EqualTo(expectedRootSize));
                Assert.That(clipRect.anchoredPosition, Is.EqualTo(expectedClipPosition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EditorValidation_PreservesManuallyEditedRootRectTransformLayout()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaHeadingTapeOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null, "FaaHeadingTapeOverlay type was not loaded.");

            GameObject root = new GameObject("Heading Tape Validation Test", typeof(RectTransform));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                Vector2 expectedRootPosition = new Vector2(321f, 654f);
                rootRect.anchoredPosition = expectedRootPosition;

                MethodInfo onValidate = overlayType.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onValidate, Is.Not.Null, "Could not locate the overlay validation method.");
                onValidate.Invoke(overlay, null);

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(expectedRootPosition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EditorUpdate_RecentersADetachedInternalClipWithoutMovingTheOverlay()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaHeadingTapeOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);

            GameObject root = new GameObject("Detached Heading Tape Test", typeof(RectTransform));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                RectTransform clipRect = root.transform.Find("Heading Tape Clip") as RectTransform;
                Assert.That(clipRect, Is.Not.Null);

                Vector2 expectedRootPosition = new Vector2(-318f, 207f);
                rootRect.anchoredPosition = expectedRootPosition;
                clipRect.anchoredPosition = new Vector2(611f, -563f);

                InvokePrivate(overlayType, overlay, "Update");

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(expectedRootPosition));
                Assert.That(clipRect.anchoredPosition, Is.EqualTo(new Vector2(0f, -5f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HeadingNorth_RestoresAndKeepsAllNewsLabelsInsideTheClip()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaHeadingTapeOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);

            GameObject root = new GameObject("NEWS Heading Tape Test", typeof(RectTransform));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                SetPrivateField(overlayType, overlay, "autoFindSources", false);
                SetPrivateField(overlayType, overlay, "flightDataProvider", null);
                SetPrivateField(overlayType, overlay, "aircraftController", null);
                SetPrivateField(overlayType, overlay, "headingHud", null);
                SetPrivateField(overlayType, overlay, "headingTarget", null);
                SetPrivateField(overlayType, overlay, "_displayedHeading", 0f);

                RectTransform clipRect = root.transform.Find("Heading Tape Clip") as RectTransform;
                Assert.That(clipRect, Is.Not.Null);
                foreach (Graphic label in clipRect.GetComponentsInChildren<Graphic>(true))
                {
                    if (label.GetType().GetProperty("text") == null)
                    {
                        continue;
                    }

                    label.enabled = false;
                    label.canvasRenderer.cull = true;
                    label.canvasRenderer.SetAlpha(0f);
                }

                InvokePrivate(overlayType, overlay, "Update");

                foreach (string cardinal in new[] { "N", "E", "W", "S" })
                {
                    Graphic label = FindVisibleText(clipRect, cardinal);
                    Assert.That(label, Is.Not.Null, $"{cardinal} was not rendered.");
                    Assert.That(label.gameObject.activeInHierarchy, Is.True);
                    Assert.That(label.enabled, Is.True);
                    Assert.That(label.canvasRenderer.cull, Is.False);
                    Assert.That(label.canvasRenderer.GetAlpha(), Is.GreaterThan(0.99f));
                    Assert.That(label.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(24f));

                    RectTransform marker = label.transform.parent as RectTransform;
                    float halfLabelWidth = label.rectTransform.sizeDelta.x * 0.5f;
                    float halfClipWidth = clipRect.rect.width * 0.5f;
                    Assert.That(Mathf.Abs(marker.anchoredPosition.x) + halfLabelWidth,
                        Is.LessThanOrEqualTo(halfClipWidth + 0.01f),
                        $"{cardinal} was clipped by the heading-tape viewport.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeSanitizer_PreservesExistingHeadingTapeLayout()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaHeadingTapeOverlay, Assembly-CSharp");
            Type sanitizerType = Type.GetType("FAA.Customization.FaaHudRuntimeSanitizer, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);
            Assert.That(sanitizerType, Is.Not.Null);

            GameObject root = new GameObject("Existing Heading Tape", typeof(RectTransform));
            try
            {
                Component overlay = root.AddComponent(overlayType);
                RectTransform rect = root.GetComponent<RectTransform>();
                Vector2 expectedPosition = new Vector2(275f, -180f);
                Vector2 expectedSize = new Vector2(640f, 42f);
                rect.anchoredPosition = expectedPosition;
                rect.sizeDelta = expectedSize;

                MethodInfo configure = sanitizerType.GetMethod(
                    "ConfigureHeadingTapeLayoutIfCreated",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(configure, Is.Not.Null);
                configure.Invoke(null, new object[] { overlay, false });

                Assert.That(rect.anchoredPosition, Is.EqualTo(expectedPosition));
                Assert.That(rect.sizeDelta, Is.EqualTo(expectedSize));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeSanitizer_DoesNotOverwriteMenuSelectedHudColor()
        {
            Type sanitizerType = Type.GetType("FAA.Customization.FaaHudRuntimeSanitizer, Assembly-CSharp");
            Assert.That(sanitizerType, Is.Not.Null);

            GameObject root = new GameObject("Second Interation GUI");
            GameObject imageObject = new GameObject("Heading Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            Image image = imageObject.GetComponent<Image>();
            Color expected = new Color(0.2f, 0.75f, 1f, 0.37f);
            image.color = expected;
            image.raycastTarget = true;

            try
            {
                MethodInfo normalize = sanitizerType.GetMethod(
                    "NormalizeFlightHudSymbology",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(normalize, Is.Not.Null);
                normalize.Invoke(null, new object[] { root });

                Assert.That(image.color, Is.EqualTo(expected));
                Assert.That(image.raycastTarget, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TerrainAnchor_PreservesAuthoredTerrainLayersByDefault()
        {
            Type anchorType = Type.GetType("FAA.XPlaneIntegration.Runtime.XPlaneMappedTerrainAnchor, Assembly-CSharp");
            Assert.That(anchorType, Is.Not.Null);

            TerrainData data = new TerrainData();
            TerrainLayer authoredLayer = new TerrainLayer();
            TerrainLayer diagnosticLayer = new TerrainLayer();
            data.terrainLayers = new[] { authoredLayer };
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            try
            {
                Component anchor = terrainObject.AddComponent(anchorType);
                SetPrivateField(anchorType, anchor, "applyRenderingInEditMode", true);
                SetPrivateField(anchorType, anchor, "visibleTerrainLayer", diagnosticLayer);
                SetPrivateField(anchorType, anchor, "overrideAuthoredTerrainLayers", false);

                MethodInfo apply = anchorType.GetMethod("ApplyTerrainRendering", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(apply, Is.Not.Null);
                apply.Invoke(anchor, new object[] { terrainObject.transform });

                Assert.That(data.terrainLayers, Has.Length.EqualTo(1));
                Assert.That(data.terrainLayers[0], Is.SameAs(authoredLayer));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(terrainObject);
                UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(authoredLayer);
                UnityEngine.Object.DestroyImmediate(diagnosticLayer);
            }
        }

        [Test]
        public void WeatherProvider_HasNoSyntheticFallbackGenerator()
        {
            Type providerType = Type.GetType("WeatherRadar.XPlaneOriginalWeatherRadarProvider, WeatherRadar");
            Assert.That(providerType, Is.Not.Null);
            MethodInfo create = providerType.GetMethod(
                "CreateSimulatedFallbackTexture",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(create, Is.Null, "Weather radar must never invent returns when X-Plane is unavailable.");
        }

        [Test]
        public void RadialMenu_SubcommandsHaveNonOverlappingFanSpacing()
        {
            Type menuType = Type.GetType("VoiceControl.UI.UIToolkitRadialMenuAdvanced, Assembly-CSharp");
            Assert.That(menuType, Is.Not.Null);

            float width = ReadConstant(menuType, "SubSegmentWidth");
            float spread = ReadConstant(menuType, "SubMenuSpreadDegrees");
            float radius = 390f;
            float adjacentAngle = spread / 4f * Mathf.Deg2Rad;
            float adjacentChord = 2f * radius * Mathf.Sin(adjacentAngle * 0.5f);

            Assert.That(spread, Is.GreaterThanOrEqualTo(120f));
            Assert.That(adjacentChord, Is.GreaterThan(width + 20f));
        }

        [Test]
        public void RadialMenu_AviationPresetPlacesLauncherAtTopRight()
        {
            Type menuType = Type.GetType("VoiceControl.UI.UIToolkitRadialMenuAdvanced, Assembly-CSharp");
            Assert.That(menuType, Is.Not.Null);

            GameObject menuObject = new GameObject("Top Right Radial Menu Test");
            try
            {
                Component menu = menuObject.AddComponent(menuType);
                MethodInfo applyPreset = menuType.GetMethod("ApplyAviationHudPreset", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(applyPreset, Is.Not.Null);
                applyPreset.Invoke(menu, new object[] { false });

                Assert.That(menuType.GetProperty("CollapsedButtonTopRight")?.GetValue(menu), Is.True);
                Assert.That(
                    (Vector2)menuType.GetProperty("CollapsedButtonPosition")?.GetValue(menu),
                    Is.EqualTo(new Vector2(34f, 34f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menuObject);
            }
        }

        [TestCase("traffic_radar", "hide_panel", true)]
        [TestCase("weather_radar", "show_panel", true)]
        [TestCase("indicator_system", "hide_all_indicators", true)]
        [TestCase("symbology", "show", true)]
        [TestCase("traffic_radar", "set_range", false)]
        public void RadialMenu_VisibilityCommandsAreNotUndoneWhenMenuCloses(
            string targetId,
            string commandName,
            bool expected)
        {
            Type menuType = Type.GetType("VoiceControl.UI.UIToolkitRadialMenuAdvanced, Assembly-CSharp");
            Assert.That(menuType, Is.Not.Null);
            Type commandType = menuType.GetNestedType("MenuCommand", BindingFlags.Public);
            Assert.That(commandType, Is.Not.Null);

            object command = Activator.CreateInstance(commandType);
            commandType.GetField("TargetId").SetValue(command, targetId);
            commandType.GetField("CommandName").SetValue(command, commandName);

            MethodInfo predicate = menuType.GetMethod(
                "ShouldLetHudCommandOwnVisibility",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(predicate, Is.Not.Null);
            Assert.That(predicate.Invoke(null, new[] { command }), Is.EqualTo(expected));
        }

        [Test]
        public void RadarControls_PreserveCanonicalWeatherReadoutsWithoutDuplicateMode()
        {
            Type overlayType = Type.GetType("FAA.Customization.FaaRadarControlsOverlay, Assembly-CSharp");
            Assert.That(overlayType, Is.Not.Null);

            GameObject root = new GameObject("X-Plane Weather Radar System");
            GameObject mode = new GameObject("ModeLabel");
            GameObject source = new GameObject("SourceLabel");
            GameObject powerBadge = new GameObject("WeatherPowerBadge");
            GameObject texture = new GameObject("OriginalRadarTexture");
            mode.transform.SetParent(root.transform);
            source.transform.SetParent(root.transform);
            powerBadge.transform.SetParent(root.transform);
            texture.transform.SetParent(root.transform);

            try
            {
                MethodInfo improve = overlayType.GetMethod(
                    "ImproveWeatherLabelLegibility",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(improve, Is.Not.Null);
                improve.Invoke(null, new object[] { root.transform });

                Assert.That(mode.activeSelf, Is.False);
                Assert.That(source.activeSelf, Is.True);
                Assert.That(powerBadge.activeSelf, Is.True);
                Assert.That(texture.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(Type type, object target, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not locate {name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(Type type, object target, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not locate {methodName}.");
            method.Invoke(target, null);
        }

        private static Graphic FindVisibleText(RectTransform root, string expectedText)
        {
            foreach (Graphic label in root.GetComponentsInChildren<Graphic>(true))
            {
                PropertyInfo textProperty = label.GetType().GetProperty("text");
                if (textProperty != null &&
                    Equals(textProperty.GetValue(label), expectedText) &&
                    label.gameObject.activeInHierarchy &&
                    label.enabled)
                {
                    return label;
                }
            }

            return null;
        }

        private static float ReadConstant(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not locate {name}.");
            return (float)field.GetRawConstantValue();
        }
    }
}
