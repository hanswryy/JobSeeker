using UnityEngine;
using UnityEngine.EventSystems;

namespace JobGame.UI
{
    [DisallowMultipleComponent]
    public sealed class CvMakerDragSource : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        CvMakerCategoryPresenter _presenter;
        int _slotIndex;

        internal void Bind(CvMakerCategoryPresenter presenter, int slotIndex)
        {
            _presenter = presenter;
            _slotIndex = slotIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_presenter != null)
                _presenter.BeginDrag(_slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_presenter != null)
                _presenter.DragCard(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_presenter != null)
                _presenter.EndDrag();
        }
    }
}
