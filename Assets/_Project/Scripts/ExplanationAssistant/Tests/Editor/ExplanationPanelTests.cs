using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FAA.Explanations.Tests
{
    /// <summary>Reflection bridges the runtime Assembly-CSharp UI without moving existing HUD assemblies.</summary>
    public sealed class ExplanationPanelTests
    {
        private GameObject owner;
        private Component panel;
        private Type panelType;
        private object Value(string property) => panelType.GetProperty(property).GetValue(panel);
        private void Call(string method, params object[] args) => panelType.GetMethod(method).Invoke(panel, args);

        [SetUp]
        public void Build()
        {
            panelType = Type.GetType("FAA.Explanations.ExplanationAssistantPanel, Assembly-CSharp", true);
            owner = new GameObject("Pilot brief test fixture");
            panel = owner.AddComponent(panelType);
            if (Value("DockRect") == null) Call("RebuildView");
            Canvas.ForceUpdateCanvases();
        }
        [TearDown]
        public void Remove() { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }

        [Test]
        public void DefaultDock_IsClickOnlyAndDoesNotSendData()
        {
            Assert.That((bool)Value("IsOpen"), Is.True);
            Assert.That((bool)Value("IsResultVisible"), Is.False);
            Assert.That(owner.GetComponentsInChildren<TMP_InputField>(true), Is.Empty);
            Assert.That(owner.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "InputField"), Is.False);
            var actions = owner.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Pilot actions");
            Assert.That(actions.childCount, Is.EqualTo(4));
            Assert.That(Enumerable.Range(0, actions.childCount).Select(i => actions.GetChild(i).name),
                Is.EqualTo(new[] { "Traffic brief", "Weather brief", "Chart brief", "Status brief" }));
            var controller = Value("Controller");
            Assert.That(controller.GetType().GetProperty("Snapshot").GetValue(controller), Is.Null);
            Assert.That(controller.GetType().GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Ready"));
        }

        [Test]
        public void CardIsCompact_AndStaysBelowHeadingInReferenceLayout()
        {
            var dock = (RectTransform)Value("DockRect");
            var result = (RectTransform)Value("ResultRect");
            Assert.That(dock.sizeDelta.x, Is.LessThanOrEqualTo(520));
            Assert.That(dock.sizeDelta.y, Is.EqualTo(82));
            Assert.That(result.sizeDelta.y, Is.EqualTo(196));
            Assert.That(result.anchoredPosition.y + result.sizeDelta.y, Is.LessThanOrEqualTo(304));
            // Heading-tape bottom is y=323 at the scene's 1920x1080 reference scale.
            Assert.That(304, Is.LessThan(323));
            Assert.That(owner.GetComponentInChildren<Canvas>(true).sortingOrder, Is.EqualTo(5400));
            Assert.That((bool)Value("IsWide"), Is.False);
        }

        [TestCase(640f)] [TestCase(1280f)] [TestCase(1920f)] [TestCase(3840f)]
        public void CardSizeDoesNotGrowIntoAWorkspace(float width)
        {
            var size = (Vector2)panelType.GetMethod("CompactSize").Invoke(null, new object[] { new Vector2(width, 1080) });
            Assert.That(size.x, Is.LessThanOrEqualTo(520));
            Assert.That(size.y, Is.EqualTo(196));
        }

        [Test]
        public void HideRestoreAndDetailTabsDoNotCaptureOrChangeHud()
        {
            var controller = Value("Controller");
            var result = (RectTransform)Value("ResultRect");
            for (int tab = 0; tab < 4; tab++)
            {
                Call("SetTab", tab);
                Assert.That((bool)Value("IsResultVisible"), Is.True);
                Assert.That(result.sizeDelta.y, Is.EqualTo(196));
            }
            Call("HideResult");
            Assert.That((bool)Value("IsOpen"), Is.True);
            Assert.That((bool)Value("IsResultVisible"), Is.False);
            Call("SetOpen", false);
            Assert.That(((RectTransform)Value("DockRect")).gameObject.activeSelf, Is.False);
            Call("SetOpen", true);
            Assert.That(((RectTransform)Value("DockRect")).gameObject.activeSelf, Is.True);
            Assert.That(controller.GetType().GetProperty("Snapshot").GetValue(controller), Is.Null);
            Assert.That(Value("Controller"), Is.SameAs(controller));
        }

        [Test]
        public void UnknownButtonActionIsANoOp()
        {
            Call("AskQuick", "Execute");
            Assert.That(Value("ActiveAction"), Is.Null);
            Assert.That((bool)Value("IsResultVisible"), Is.False);
            var controller = Value("Controller");
            Assert.That(controller.GetType().GetProperty("State").GetValue(controller).ToString(), Is.EqualTo("Ready"));
        }

        [Test]
        public void LongBriefHasExplicitPagesInsteadOfClippedBottomLines()
        {
            Call("ShowResult");
            var text = owner.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Streamed brief");
            text.text = string.Join("\n", Enumerable.Repeat("Picture: a deliberately long test observation with evidence [E1].", 20));
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate(true);
            Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Page));
            Assert.That(text.textInfo.pageCount, Is.GreaterThan(1));
            panelType.GetMethod("UpdatePages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
            Call("ChangeBriefPage", 1);
            Assert.That((int)Value("CurrentBriefPage"), Is.EqualTo(2));
            Call("ChangeBriefPage", -1);
            Assert.That((int)Value("CurrentBriefPage"), Is.EqualTo(1));
        }
    }
}
