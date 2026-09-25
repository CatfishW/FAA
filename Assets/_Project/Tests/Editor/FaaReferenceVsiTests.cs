using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public sealed class FaaReferenceVsiTests
    {
        private static Type Geometry => Type.GetType("FAA.Customization.FaaClassicVsiGeometry, Assembly-CSharp",true);
        private static float Value(string method,float input)=>(float)Geometry.GetMethod(method).Invoke(null,new object[]{input});
        private static Rect Body(float input)=>(Rect)Geometry.GetMethod("PointerBody").Invoke(null,new object[]{input});

        [TestCase(-5000,-120)] [TestCase(-2000,-120)] [TestCase(-1000,-60)]
        [TestCase(0,0)] [TestCase(500,30)] [TestCase(1000,60)] [TestCase(2000,120)] [TestCase(5000,120)]
        public void CalibrationRemainsFeetPerMinuteAndClampsOnlyPointer(float fpm,float y)=>
            Assert.That(Value("ValueToY",fpm),Is.EqualTo(y).Within(.0001f));

        [TestCase(0,-13)] [TestCase(12,-1)] [TestCase(24,11)] [TestCase(28,14)]
        [TestCase(32,15)] [TestCase(60,15)] [TestCase(136,15)]
        public void RightRailMatchesNotchAndRoundedShoulder(float y,float expected)
        {
            Assert.That(Value("RightBoundary",y),Is.EqualTo(expected).Within(.0001f));
            Assert.That(Value("RightBoundary",-y),Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(24)] [TestCase(32)]
        public void PointerContourHasContinuousPositionAndSlope(float boundary)
        {
            const float h=.01f;
            float a=Value("RightBoundary",boundary-h),b=Value("RightBoundary",boundary),c=Value("RightBoundary",boundary+h);
            Assert.That(Mathf.Abs(c-a),Is.LessThan(.021f));
            Assert.That(Mathf.Abs((b-a)/h-(c-b)/h),Is.LessThan(.01f));
        }

        [Test]
        public void PointerHasReferenceBodyAtZeroAndCannotCoverNumbers()
        {
            Rect zero=Body(0);Assert.That(zero.xMin,Is.EqualTo(-1));Assert.That(zero.width,Is.EqualTo(30));
            Assert.That(zero.height,Is.EqualTo(11));Assert.That(zero.center.y,Is.EqualTo(0));
            foreach(float fpm in new[]{-2000f,-1000f,1000f,2000f})
            {
                Rect body=Body(fpm);
                Assert.That(body.xMin,Is.EqualTo(27f));
                Assert.That(body.yMin,Is.LessThan(Value("ValueToY",fpm)));
                Assert.That(body.xMin,Is.GreaterThan(11f),"Moving body remains outside the interior text column.");
            }
        }

        [Test]
        public void PointerSweepNeverOverlapsAnyDigitRectangle()
        {
            for(int fpm=-2200;fpm<=2200;fpm+=10)
            {
                float y=Value("ValueToY",fpm),tip=Value("RightBoundary",y)+2;
                Rect arrowBounds=new Rect(tip,y-3.8f,7f,7.6f),body=Body(fpm);
                foreach(int label in new[]{-2,-1,1,2})
                {
                    Rect digit=new Rect(1,label*60-12.5f,10,25);
                    Assert.That(digit.Overlaps(body)||digit.Overlaps(arrowBounds),Is.False,$"Pointer at {fpm} FPM overlaps {label}.");
                }
            }
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonfiniteInputCannotGeneratePointerVertices(float value)
        {
            using(var mesh=new VertexHelper())
            {
                Geometry.GetMethod("DrawPointer").Invoke(null,new object[]{mesh,value,Color.green});
                Assert.That(mesh.currentVertCount,Is.Zero);
            }
        }

        [Test]
        public void ReferenceMeshContainsRoundedBodyAndFiniteTriangles()
        {
            using(var vh=new VertexHelper())
            {
                Geometry.GetMethod("DrawScale").Invoke(null,new object[]{vh,Color.green});int baseCount=vh.currentVertCount;
                Geometry.GetMethod("DrawPointer").Invoke(null,new object[]{vh,0f,Color.green});
                Assert.That(vh.currentVertCount-baseCount,Is.GreaterThan(60),"Rounded filled body is real vector geometry.");
                UIVertex vertex=default;
                for(int i=0;i<vh.currentVertCount;i++)
                {
                    vh.PopulateUIVertex(ref vertex,i);
                    Assert.That(float.IsNaN(vertex.position.x)||float.IsInfinity(vertex.position.x),Is.False);
                    Assert.That(float.IsNaN(vertex.position.y)||float.IsInfinity(vertex.position.y),Is.False);
                    Assert.That(vertex.position.y,Is.InRange(-137f,137f));
                }
            }
        }

        [Test]
        public void InvalidReadingKeepsScaleButHidesPointerAndAnnunciates()
        {
            Type type=Type.GetType("FAA.Customization.FaaClassicDeviationGraphic, Assembly-CSharp",true);
            var go=new GameObject("Reference VSI validity test",typeof(RectTransform));
            try
            {
                var graphic=go.AddComponent(type);var kind=Enum.Parse(type.GetNestedType("Scale"),"VerticalSpeed");
                type.GetMethod("Configure").Invoke(graphic,new object[]{kind,Color.green});
                var populate=type.GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null);
                using(var vh=new VertexHelper())
                {
                    type.GetMethod("Present").Invoke(graphic,new object[]{0f,true,Color.green,null});
                    populate.Invoke(graphic,new object[]{vh});int validCount=vh.currentVertCount;
                    type.GetMethod("Present").Invoke(graphic,new object[]{float.NaN,true,Color.green,null});
                    populate.Invoke(graphic,new object[]{vh});Assert.That(vh.currentVertCount,Is.LessThan(validCount));
                    Assert.That(vh.currentVertCount,Is.GreaterThan(20));
                    Assert.That(go.transform.Find("Value").GetComponent<TMP_Text>().text,Is.EqualTo("NO DATA"));
                    type.GetMethod("Present").Invoke(graphic,new object[]{3000f,true,Color.green,null});
                    Assert.That(go.transform.Find("Value").GetComponent<TMP_Text>().text,Does.Contain("OFF SCALE").And.Contain("+3000"));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
