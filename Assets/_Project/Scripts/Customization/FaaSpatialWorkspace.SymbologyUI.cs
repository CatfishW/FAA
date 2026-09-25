using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaWorkspaceUi;
namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        private RectTransform symbologyPage;
        private Button digitalVersionButton,classicVersionButton;
        private TMP_Text selectedVersionText,attitudeChoiceText,motionChoiceText,paletteChoiceText;
        private void BuildSymbologyPage()
        {
            symbologyPage=Rect("Symbology Versions Page",menuRoot,320,352,640,354);
            Text("Symbology Section",symbologyPage,"CHOOSE YOUR FLIGHT DISPLAY",320,24,592,32,21);
            digitalVersionButton=Button("Digital Symbology",symbologyPage,"DIGITAL",167,81,284,66,()=>SetSymbologyVersion(FaaSymbologyVersion.Digital));
            classicVersionButton=Button("Classic Analog Symbology",symbologyPage,"CLASSIC ANALOG",473,81,284,66,()=>SetSymbologyVersion(FaaSymbologyVersion.ClassicAnalog));
            Text("Digital Description",symbologyPage,"Current numeric readouts\nand linear engine scales",167,139,280,46,14).color=Muted;
            Text("Classic Description",symbologyPage,"Reference-style round dials\nwith animated needles",473,139,280,46,14).color=Muted;
            selectedVersionText=Text("Current Symbology",symbologyPage,"",320,190,592,30,18);
            var center=Button("Classic Attitude Choice",symbologyPage,"",320,231,592,36,()=>SetClassicInstrumentAttitude(!ClassicInstrumentAttitude));
            attitudeChoiceText=center.GetComponentInChildren<TMP_Text>();
            var motion=Button("Symbology Motion",symbologyPage,"",167,277,284,36,()=>SetReducedSymbologyMotion(!ReducedSymbologyMotion));
            motionChoiceText=motion.GetComponentInChildren<TMP_Text>();
            var palette=Button("Symbology Palette",symbologyPage,"",473,277,284,36,()=>SetPilotSymbologyColor(!UsePilotSymbologyColor));
            paletteChoiceText=palette.GetComponentInChildren<TMP_Text>();
            Text("Style Persistence Note",symbologyPage,"Each version remembers its instrument sizes.\nRadar positions and calibrated scene cues remain separate.",320,329,592,44,14).color=Muted;
            symbologyPage.gameObject.SetActive(false);RefreshSymbologyControls();
        }
        public void OpenSymbologySettings()
        {
            if(dataSourcePage!=null)dataSourcePage.gameObject.SetActive(false);
            OpenMenu();hudPage.gameObject.SetActive(false);panelsPage.gameObject.SetActive(false);
            if(symbologyPage!=null)symbologyPage.gameObject.SetActive(true);RefreshSymbologyControls();
        }
        private void RefreshSymbologyControls()
        {
            if(symbologyPage==null)return;
            bool classic=CurrentSymbology==FaaSymbologyVersion.ClassicAnalog;
            digitalVersionButton.GetComponent<Image>().color=!classic?new Color(.07f,.31f,.28f):Card;
            classicVersionButton.GetComponent<Image>().color=classic?new Color(.07f,.31f,.28f):Card;
            selectedVersionText.text="ACTIVE: "+(classic?"CLASSIC ANALOG":"DIGITAL");selectedVersionText.color=Accent;
            attitudeChoiceText.text=ClassicInstrumentAttitude?"CLASSIC CENTER: ATTITUDE INSTRUMENT":"CLASSIC CENTER: CONFORMAL SCENE CUES";
            motionChoiceText.text=ReducedSymbologyMotion?"MOTION: DIRECT / REDUCED":"MOTION: SMOOTH NEEDLES";
            paletteChoiceText.text=UsePilotSymbologyColor?"COLOR: PILOT PALETTE":"COLOR: REFERENCE GREEN";
        }
    }
}
