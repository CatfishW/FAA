using System.Collections.Generic;
using FAA.HUDToolkit;
using UnityEngine;

namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        public FaaSymbologyVersion CurrentSymbology {get;private set;}=FaaSymbologyVersion.Digital;
        public FaaClassicAnalogHud ClassicHud {get;private set;}
        public bool ClassicInstrumentAttitude {get;private set;}=true;
        public bool ReducedSymbologyMotion {get;private set;}
        public bool UsePilotSymbologyColor {get;private set;}
        public bool UsesClassicAttitude => isActiveAndEnabled && CurrentSymbology==FaaSymbologyVersion.ClassicAnalog && ClassicInstrumentAttitude && ClassicHud!=null && ClassicHud.Visible;
        private readonly List<FaaNonConformalScaleTarget> digitalModules=new(),classicModules=new();
        private readonly List<StyleGate> digitalGates=new();
        private readonly Dictionary<string,Transform> classicSourceRoots=new();
        private readonly Dictionary<string,HUDControl.Core.HUDElementBase> classicSourceElements=new();
        private SymbologyColorManager colorManager;
        private string symbologyKey;
        private sealed class StyleGate
        {
            public CanvasGroup group;public bool created;public float alpha;public bool interactable,blocks;
            public HUDControl.Core.HUDElementBase source;
            public void Set(bool visible)
            {
                if(group==null)return;
                visible&=source==null||source.IsEnabled;
                group.alpha=visible?alpha:0;group.interactable=visible&&interactable;group.blocksRaycasts=visible&&blocks;
            }
            public void Restore(){Set(true);if(created&&group!=null)Destroy(group);}
        }
        private void BindSymbologyVersions(Canvas flight,Transform originalRoot,Canvas headingCanvas)
        {
            digitalModules.Clear();digitalModules.AddRange(modules);
            foreach(var m in digitalModules)
            {
                classicSourceRoots[m.Id]=m.Target;
                classicSourceElements[m.Id]=m.Target.GetComponent<HUDControl.Core.HUDElementBase>();
            }
            if(originalRoot!=null)foreach(Transform child in originalRoot)AddStyleGate(child);
            if(headingCanvas!=null)
            {
                var root=headingCanvas.transform.Find("FAA Heading Tape Overlay");if(root!=null)AddStyleGate(root);
            }
            var go=new GameObject("FAA Classic Analog Symbology",typeof(RectTransform));
            ClassicHud=go.AddComponent<FaaClassicAnalogHud>();ClassicHud.Build(flight,originalRoot);
            foreach(var pair in ClassicHud.InstrumentRoots)
                classicModules.Add(new FaaNonConformalScaleTarget(pair.Key,ClassicHud.Captions[pair.Key],pair.Value,1f));
            colorManager=FindFirstObjectByType<SymbologyColorManager>();
            symbologyKey=profileKey+".Symbology.v1";
            var selected=FaaSymbologyVersion.Digital;
            ClassicInstrumentAttitude=true;ReducedSymbologyMotion=false;UsePilotSymbologyColor=false;
            if(PersistChanges&&PlayerPrefs.HasKey(symbologyKey)&&FaaSymbologyPreferences.TryParse(PlayerPrefs.GetString(symbologyKey),out var saved))
            {
                // Existing v1 workspace remains the canonical Digital profile; classic scales are separate.
                foreach(var item in saved.classic)foreach(var target in classicModules)if(item.id==target.Id)target.Layout.scale=item.scale;
                selected=saved.selected;ClassicInstrumentAttitude=saved.localAttitude;
                ReducedSymbologyMotion=saved.reducedMotion;UsePilotSymbologyColor=saved.pilotColor;
            }
            CurrentSymbology=selected;
            modules.Clear();modules.AddRange(selected==FaaSymbologyVersion.Digital?digitalModules:classicModules);
            ApplySymbologyPresentation();
        }
        private void AddStyleGate(Transform root)
        {
            // Unity can return a destroyed/native-null wrapper; CLR ?? does not honor its null check.
            var existing=root.GetComponent<CanvasGroup>();var g=existing!=null?existing:root.gameObject.AddComponent<CanvasGroup>();
            digitalGates.Add(new StyleGate{group=g,created=existing==null,alpha=g.alpha,interactable=g.interactable,blocks=g.blocksRaycasts,
                source=root.GetComponent<HUDControl.Core.HUDElementBase>()});
        }
        public bool SetSymbologyVersion(FaaSymbologyVersion version)
        {
            if(!System.Enum.IsDefined(typeof(FaaSymbologyVersion),version)||ClassicHud==null)return false;
            LaptopCamera?.CancelSizing();CancelManipulation();gestures?.CancelPointers();
            // An explicit style choice returns from the separate developer UI Toolkit renderer.
            var backend=FindFirstObjectByType<FaaHudModeSwitcher>();
            if(backend!=null&&backend.ActiveMode!=FaaHudModeSwitcher.HudMode.LegacyUGUI)backend.UseLegacyHud();
            CurrentSymbology=version;
            modules.Clear();modules.AddRange(version==FaaSymbologyVersion.Digital?digitalModules:classicModules);
            if(GetPanel(SelectedId)==null&&!modules.Exists(m=>m.Id==SelectedId))SelectedId="airspeed";
            ApplySymbologyPresentation();RefreshInstrumentPicker();
            MarkChanged();RefreshTransforms();RefreshControls();SaveNow();return true;
        }
        public void SetClassicInstrumentAttitude(bool enabled)
        {ClassicInstrumentAttitude=enabled;ApplySymbologyPresentation();RefreshInstrumentPicker();MarkChanged();RefreshControls();SaveNow();}
        public void SetReducedSymbologyMotion(bool enabled)
        {ReducedSymbologyMotion=enabled;ApplySymbologyPresentation();MarkChanged();RefreshControls();SaveNow();}
        public void SetPilotSymbologyColor(bool enabled)
        {UsePilotSymbologyColor=enabled;ApplySymbologyPresentation();MarkChanged();RefreshControls();SaveNow();}
        private void ApplySymbologyPresentation()
        {
            if(ClassicHud==null)return;
            bool classic=CurrentSymbology==FaaSymbologyVersion.ClassicAnalog;
            foreach(var gate in digitalGates)gate.Set(!classic);
            ClassicHud.LocalAttitude=ClassicInstrumentAttitude;
            // Explicit desktop side inspection gives the controls visual priority without
            // changing canvas mode, layout or telemetry. Native XR retains normal depth/order.
            ClassicHud.PanelInspectionOpacity=(!NativeXr&&desktopView!=null&&desktopView.IsPanelInspectionActive) ? .12f : 1f;
            ClassicHud.ReducedMotion=ReducedSymbologyMotion;
            ClassicHud.ReferenceGreen=UsePilotSymbologyColor&&colorManager!=null?colorManager.CurrentColor:new Color(117f/255f,187f/255f,64f/255f,1);
            ClassicHud.SetVisible(classic);ClassicHud.ApplyLayout();
            foreach(var pair in ClassicHud.InstrumentRoots)
                if(classicSourceRoots.TryGetValue(pair.Key,out var source)&&source!=null)
                {
                    bool visible=source.gameObject.activeInHierarchy;
                    if(classicSourceElements.TryGetValue(pair.Key,out var element)&&element!=null)visible&=element.IsEnabled;
                    if(pair.Value.gameObject.activeSelf!=visible)pair.Value.gameObject.SetActive(visible);
                }
        }
        private void RefreshInstrumentPicker()
        {
            selectionIds.Clear();
            foreach(var m in modules)if(m.TryScreenBounds(View,out _))selectionIds.Add(m.Id);
        }
        private IEnumerable<FaaNonConformalScaleTarget> DigitalProfileModules => digitalModules.Count>0?digitalModules:modules;
        public string ExportSymbologyPreferences()
        {
            var preferences=new FaaSymbologyPreferences{selected=CurrentSymbology,localAttitude=ClassicInstrumentAttitude,
                reducedMotion=ReducedSymbologyMotion,pilotColor=UsePilotSymbologyColor};
            foreach(var m in digitalModules)preferences.digital.Add(new FaaSymbologyScale{id=m.Id,scale=m.Layout.scale});
            foreach(var m in classicModules)preferences.classic.Add(new FaaSymbologyScale{id=m.Id,scale=m.Layout.scale});
            return JsonUtility.ToJson(preferences);
        }
        private void SaveSymbologyPreferences()
        {if(!string.IsNullOrEmpty(symbologyKey)&&ClassicHud!=null)PlayerPrefs.SetString(symbologyKey,ExportSymbologyPreferences());}
        private void ReleaseSymbologyVersions()
        {
            foreach(var gate in digitalGates)gate.Restore();digitalGates.Clear();
            if(ClassicHud!=null){ClassicHud.SetVisible(false);Destroy(ClassicHud.gameObject);}ClassicHud=null;
            modules.Clear();modules.AddRange(digitalModules);classicModules.Clear();digitalModules.Clear();
            CurrentSymbology=FaaSymbologyVersion.Digital;symbologyKey=null;
            classicSourceRoots.Clear();classicSourceElements.Clear();
        }
    }
}
