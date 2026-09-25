using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace FAA.Customization
{
    public static class FaaWorkspaceUi
    {
        public static readonly Color Background=new(.025f,.044f,.065f,1), Card=new(.045f,.08f,.11f,1), Accent=new(.28f,.9f,.79f,1), Muted=new(.62f,.73f,.81f,1);
        public static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        public static Image Box(string name,Transform parent,float x,float y,float width,float height,Color tint,bool hit=false)
        {var rect=Rect(name,parent,x,y,width,height);var image=rect.gameObject.AddComponent<Image>();image.color=tint;image.raycastTarget=hit;return image;}
        public static TMP_Text Text(string name,Transform parent,string value,float x,float y,float width,float height,float size=18,bool left=false)
        {
            var rect=Rect(name,parent,x,y,width,height);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=TMP_Settings.defaultFontAsset;text.fontSize=size;text.text=value;text.richText=false;text.raycastTarget=false;
            text.color=Color.white;text.alignment=left?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        public static Button Button(string name,Transform parent,string caption,float x,float y,float width,float height,UnityEngine.Events.UnityAction action)
        {
            var image=Box(name,parent,x,y,width,height,Card,true);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.72f,1,.95f);colors.pressedColor=new Color(.4f,.8f,.7f);colors.disabledColor=new Color(.3f,.3f,.3f);button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);
            var label=Text(name+" Caption",image.transform,caption,width*.5f,height*.5f,width-12,height-4,width<165?13:17);
            label.textWrappingMode=TextWrappingModes.NoWrap;return button;
        }
        public static Canvas Canvas(string name,Transform parent,int order)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(FaaCanvasPixelRaycaster),typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(parent,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=order;
            return canvas;
        }
    }
}
