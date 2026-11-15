using UnityEngine;
using UnityEngine.UI;

namespace JobGame.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonSfx : MonoBehaviour
    {
        [SerializeField] UiSfxKind kind = UiSfxKind.Select;

        Button _button;

        public void SetKind(UiSfxKind newKind)
        {
            kind = newKind;
        }

        void Awake()
        {
            _button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            _button.onClick.AddListener(Play);
        }

        void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(Play);
        }

        void Play()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(kind);
        }
    }
}
