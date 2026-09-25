using System.Collections.Generic;
using UnityEngine;
namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        public FaaLaptopCameraGestures LaptopCamera { get; private set; }
        private readonly Dictionary<string,float> webcamBaseline = new();
        public bool WebcamSizing { get; private set; }
        private void BindLaptopCamera()
        {
            LaptopCamera=GetComponent<FaaLaptopCameraGestures>()??gameObject.AddComponent<FaaLaptopCameraGestures>();
            LaptopCamera.Bind(this);
        }
        public bool BeginWebcamSizing()
        {
            if(!Initialized||!EditMode||NativeXr||IsManipulating) return false;
            webcamBaseline.Clear();
            foreach(var module in modules)webcamBaseline[module.Id]=module.Layout.scale;
            WebcamSizing=webcamBaseline.Count>0;return WebcamSizing;
        }
        public void ApplyWebcamSizing(float factor)
        {
            if(!WebcamSizing||!Initialized||!EditMode||NativeXr||!FaaSpatialLayoutMath.Finite(factor)||factor<=0)return;
            float lower=.01f,upper=100;
            foreach(var pair in webcamBaseline)
            {
                lower=Mathf.Max(lower,FaaSpatialLayoutMath.MinScale/pair.Value);
                upper=Mathf.Min(upper,FaaSpatialLayoutMath.MaxScale/pair.Value);
            }
            factor=Mathf.Clamp(factor,lower,upper);
            foreach(var module in modules)
                if(webcamBaseline.TryGetValue(module.Id,out float scale))module.Layout.scale=scale*factor;
            // Main workspace LateUpdate applies the current scale once per render frame.
            // UI text is refreshed at its own bounded cadence, not once per camera packet.
            MarkChanged();
        }
        public void EndWebcamSizing()
        {
            bool changed=WebcamSizing;WebcamSizing=false;webcamBaseline.Clear();
            if(changed)SaveNow();
        }
    }
}
