using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace JobGame.UI
{
    [DisallowMultipleComponent]
    public sealed class CvMakerDropZone : MonoBehaviour, IDropHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        CvMakerCategoryPresenter _presenter;
        bool _isExperienceZone;
        Graphic _graphic;
        TMP_Text _instruction;
        Color _normalColor;
        Color _highlightColor;

        internal void Bind(CvMakerCategoryPresenter presenter, bool isExperienceZone)
        {
            _presenter = presenter;
            _isExperienceZone = isExperienceZone;
        }

        internal void ConfigureVisual(Sprite dashedSprite, string instruction)
        {
            _graphic = GetComponent<Graphic>();
            if (_graphic == null)
            {
                Image image = gameObject.AddComponent<Image>();
                image.color = Color.white;
                _graphic = image;
            }

            Image background = _graphic as Image;
            if (background != null && dashedSprite != null)
            {
                background.sprite = dashedSprite;
                background.type = Image.Type.Sliced;
            }

            _normalColor = new Color(0.35f, 0.75f, 0.5f, 0.45f);
            _highlightColor = new Color(0.08f, 0.32f, 0.2f, 0.92f);
            _graphic.color = _normalColor;
            _graphic.raycastTarget = true;

            TMP_Text template = GetComponentInChildren<TMP_Text>(true);
            GameObject instructionObject = new GameObject("Drop Instruction", typeof(RectTransform));
            instructionObject.transform.SetParent(transform, false);
            _instruction = instructionObject.AddComponent<TextMeshProUGUI>();
            if (template != null)
            {
                _instruction.font = template.font;
                _instruction.fontSize = template.fontSize;
                _instruction.fontStyle = template.fontStyle;
            }

            _instruction.text = instruction;
            _instruction.alignment = TextAlignmentOptions.Center;
            _instruction.color = new Color(0.11f, 0.36f, 0.25f, 0.9f);
            _instruction.raycastTarget = false;

            RectTransform instructionRect = instructionObject.GetComponent<RectTransform>();
            LayoutElement layoutElement = instructionObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            instructionRect.anchorMin = new Vector2(0f, 0f);
            instructionRect.anchorMax = new Vector2(1f, 1f);
            instructionRect.offsetMin = new Vector2(20f, 8f);
            instructionRect.offsetMax = new Vector2(-20f, -8f);
            instructionObject.transform.SetAsFirstSibling();
        }

        internal void SetFeedback(bool highlighted)
        {
            if (_graphic != null)
                _graphic.color = highlighted ? _highlightColor : _normalColor;
            if (_instruction != null)
                _instruction.color = highlighted
                    ? Color.white
                    : new Color(0.11f, 0.36f, 0.25f, 0.9f);
        }

        internal void SetInstructionVisible(bool visible)
        {
            if (_instruction != null)
                _instruction.gameObject.SetActive(visible);
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_presenter != null)
                _presenter.SetDropZoneHover(_isExperienceZone, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_presenter != null)
                _presenter.SetDropZoneHover(_isExperienceZone, false);
        }
    }
}
