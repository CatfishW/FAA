using System.Linq;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using static FAA.Customization.FaaWorkspaceUi;

namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        private RectTransform dataSourcePage;
        private TMP_Text sourceDetails,sourceCandidates,sourceMode;
        private void BuildDataSourcePage()
        {
            dataSourcePage=Rect("Data Sources Page",menuRoot,320,352,640,354);
            sourceMode=Text("Source Selection Mode",dataSourcePage,"AUTOMATIC LOCAL DISCOVERY",320,24,600,30,17);
            sourceDetails=Text("Source Details",dataSourcePage,"",320,94,594,100,14);
            Button("Auto Sources",dataSourcePage,"AUTO + FALLBACK",116,173,184,36,()=>XPlaneSourceDiscovery.Active?.SetMode(XPlaneSourceDiscovery.SelectionMode.Automatic));
            Button("Local Sources Only",dataSourcePage,"LOCAL ONLY",320,173,204,36,()=>XPlaneSourceDiscovery.Active?.SetMode(XPlaneSourceDiscovery.SelectionMode.LocalOnly));
            Button("Fallback Source Only",dataSourcePage,"FALLBACK ONLY",524,173,184,36,()=>XPlaneSourceDiscovery.Active?.SetMode(XPlaneSourceDiscovery.SelectionMode.FallbackOnly));
            Button("Rescan Sources",dataSourcePage,"RESCAN",116,219,184,34,()=>XPlaneSourceDiscovery.Active?.Rescan());
            Button("Cycle Valid Source",dataSourcePage,"NEXT VALID SOURCE",320,219,204,34,()=>
            {
                var source=XPlaneSourceDiscovery.Active;if(source==null)return;var ids=source.CandidateIds;
                if(ids.Length>0)source.PreferSource(ids[(System.Array.IndexOf(ids,source.SelectedSourceId)+1)%ids.Length]);
            });
            Button("Stop Data Sources",dataSourcePage,"STOP DATA",524,219,184,34,()=>XPlaneSourceDiscovery.Active?.SetMode(XPlaneSourceDiscovery.SelectionMode.Stopped));
            sourceCandidates=Text("Source Candidates",dataSourcePage,"",320,279,594,70,11);
            Text("Read Only Sources",dataSourcePage,"Read-only discovery. Retained MQTT data is not live proof.\nA fallback is labelled and may belong to another simulator.",320,333,594,36,11).color=Muted;
            dataSourcePage.gameObject.SetActive(false);
        }
        public void OpenDataSourceSettings()
        {
            OpenMenu();hudPage.gameObject.SetActive(false);panelsPage.gameObject.SetActive(false);symbologyPage.gameObject.SetActive(false);
            dataSourcePage.gameObject.SetActive(true);InspectUtility("settings");RefreshDataSourceControls();
        }
        private void RefreshDataSourceControls()
        {
            if(dataSourcePage==null||!dataSourcePage.gameObject.activeSelf)return;
            var source=XPlaneSourceDiscovery.Active;
            sourceMode.text=source==null?"SOURCE DISCOVERY NOT RUNNING":"MODE: "+source.Mode.ToString().ToUpperInvariant();
            sourceDetails.text=source==null?"Start the live FAA bridge to detect data sources.":source.SourceSummary+"\n"+source.Details;
            sourceCandidates.text=source==null?"":source.InventorySummary+"\n"+
                (source.CandidateDescriptions.Length>0?string.Join("\n",source.CandidateDescriptions.Take(2)):source.DiscoveryDiagnostic);
        }
    }
}
