using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public class FaaRadarSettingsTests
    {
        private static Type Overlay => Type.GetType("FAA.Customization.FaaRadarControlsOverlay, Assembly-CSharp");
        private GameObject _host;
        private Component _overlay;
        private RectTransform _strip;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Readable Settings Test", typeof(RectTransform));
            _host.SetActive(false);
            _overlay = _host.AddComponent(Overlay);
            var stripObject = new GameObject("Test Settings Strip", typeof(RectTransform));
            stripObject.transform.SetParent(_host.transform, false);
            _strip = stripObject.GetComponent<RectTransform>();
            _strip.sizeDelta = new Vector2(476, 276);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_host);

        [TestCase("WX", "Weather")]
        [TestCase("WX_T", "Weather + turbulence")]
        [TestCase("TURB", "Turbulence")]
        [TestCase("MAP", "Ground map")]
        [TestCase("STBY", "Standby")]
        public void WeatherModes_UseCompleteNames(string mode, string expected)
        {
            var method = Overlay.GetMethod("ReadableWeatherMode");
            var value = Enum.Parse(method.GetParameters()[0].ParameterType, mode);
            Assert.That(method.Invoke(null, new[] { value }), Is.EqualTo(expected));
        }

        [TestCase("Sectional", "Sectional chart")]
        [TestCase("Terminal Area", "Terminal area chart")]
        [TestCase("World Aeronautical", "World aeronautical chart")]
        [TestCase("Street", "Street map")]
        [TestCase("Custom", "Custom map")]
        [TestCase(null, "No map source")]
        public void MapSources_AreNotCrypticCodes(string source, string expected) =>
            Assert.That(Overlay.GetMethod("ReadableMapSource").Invoke(null, new object[] { source }), Is.EqualTo(expected));

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void SettingsPages_HaveFourNamedCardsAndBoundedReadableControls(bool traffic, int page)
        {
            Build(traffic, page);
            var root = _strip.Find("Readable Settings");
            var cards = root.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.Find("Setting Name") != null).ToArray();
            Assert.That(cards.Length, Is.EqualTo(4));
            foreach (var card in cards)
            {
                Assert.That(card.Find("Setting Name").GetComponent<TMP_Text>().text.Length, Is.GreaterThan(8));
                foreach (var rect in card.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect == card || !rect.gameObject.activeSelf) continue;
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card, rect);
                    var cardRect = ((RectTransform)card).rect;
                    Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(cardRect.xMin - .1f), rect.name);
                    Assert.That(bounds.max.x, Is.LessThanOrEqualTo(cardRect.xMax + .1f), rect.name);
                }
            }
            foreach (var button in _strip.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.activeSelf))
            {
                Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(32));
                var text = button.GetComponentInChildren<TMP_Text>(true).text;
                Assert.That(new[] { "T-", "G+", "M-", "R+", "BKG", "REF", "CHT", "MORE", "LESS", "REST" }, Does.Not.Contain(text));
                Assert.That(button.GetComponent(Type.GetType("FAA.Customization.FaaRadarControlHint, Assembly-CSharp")), Is.Not.Null);
            }
        }

        [Test]
        public void SettingFocus_ExplainsTheControlWithoutOpeningAnotherPopup()
        {
            Build(false, 0);
            var button = _strip.GetComponentsInChildren<Button>(true).First(b => b.name == "WXTiltUp");
            var hintType = Type.GetType("FAA.Customization.FaaRadarControlHint, Assembly-CSharp");
            var hint = button.GetComponent(hintType);
            hintType.GetMethod("OnPointerDown").Invoke(hint, new object[] { null });
            var help = _strip.Find("Readable Settings/Help").GetComponent<TMP_Text>();
            Assert.That(help.text, Does.Contain("0.5°"));
            Assert.That(help.text, Does.Contain("above the horizon"));
            hintType.GetMethod("OnPointerExit").Invoke(hint, new object[] { null });
            Assert.That(help.text, Does.Contain("tap the radar again to close"));
        }

        [Test]
        public void FullMapToolbar_FitsCompleteSourceAndActionNames()
        {
            _strip.sizeDelta = new Vector2(796, 110);
            Overlay.GetMethod("EnsureReadableFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_overlay, new object[] { _strip });
            foreach (var button in _strip.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<TMP_Text>();
                label.font = TMP_Settings.defaultFontAsset;
                label.fontSharedMaterial = label.font.material;
                if (button.name == "TCASFocusSource") label.text = "World aeronautical chart";
                float needed = label.GetPreferredValues(label.text).x;
                Assert.That(needed, Is.LessThanOrEqualTo(label.rectTransform.rect.width), label.text);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_strip, button.transform);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(_strip.rect.xMax + .1f), button.name);
            }
        }

        [Test]
        public void Labels_TolerateRetiredUnityReferencesDuringEditorReload()
        {
            var retired = new GameObject("Retired weather root", typeof(RectTransform));
            Set("_weatherRoot", retired.transform);
            UnityEngine.Object.DestroyImmediate(retired);
            Assert.DoesNotThrow(() => Overlay.GetMethod("RefreshReadableValues", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_overlay, null));
        }

        [TestCase("TrafficRangeDown")]
        [TestCase("TrafficRangeUp")]
        public void ManualRangeSelection_DisablesAutoRange(string action)
        {
            var type = Type.GetType("TrafficRadar.Core.TrafficRadarController, TrafficRadar");
            var controller = _host.AddComponent(type);
            Set("_trafficController", controller);
            type.GetProperty("AutoRangeEnabled").SetValue(controller, true);
            Overlay.GetMethod(action).Invoke(_overlay, null);
            Assert.That(type.GetProperty("AutoRangeEnabled").GetValue(controller), Is.False);
        }

        [Test]
        public void TiltAndGain_UseTheDisplayedUnitsAndKeepThePanelOpen()
        {
            var type = Type.GetType("WeatherRadar.WeatherRadarDataProvider, WeatherRadar");
            var provider = _host.AddComponent(type);
            Set("_weatherDataProvider", provider);
            Set("_weatherConfigurationVisible", true);
            Build(false, 0);
            _strip.GetComponentsInChildren<Button>(true).First(b => b.name == "WXTiltUp").onClick.Invoke();
            _strip.GetComponentsInChildren<Button>(true).First(b => b.name == "WXGainDown").onClick.Invoke();
            var texts = _strip.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts.First(t => t.name == "WXTiltValue").text, Is.EqualTo("+0.5°"));
            Assert.That(texts.First(t => t.name == "WXGainValue").text, Is.EqualTo("-1 dB"));
            Assert.That(Overlay.GetField("_weatherConfigurationVisible", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_overlay), Is.True);
        }

        [TestCase(220f)]
        [TestCase(360f)]
        [TestCase(560f)]
        public void TrafficSettings_DockBelowHudAndClearOfTheQuickMenu(float radarWidth)
        {
            var position = (Vector2)Overlay.GetMethod("CalculateTrafficSettingsDock").Invoke(null, new object[] { new Vector2(-28, 28), radarWidth });
            float menuLeft = -28 - radarWidth - 20 - 284;
            Assert.That(position.x, Is.LessThanOrEqualTo(menuLeft - 12));
            Assert.That(position.y + 276, Is.LessThan(320), "Keep below the heading tape and primary instruments.");
            Assert.That(position.x + 1920 - 476, Is.GreaterThan(300), "Leave the weather scope clear at the 16:9 reference layout.");
        }

        private void Build(bool traffic, int page)
        {
            Set(traffic ? "_trafficSettingsPage" : "_weatherSettingsPage", page);
            Overlay.GetMethod(traffic ? "EnsureReadableTrafficControls" : "EnsureReadableWeatherControls", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_overlay, new object[] { _strip });
            Overlay.GetMethod("RefreshReadableValues", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_overlay, null);
        }

        private void Set(string field, object value) => Overlay.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_overlay, value);
    }
}
