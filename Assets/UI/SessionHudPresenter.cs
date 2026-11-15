using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

        [Header("Actions")]
        [SerializeField] Button restButton;
        [SerializeField] Button endWeekButton;

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

            Unsubscribe(_session);
        }

        public void Bind(IGameSession session)
        {
            Unsubscribe(_session);
            _session = session;
            Subscribe(_session);
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

            Execute("Istirahat", _session.TryRest());
        }

        public void OnEndWeekClicked()
        {
            if (_session == null)
                return;

            Execute("Akhiri minggu", _session.EndWeek());
        }

        bool ValidateSetup()
        {
            if (content == null || config == null ||
                stateText == null || mentalValueText == null ||
                energySlider == null || runwayValueText == null ||
                restButton == null || endWeekButton == null)
            {
                Debug.LogError(
                    "SessionHudPresenter: assign GameContent, GameConfig, state/mental/runway text, " +
                    "Energy Slider, serta tombol Istirahat dan End Week di Inspector.",
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
            _message = summary.Headline;
            Refresh();
        }
    }
}
