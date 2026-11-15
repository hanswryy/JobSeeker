using UnityEngine;
using UnityEngine.UI;

namespace JobGame.UI
{
    public enum UiSfxKind
    {
        Select,
        OpenApp,
        Confirmation,
        CloseApp
    }

    public sealed class AudioManager : MonoBehaviour
    {
        static AudioManager _instance;

        [SerializeField] AudioClip bgmClip;
        [SerializeField] AudioClip selectSfx;
        [SerializeField] AudioClip openAppSfx;
        [SerializeField] AudioClip confirmationSfx;
        [SerializeField] AudioClip closeAppSfx;
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;

        AudioSource _bgmSource;
        AudioSource _sfxSource;

        public static AudioManager Instance { get { return _instance; } }

        public static AudioManager Ensure(
            AudioClip mainTheme,
            AudioClip selectClip,
            AudioClip openClip,
            AudioClip confirmationClip,
            AudioClip closeClip)
        {
            AudioManager manager = _instance;
            if (manager == null)
                manager = FindObjectOfType<AudioManager>();

            if (manager == null)
            {
                GameObject managerObject = new GameObject("Audio Manager");
                manager = managerObject.AddComponent<AudioManager>();
            }

            manager.Configure(
                mainTheme,
                selectClip,
                openClip,
                confirmationClip,
                closeClip);
            manager.InstallButtonSfx();
            return manager;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreateSources();
            ApplyBgm();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void Configure(
            AudioClip mainTheme,
            AudioClip selectClip,
            AudioClip openClip,
            AudioClip confirmationClip,
            AudioClip closeClip)
        {
            bgmClip = mainTheme;
            selectSfx = selectClip;
            openAppSfx = openClip;
            confirmationSfx = confirmationClip;
            closeAppSfx = closeClip;
            CreateSources();
            ApplyBgm();
        }

        public void PlaySelect()
        {
            Play(UiSfxKind.Select);
        }

        public void PlayOpenApp()
        {
            Play(UiSfxKind.OpenApp);
        }

        public void PlayConfirmation()
        {
            Play(UiSfxKind.Confirmation);
        }

        public void PlayCloseApp()
        {
            Play(UiSfxKind.CloseApp);
        }

        public void Play(UiSfxKind kind)
        {
            AudioClip clip = selectSfx;
            switch (kind)
            {
                case UiSfxKind.OpenApp:
                    clip = openAppSfx;
                    break;
                case UiSfxKind.Confirmation:
                    clip = confirmationSfx;
                    break;
                case UiSfxKind.CloseApp:
                    clip = closeAppSfx;
                    break;
            }

            PlaySfx(clip);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;

            CreateSources();
            _sfxSource.PlayOneShot(clip, sfxVolume);
        }

        void CreateSources()
        {
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.spatialBlend = 0f;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f;
            }
        }

        void ApplyBgm()
        {
            if (_bgmSource == null)
                return;

            _bgmSource.volume = bgmVolume;
            if (_bgmSource.clip == bgmClip && _bgmSource.isPlaying)
                return;

            _bgmSource.Stop();
            _bgmSource.clip = bgmClip;
            if (bgmClip != null)
                _bgmSource.Play();
        }

        public void InstallButtonSfx()
        {
            Button[] buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].GetComponent<ButtonSfx>() != null)
                    continue;

                ButtonSfx buttonSfx = buttons[i].gameObject.AddComponent<ButtonSfx>();
                buttonSfx.SetKind(InferKind(buttons[i]));
            }
        }

        static UiSfxKind InferKind(Button button)
        {
            string buttonName = button.gameObject.name.ToLowerInvariant();
            if (buttonName.Contains("close") || buttonName.Contains("back"))
                return UiSfxKind.CloseApp;
            if (buttonName.Contains("apply"))
                return UiSfxKind.OpenApp;
            if (buttonName.Contains("yes") ||
                buttonName.Contains("no") ||
                buttonName.Contains("rest") ||
                buttonName.Contains("week") ||
                buttonName.Contains("learn") ||
                buttonName.Contains("accept") ||
                buttonName.Contains("decline"))
                return UiSfxKind.Confirmation;
            return UiSfxKind.Select;
        }
    }
}
