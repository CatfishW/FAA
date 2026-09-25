using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaHudTerrainReleaseTests
    {
        [TestCase(.4f)] [TestCase(.72f)] [TestCase(1f)] [TestCase(1.6f)]
        public void VsiNumbersClearRightRailAndPointerAtEverySize(float scale)
        {
            Type type=Type.GetType("FAA.Customization.FaaClassicDeviationGraphic, Assembly-CSharp",true);
            var go=new GameObject("VSI label verification",typeof(RectTransform));
            try
            {
                var graphic=go.AddComponent(type);var kind=Enum.Parse(type.GetNestedType("Scale"),"VerticalSpeed");
                type.GetMethod("Configure").Invoke(graphic,new object[]{kind,Color.green});
                type.GetMethod("Present").Invoke(graphic,new object[]{1000f,true,Color.green,null});
                go.transform.localScale=Vector3.one*scale;
                int labels=0;
                foreach(var text in go.GetComponentsInChildren<TMP_Text>())
                {
                    if(!text.name.StartsWith("VSI ",StringComparison.Ordinal))continue;
                    labels++;text.ForceMeshUpdate(true,true);
                    float left=text.rectTransform.anchoredPosition.x+text.rectTransform.rect.xMin;
                    Assert.That((left-15.8f)*scale,Is.GreaterThan(8f),"Numeric column must not straddle rail.");
                    Assert.That((left-29.7f)*scale,Is.GreaterThan(3f),"Live pointer and outline must not touch digits.");
                    Assert.That(text.alignment,Is.EqualTo(TextAlignmentOptions.MidlineLeft));
                }
                Assert.That(labels,Is.EqualTo(4));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        private static Type Connection=>Type.GetType("FAA.XPlaneIntegration.Runtime.XPlaneTerrainConnection, Assembly-CSharp",true);
        [TestCase("http://127.0.0.1:12679",true)]
        [TestCase("https://terrain.example.test/base/",true)]
        [TestCase("ftp://example.test",false)]
        [TestCase("http://user:secret@example.test",false)]
        [TestCase("http://example.test/?token=secret",false)]
        [TestCase("http://example.test/#fragment",false)]
        [TestCase("",false)]
        public void TerrainEndpointRejectsCredentialsAndUnsupportedSchemes(string value,bool expected)
        {
            object[] args={value,null};Assert.That((bool)Connection.GetMethod("TryNormalize").Invoke(null,args),Is.EqualTo(expected));
        }
        [Test]
        public void ExplicitTerrainOverridePrecedence()
        {
            object[] args={"http://127.0.0.1:1000","{\"terrainUrl\":\"http://127.0.0.1:2000\"}","http://127.0.0.1:3000",new[]{"--terrain-url","http://127.0.0.1:4000"},null,null};
            Assert.That((bool)Connection.GetMethod("TryResolve").Invoke(null,args),Is.True);
            Assert.That(args[4],Is.EqualTo("http://127.0.0.1:4000"));
            args[3]=new[]{"--terrain-url=http://127.0.0.1:5000/"};
            Assert.That((bool)Connection.GetMethod("TryResolve").Invoke(null,args),Is.True);
            Assert.That(args[4],Is.EqualTo("http://127.0.0.1:5000"));
        }
        [Test]
        public void MissingTerrainOverrideValueFailsClosed()
        {
            object[] args={"http://127.0.0.1:12679",null,null,new[]{"--terrain-url"},null,null};
            Assert.That((bool)Connection.GetMethod("TryResolve").Invoke(null,args),Is.False);Assert.That(args[4],Is.Null);
        }
    }
}
