using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace FAA.Customization.Tests
{
    public class FaaRadarBriefDragTests
    {
        private static Type Footprint => Type.GetType("FAA.Customization.FaaRadarGroupFootprint, Assembly-CSharp",true);
        private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)obj.transform;
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=size;rect.anchoredPosition=position;obj.AddComponent<Image>();return rect;
        }
        [TestCase(false)] [TestCase(true)]
        public void CompleteEnvelopeIncludesInactiveAndFadedDrawers(bool active)
        {
            var host=new GameObject("Envelope fixture",typeof(RectTransform),typeof(Canvas));
            try
            {
                var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var radar=Rect("radar",host.transform,new Vector2(200,200),Vector2.zero);
                var drawer=Rect("drawer",host.transform,new Vector2(450,250),new Vector2(100,300));
                drawer.gameObject.AddComponent<CanvasGroup>().alpha=0;drawer.gameObject.SetActive(active);
                object model=Activator.CreateInstance(Footprint);
                var bounds=(Vector2)Footprint.GetMethod("Measure").Invoke(model,new object[]{canvas,radar});
                Assert.That(bounds.x,Is.GreaterThan(1.6f));Assert.That(bounds.y,Is.GreaterThan(2.1f));
                drawer.gameObject.SetActive(false);
                var after=(Vector2)Footprint.GetMethod("Measure").Invoke(model,new object[]{canvas,radar});
                Assert.That(after,Is.EqualTo(bounds));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [Test] public void ClippedMapTilesDoNotEnlargeThePanel()
        {
            var host=new GameObject("Clipping fixture",typeof(RectTransform),typeof(Canvas));
            try
            {
                var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var radar=Rect("radar",host.transform,new Vector2(200,200),Vector2.zero);radar.gameObject.AddComponent<RectMask2D>();
                Rect("large tile",radar,new Vector2(20000,20000),Vector2.zero);
                object model=Activator.CreateInstance(Footprint);
                var size=(Vector2)Footprint.GetMethod("Measure").Invoke(model,new object[]{canvas,radar});
                Assert.That(size.x,Is.EqualTo(.56f).Within(.001f));Assert.That(size.y,Is.EqualTo(.56f).Within(.001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [Test] public void EnvelopeIgnoresWorldTranslationAndRotation()
        {
            var host=new GameObject("Moving fixture",typeof(RectTransform),typeof(Canvas));
            try
            {
                var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var radar=Rect("radar",host.transform,new Vector2(200,200),Vector2.zero);
                Rect("drawer",host.transform,new Vector2(300,200),new Vector2(200,300));
                object model=Activator.CreateInstance(Footprint);
                var before=(Vector2)Footprint.GetMethod("Measure").Invoke(model,new object[]{canvas,radar});
                host.transform.SetPositionAndRotation(new Vector3(90000,300,300000),Quaternion.Euler(20,80,15));
                var after=(Vector2)Footprint.GetMethod("Measure").Invoke(model,new object[]{canvas,radar});Assert.That(after,Is.EqualTo(before));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [Test] public void PanelPointerCaptureBlocksCameraLookUntilRelease()
        {
            Type type=Type.GetType("AircraftControl.Camera.AircraftCameraController, AircraftControl",true);
            var host=new GameObject("Inactive look fixture");host.SetActive(false);
            try
            {
                var component=host.AddComponent(type);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var process=type.GetMethod("ProcessLookInput",flags);
                type.GetMethod("SetPanelPointerCapture").Invoke(component,new object[]{true});
                process.Invoke(component,new object[]{true,new Vector2(25,10),false,.016f});
                Assert.That((bool)type.GetProperty("IsLookActive").GetValue(component),Is.False);
                type.GetMethod("SetPanelPointerCapture").Invoke(component,new object[]{false});
                process.Invoke(component,new object[]{true,new Vector2(25,10),false,.016f});
                Assert.That((bool)type.GetProperty("IsLookActive").GetValue(component),Is.False,"Leaving a captured panel while still held cannot become look input.");
                process.Invoke(component,new object[]{false,Vector2.zero,false,.016f});
                process.Invoke(component,new object[]{true,new Vector2(1,0),false,.016f});
                Assert.That((bool)type.GetProperty("IsLookActive").GetValue(component),Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [TestCase(0f)] [TestCase(25f)] [TestCase(-20f)]
        public void ProtectedWholeGroupAlwaysClearsForwardCone(float yaw)
        {
            Type entryType=Type.GetType("FAA.Customization.FaaSpatialLayoutEntry, Assembly-CSharp",true);
            Type math=Type.GetType("FAA.Customization.FaaPeripheralPanelLayout, Assembly-CSharp",true);
            object entry=Activator.CreateInstance(entryType);entryType.GetField("id").SetValue(entry,"weather");
            entryType.GetField("yaw").SetValue(entry,yaw);entryType.GetField("elevation").SetValue(entry,-25f);
            entryType.GetField("scale").SetValue(entry,1.6f);entryType.GetField("distance").SetValue(entry,.55f);
            math.GetMethod("Protect").Invoke(null,new object[]{entry,2f,1.5f,-1f});
            float resultYaw=(float)entryType.GetField("yaw").GetValue(entry);
            float radius=(float)math.GetMethod("AngularRadius").Invoke(null,new object[]{2f,1.5f,1.6f,.55f});
            float angle=Mathf.Acos(Mathf.Cos(resultYaw*Mathf.Deg2Rad)*Mathf.Cos(-25*Mathf.Deg2Rad))*Mathf.Rad2Deg;
            Assert.That(angle-radius,Is.GreaterThanOrEqualTo(62.99f));
        }
    }
}
