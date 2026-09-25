using UnityEngine;
namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        public Canvas SettingsCanvas { get; private set; }
        public FaaSpatialRadarPanel RegisterUtility(string id,Canvas canvas,RectTransform root,float yaw,float width=.74f)
        {
            var previous=GetPanel(id);if(previous!=null){previous.Dispose();utilityPanels.Remove(previous);}
            var utility=new FaaSpatialRadarPanel(this,id,canvas,root,yaw,true,width);
            utilityPanels.Add(utility);utility.SetSpatial(true);return utility;
        }
        private void LoadUtilityProfile()
        {
            if(!PersistChanges||!PlayerPrefs.HasKey(profileKey)||!FaaSpatialLayoutMath.TryReadProfile(PlayerPrefs.GetString(profileKey),out var profile))return;
            foreach(var saved in profile.entries)foreach(var panel in utilityPanels)
                if(saved.id==panel.Id)
                {panel.Layout.yaw=saved.yaw;panel.Layout.elevation=saved.elevation;panel.Layout.distance=saved.distance;panel.Layout.scale=saved.scale;}
        }
        public void BringUtilityHere(string id)
        {
            // Compatibility entry point: recovery now means the safe side slot, never the pilot's gaze.
            StowUtility(id);
        }
        public void StowUtility(string id)
        {
            var panel=GetPanel(id);if(panel==null||!panel.IsUtility)return;
            CancelManipulation();panel.Layout.yaw=panel.DefaultLayout.yaw;panel.Layout.elevation=panel.DefaultLayout.elevation;
            panel.Layout.distance=panel.DefaultLayout.distance;
            MarkChanged();RefreshTransforms();
        }
        public void InspectUtility(string id)
        {
            var panel=GetPanel(id);if(panel==null||!panel.IsUtility||View==null||NativeXr)return;
            if(id=="settings")OpenMenu();
            else if(LaptopCamera!=null&&!LaptopCamera.PanelOpen)LaptopCamera.TogglePanel();
            RefreshTransforms();
            var camera=View.GetComponent<AircraftControl.Camera.AircraftCameraController>();
            // Aim slightly below the panel so its controls sit above the fixed target-bearing
            // and heading readouts. No HUD canvas/property is moved to make room.
            camera?.BeginPanelInspection(panel.Layout.yaw,panel.Layout.elevation-10f);
        }
        public void InspectPanel(string id)
        {
            var panel=GetPanel(id);if(panel==null||View==null||NativeXr)return;
            if(panel.IsUtility){InspectUtility(id);return;}
            RefreshTransforms();
            View.GetComponent<AircraftControl.Camera.AircraftCameraController>()?.BeginPanelInspection(panel.Layout.yaw,panel.Layout.elevation+8f);
        }
        public void ReturnToForwardView()
        { if(!NativeXr&&View!=null)View.GetComponent<AircraftControl.Camera.AircraftCameraController>()?.ResetView(); }
        public void ResizeUtility(string id,float delta)
        {var panel=GetPanel(id);if(panel!=null)SetScale(id,panel.Layout.scale+delta);}
        public void SetPanelDistance(string id,float delta)
        {
            var panel=GetPanel(id);if(panel==null)return;
            panel.Layout.distance=Mathf.Clamp(panel.Layout.distance+delta,.55f,3f);MarkChanged();RefreshTransforms();
        }
    }
}
