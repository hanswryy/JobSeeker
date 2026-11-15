using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using JobGame;
using JobGame.Logic;

namespace JobGame.UI
{
    public sealed class SessionHudPresenter : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] GameContent content;
        [SerializeField] GameConfig config;
        [SerializeField] int seed = 42;

        [Header("HUD")]
        [SerializeField] TMP_Text stateText;
        [SerializeField] TMP_Text mentalValueText;
        [SerializeField] Slider energySlider;
        [SerializeField] TMP_Text runwayValueText;

        [Header("Run Result")]
        [SerializeField] GameObject winPanel;
        [SerializeField] GameObject losePanel;
        [SerializeField] TMP_Text winScoreText;

        [Header("Run Result Actions")]
        [SerializeField] Button winNewRunButton;
        [SerializeField] Button winQuitButton;
        [SerializeField] Button loseNewRunButton;
        [SerializeField] Button loseQuitButton;

        [Header("Actions")]
        [SerializeField] Button restButton;
        [SerializeField] Button endWeekButton;

        [Header("Action Confirmations")]
        [SerializeField] GameObject confirmationPanel;
        [SerializeField] GameObject utubeWarningPanel;
        [SerializeField] Button utubeConfirmButton;
        [SerializeField] GameObject sleepWarningPanel;
        [SerializeField] Button sleepConfirmButton;

        [Header("Audio")]
        [SerializeField] AudioClip bgmClip;
        [SerializeField] AudioClip selectSfx;
        [SerializeField] AudioClip openAppSfx;
        [SerializeField] AudioClip confirmationSfx;
        [SerializeField] AudioClip closeAppSfx;

        IGameSession _session;
        string _message = "Sesi belum dimulai.";

        public IGameSession Session { get { return _session; } }
        public event Action<IGameSession> SessionBound;

        void Awake()
        {
            HideConfirmationPanel();

            AudioManager.Ensure(
                bgmClip,
                selectSfx,
                openAppSfx,
                confirmationSfx,
                closeAppSfx);
        }

        void OnEnable()
        {
            if (restButton != null)
                restButton.onClick.AddListener(OnRestClicked);
            if (endWeekButton != null)
                endWeekButton.onClick.AddListener(OnEndWeekClicked);
            if (utubeConfirmButton != null)
                utubeConfirmButton.onClick.AddListener(OnRestConfirmedClicked);
            if (sleepConfirmButton != null)
                sleepConfirmButton.onClick.AddListener(OnSleepConfirmedClicked);
            if (winNewRunButton != null)
                winNewRunButton.onClick.AddListener(OnNewRunClicked);
            if (loseNewRunButton != null)
                loseNewRunButton.onClick.AddListener(OnNewRunClicked);
            if (winQuitButton != null)
                winQuitButton.onClick.AddListener(OnQuitClicked);
            if (loseQuitButton != null)
                loseQuitButton.onClick.AddListener(OnQuitClicked);

            Subscribe(_session);
            Refresh();
        }

        void Start()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.InstallButtonSfx();

            if (!ValidateSetup())
                return;

            ConfigureEnergySlider();
            StartNewRun(seed);
        }

        void OnDisable()
        {
            if (restButton != null)
                restButton.onClick.RemoveListener(OnRestClicked);
            if (endWeekButton != null)
                endWeekButton.onClick.RemoveListener(OnEndWeekClicked);
            if (utubeConfirmButton != null)
                utubeConfirmButton.onClick.RemoveListener(OnRestConfirmedClicked);
            if (sleepConfirmButton != null)
                sleepConfirmButton.onClick.RemoveListener(OnSleepConfirmedClicked);
            if (winNewRunButton != null)
                winNewRunButton.onClick.RemoveListener(OnNewRunClicked);
            if (loseNewRunButton != null)
                loseNewRunButton.onClick.RemoveListener(OnNewRunClicked);
            if (winQuitButton != null)
                winQuitButton.onClick.RemoveListener(OnQuitClicked);
            if (loseQuitButton != null)
                loseQuitButton.onClick.RemoveListener(OnQuitClicked);

            Unsubscribe(_session);
        }

        public void Bind(IGameSession session)
        {
            Unsubscribe(_session);
            _session = session;
            HideConfirmationPanel();
            Subscribe(_session);
            if (_session == null || _session.Summary == null)
                HideOutcomePanels();
            else
            {
                _message = _session.Summary.Headline;
                ShowOutcomePanels(_session.Summary);
            }
            Refresh();
            if (SessionBound != null)
                SessionBound(_session);
        }

        public void StartNewRun(int newSeed)
        {
            if (content == null || config == null)
            {
                Debug.LogError("SessionHudPresenter membutuhkan GameContent dan GameConfig.", this);
                return;
            }

            IGameFactory factory = new GameFactory(content, config);
            Bind(factory.NewRun(newSeed));
            _message = "Run baru dimulai.";
            Refresh();
        }

        public void OnRestClicked()
        {
            if (_session == null)
                return;

            OpenConfirmation(utubeWarningPanel, sleepWarningPanel);
        }

        void OnRestConfirmedClicked()
        {
            if (_session == null)
            {
                Debug.LogError("Tidak dapat mengonfirmasi UTUBE karena sesi belum dimulai.", this);
                HideConfirmationPanel();
                return;
            }

            Execute("Menonton UTUBE", _session.TryRest());
            HideConfirmationPanel();
        }

        public void OnEndWeekClicked()
        {
            if (_session == null)
                return;

            OpenConfirmation(sleepWarningPanel, utubeWarningPanel);
        }

        void OnSleepConfirmedClicked()
        {
            if (_session == null)
            {
                Debug.LogError("Tidak dapat mengonfirmasi Sleep karena sesi belum dimulai.", this);
                HideConfirmationPanel();
                return;
            }

            Execute("Tidur", _session.EndWeek());
            HideConfirmationPanel();
        }

        void OpenConfirmation(GameObject warningPanel, GameObject otherWarningPanel)
        {
            if (confirmationPanel == null || warningPanel == null || otherWarningPanel == null)
            {
                Debug.LogError(
                    "SessionHudPresenter: assign Confirmation Panel serta panel konfirmasi UTUBE dan Sleep.",
                    this);
                return;
            }

            otherWarningPanel.SetActive(false);
            warningPanel.SetActive(true);
            confirmationPanel.SetActive(true);
            confirmationPanel.transform.SetAsLastSibling();
        }

        void HideConfirmationPanel()
        {
            if (confirmationPanel != null)
                confirmationPanel.SetActive(false);
        }

        void OnNewRunClicked()
        {
            int sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
            if (sceneBuildIndex < 0)
            {
                Debug.LogError(
                    "Scene aktif belum ditambahkan ke Build Settings; tidak dapat memulai ulang.",
                    this);
                return;
            }

            SceneManager.LoadScene(sceneBuildIndex);
        }

        void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        bool ValidateSetup()
        {
            if (content == null || config == null ||
                stateText == null || mentalValueText == null ||
                energySlider == null || runwayValueText == null ||
                winPanel == null || losePanel == null || winPanel == losePanel ||
                winScoreText == null ||
                confirmationPanel == null ||
                utubeWarningPanel == null || utubeConfirmButton == null ||
                sleepWarningPanel == null || sleepConfirmButton == null ||
                winNewRunButton == null || winQuitButton == null ||
                loseNewRunButton == null || loseQuitButton == null ||
                restButton == null || endWeekButton == null)
            {
                Debug.LogError(
                    "SessionHudPresenter: assign GameContent, GameConfig, state/mental/runway text, " +
                    "Energy Slider, Win Panel, Lose Panel yang berbeda, Text Score di Win Panel, " +
                    "tombol New Run dan Quit pada kedua panel hasil, serta tombol Istirahat dan " +
                    "End Week, Confirmation Panel, panel warning UTUBE/Sleep, dan tombol konfirmasi Yes " +
                    "UTUBE/Sleep di Inspector.",
                    this);
                return false;
            }

            return true;
        }

        void ConfigureEnergySlider()
        {
            energySlider.minValue = 0f;
            energySlider.maxValue = 100f;
            energySlider.wholeNumbers = false;
            energySlider.interactable = false;
        }

        void Execute(string label, ActionResult result)
        {
            _message = result.Ok
                ? label + " berhasil."
                : label + " gagal: " + result.Error + " - " + result.Message;

            if (!result.Ok)
                Debug.LogWarning(_message, this);

            Refresh();
        }

        void Refresh()
        {
            if (_session == null)
            {
                SetText(stateText, _message);
                SetText(mentalValueText, "-");
                SetText(runwayValueText, "-");
                if (energySlider != null)
                    energySlider.SetValueWithoutNotify(0f);
                SetActionAvailability(false, false);
                return;
            }

            string marketLabel = _session.Market == null ||
                string.IsNullOrEmpty(_session.Market.Label)
                ? "Normal"
                : _session.Market.Label;
            SetText(stateText, string.Format(
                "Minggu {0}/{1}\nFase: {2}\nSlot tersisa: {3}\nPasar: {4}\n{5}",
                _session.Week,
                _session.TotalWeeks,
                _session.Phase,
                _session.SlotsLeft,
                marketLabel,
                _message));
            if (energySlider != null)
                energySlider.SetValueWithoutNotify(_session.Energy);
            SetText(mentalValueText, string.Format("{0:0.#}/100", _session.Mental));
            SetText(runwayValueText, string.Format("{0:0.#} minggu", _session.Runway));

            bool planning = _session.Phase == GamePhase.Planning;
            SetActionAvailability(planning && _session.SlotsLeft > 0, planning);
        }

        void SetActionAvailability(bool canAct, bool canEndWeek)
        {
            if (restButton != null)
                restButton.interactable = canAct;
            if (utubeConfirmButton != null)
                utubeConfirmButton.interactable = canAct;
            if (sleepConfirmButton != null)
                sleepConfirmButton.interactable = canEndWeek;
            if (endWeekButton != null)
                endWeekButton.interactable = canEndWeek;
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value;
        }

        void Subscribe(IGameSession session)
        {
            if (session == null || !isActiveAndEnabled)
                return;

            session.WeekStarted += OnStateChanged;
            session.SlotsChanged += OnStateChanged;
            session.ResourceChanged += OnResourceChanged;
            session.MarketChanged += OnMarketChanged;
            session.PostingsChanged += OnStateChanged;
            session.HoldingsChanged += OnStateChanged;
            session.ApplicationUpdated += OnApplicationUpdated;
            session.PhaseChanged += OnPhaseChanged;
            session.InterviewStarted += OnInterviewStarted;
            session.OfferReceived += OnOfferReceived;
            session.RunEnded += OnRunEnded;
        }

        void Unsubscribe(IGameSession session)
        {
            if (session == null)
                return;

            session.WeekStarted -= OnStateChanged;
            session.SlotsChanged -= OnStateChanged;
            session.ResourceChanged -= OnResourceChanged;
            session.MarketChanged -= OnMarketChanged;
            session.PostingsChanged -= OnStateChanged;
            session.HoldingsChanged -= OnStateChanged;
            session.ApplicationUpdated -= OnApplicationUpdated;
            session.PhaseChanged -= OnPhaseChanged;
            session.InterviewStarted -= OnInterviewStarted;
            session.OfferReceived -= OnOfferReceived;
            session.RunEnded -= OnRunEnded;
        }

        void OnStateChanged() { Refresh(); }
        void OnStateChanged(int unused) { Refresh(); }
        void OnResourceChanged(ResourceKind kind, float oldValue, float newValue) { Refresh(); }
        void OnMarketChanged(MarketEvent market) { Refresh(); }
        void OnApplicationUpdated(ApplicationRecord application) { Refresh(); }
        void OnPhaseChanged(GamePhase phase) { Refresh(); }
        void OnInterviewStarted(IInterviewSession interview) { Refresh(); }
        void OnOfferReceived(Offer offer) { Refresh(); }

        void OnRunEnded(RunSummary summary)
        {
            if (summary == null)
            {
                Debug.LogError("RunEnded mengirim summary kosong.", this);
                return;
            }

            _message = summary.Headline;
            ShowOutcomePanels(summary);
            Refresh();
        }

        void HideOutcomePanels()
        {
            if (winPanel != null)
                winPanel.SetActive(false);
            if (losePanel != null)
                losePanel.SetActive(false);
            SetText(winScoreText, string.Empty);
        }

        void ShowOutcomePanels(RunSummary summary)
        {
            if (winPanel == null || losePanel == null || winScoreText == null)
            {
                Debug.LogError(
                    "SessionHudPresenter: assign Win Panel, Lose Panel, dan Text Score di Win Panel " +
                    "di Inspector.",
                    this);
                return;
            }
            if (winPanel == losePanel)
            {
                Debug.LogError("Win Panel dan Lose Panel harus GameObject yang berbeda.", this);
                return;
            }

            bool won = summary.Outcome == RunOutcome.Won;
            bool lost = summary.Outcome == RunOutcome.LostRunway ||
                summary.Outcome == RunOutcome.LostMental ||
                summary.Outcome == RunOutcome.LostTimeUp;
            if (!won && !lost)
            {
                Debug.LogError("RunOutcome tidak dikenali: " + summary.Outcome + ".", this);
                HideOutcomePanels();
                return;
            }

            SetText(winScoreText, won ? "Skor akhir: " + summary.FinalScore : string.Empty);
            winPanel.SetActive(won);
            losePanel.SetActive(lost);
            (won ? winPanel : losePanel).transform.SetAsLastSibling();
        }
    }
}
