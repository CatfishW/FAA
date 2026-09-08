using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FAA.Customization
{
    /// <summary>Explanations work with mouse, touch, keyboard selection and XR rays.</summary>
    [DisallowMultipleComponent]
    public sealed class FaaRadarControlHint : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler
    {
        private TMP_Text _help;
        private string _explanation, _fallback;

        public void Configure(TMP_Text help, string explanation, string fallback)
        {
            _help = help;
            _explanation = explanation;
            _fallback = fallback;
        }

        private void Show() { if (_help != null) _help.text = _explanation; }
        private void ResetHint()
        {
            if (_help != null && _help.text == _explanation) _help.text = _fallback;
        }
        private void OnDisable() => ResetHint();
        public void OnPointerEnter(PointerEventData e) => Show();
        public void OnPointerDown(PointerEventData e) => Show();
        public void OnSelect(BaseEventData e) => Show();
        public void OnPointerExit(PointerEventData e) => ResetHint();
        public void OnDeselect(BaseEventData e) => ResetHint();
    }
}
