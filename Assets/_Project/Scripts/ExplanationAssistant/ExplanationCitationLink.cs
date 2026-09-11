using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FAA.Explanations
{
    public sealed class ExplanationCitationLink : MonoBehaviour, IPointerClickHandler
    {
        [NonSerialized] public TMP_Text Text;
        [NonSerialized] public Action<string> Clicked;
        public void OnPointerClick(PointerEventData e)
        {
            if (Text == null) return;
            int index = TMP_TextUtilities.FindIntersectingLink(Text, e.position, e.pressEventCamera);
            if (index >= 0) Clicked?.Invoke(Text.textInfo.linkInfo[index].GetLinkID());
        }
        private void OnEnable() { if (TryGetComponent<TMP_Text>(out var text)) text.raycastTarget = true; }
    }
}
