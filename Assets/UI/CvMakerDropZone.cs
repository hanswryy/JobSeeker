using UnityEngine;
using UnityEngine.EventSystems;

namespace JobGame.UI
{
    [DisallowMultipleComponent]
    public sealed class CvMakerDropZone : MonoBehaviour, IDropHandler
    {
        CvMakerCategoryPresenter _presenter;
        bool _isExperienceZone;

        internal void Bind(CvMakerCategoryPresenter presenter, bool isExperienceZone)
        {
            _presenter = presenter;
            _isExperienceZone = isExperienceZone;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_presenter == null)
                return;

            if (_isExperienceZone)
                _presenter.DropOnExperiences();
            else
                _presenter.DropOnSkills();
        }
    }
}
