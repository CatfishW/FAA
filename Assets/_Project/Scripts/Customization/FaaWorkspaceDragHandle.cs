using UnityEngine;
using UnityEngine.EventSystems;

namespace FAA.Customization
{
    /// <summary>Only edit-mode grips own layout drags; the actual radar retains its normal map gestures.</summary>
    public sealed class FaaWorkspaceDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        private FaaSpatialWorkspace owner;
        private string targetId;
        private bool resize;
        public void Configure(FaaSpatialWorkspace workspace, string id, bool resizeOnly) { owner = workspace; targetId = id; resize = resizeOnly; }
        public void OnBeginDrag(PointerEventData e)
        {
            if (owner == null || !owner.EditMode || e.button != PointerEventData.InputButton.Left) return;
            owner.BeginMouseManipulation(targetId, e.position, resize); e.Use();
        }
        public void OnDrag(PointerEventData e)
        {
            if (owner == null || !owner.EditMode) return;
            owner.UpdateMouseManipulation(e.position); e.Use();
        }
        public void OnEndDrag(PointerEventData e) { owner?.EndMouseManipulation(); e.Use(); }
        public void OnPointerClick(PointerEventData e) { if (owner != null && owner.EditMode) owner.Select(targetId); }
        public void OnScroll(PointerEventData e)
        {
            if (owner == null || !owner.EditMode) return;
            owner.Select(targetId); owner.AdjustSelectedScale(e.scrollDelta.y * .05f); e.Use();
        }
        private void OnDisable() { if (owner != null) owner.EndMouseManipulation(); }
    }
}
