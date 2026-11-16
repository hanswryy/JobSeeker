using System.Collections;
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
        [SerializeField] GameObject slotAlertPanel;

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
        Canvas _feedbackCanvas;
        RectTransform _feedbackCanvasRect;
        Transform _desktopPanel;
        GameObject _slotBarRoot;
        Image[] _slotSegments;
        GameObject _toastRoot;
        TMP_Text _toastText;
        CanvasGroup _toastCanvasGroup;
        Image _transitionOverlay;
        CanvasGroup _transitionCanvasGroup;
        Coroutine _toastRoutine;
        Coroutine _transitionRoutine;

        public IGameSession Session { get { return _session; } }
        public event Action<IGameSession> SessionBound;

        void Awake()
        {
            HideConfirmationPanel();
            SetSlotAlertVisible(false);
            EnsureFeedbackUi();

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

        public void ShowToast(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EnsureFeedbackUi();
            if (_toastText == null || _toastCanvasGroup == null)
                return;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastText.text = message;
            _toastRoutine = StartCoroutine(ShowToastRoutine());
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
                energySlider == null || runwayValueText == null || slotAlertPanel == null ||
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
                    "Energy Slider, Slot Alert Panel, Win Panel, Lose Panel yang berbeda, " +
                    "Text Score di Win Panel, " +
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
            if (result.Ok && label == "Tidur")
                PlayEndWeekTransition();
        }

        void Refresh()
        {
            bool showSlotAlert = _session != null &&
                _session.Phase == GamePhase.Planning && _session.SlotsLeft <= 0;
            SetSlotAlertVisible(showSlotAlert);

            if (_session == null)
            {
                SetText(stateText, _message);
                SetText(mentalValueText, "-");
                SetText(runwayValueText, "-");
                if (energySlider != null)
                    energySlider.SetValueWithoutNotify(0f);
                SetActionAvailability(false, false);
                RefreshSlotBar(0);
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
            RefreshSlotBar(_session.SlotsLeft);
        }

        void SetSlotAlertVisible(bool visible)
        {
            if (slotAlertPanel == null || slotAlertPanel.activeSelf == visible)
                return;

            slotAlertPanel.SetActive(visible);
            if (visible)
                slotAlertPanel.transform.SetAsLastSibling();
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
        void OnResourceChanged(ResourceKind kind, float oldValue, float newValue)
        {
            ShowFloatingResourceChange(kind, newValue - oldValue);
            Refresh();
        }
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

        void EnsureFeedbackUi()
        {
            if (_feedbackCanvas != null)
                return;

            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate.isRootCanvas && candidate.gameObject.scene.IsValid())
                {
                    _feedbackCanvas = candidate;
                    break;
                }
            }

            if (_feedbackCanvas == null)
            {
                Debug.LogError("Feedback UI membutuhkan root Canvas.", this);
                return;
            }

            _feedbackCanvasRect = _feedbackCanvas.transform as RectTransform;
            GameObject desktopObject = GameObject.Find("Desktop");
            _desktopPanel = desktopObject != null
                ? desktopObject.transform
                : _feedbackCanvas.transform;
            CreateSlotBar();
            CreateToast();
            CreateTransitionOverlay();
        }

        void CreateSlotBar()
        {
            _slotBarRoot = new GameObject("Week Slot Bar", typeof(RectTransform), typeof(Image));
            _slotBarRoot.transform.SetParent(_desktopPanel, false);
            _slotBarRoot.transform.SetAsLastSibling();

            RectTransform rootRect = _slotBarRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -12f);
            rootRect.sizeDelta = new Vector2(390f, 48f);
            _slotBarRoot.GetComponent<Image>().color = new Color(0.07f, 0.15f, 0.2f, 0.9f);

            GameObject labelObject = new GameObject("Week Slot Label", typeof(RectTransform));
            labelObject.transform.SetParent(_slotBarRoot.transform, false);
            TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
            CopyTextStyle(label, mentalValueText);
            label.text = "WEEK SLOTS";
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(-16f, 18f);

            GameObject segmentRoot = new GameObject("Slot Segments", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            segmentRoot.transform.SetParent(_slotBarRoot.transform, false);
            RectTransform segmentRect = segmentRoot.GetComponent<RectTransform>();
            segmentRect.anchorMin = new Vector2(0f, 0f);
            segmentRect.anchorMax = new Vector2(1f, 0f);
            segmentRect.pivot = new Vector2(0.5f, 0f);
            segmentRect.anchoredPosition = new Vector2(0f, 7f);
            segmentRect.sizeDelta = new Vector2(-16f, 18f);

            HorizontalLayoutGroup layout = segmentRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            int slotCount = config != null ? Mathf.Max(1, config.SlotsPerWeek) : 3;
            _slotSegments = new Image[slotCount];
            for (int i = 0; i < _slotSegments.Length; i++)
            {
                GameObject segment = new GameObject("Slot " + (i + 1), typeof(RectTransform), typeof(Image));
                segment.transform.SetParent(segmentRoot.transform, false);
                _slotSegments[i] = segment.GetComponent<Image>();
                _slotSegments[i].color = new Color(0.15f, 0.25f, 0.3f, 1f);
            }
        }

        void CreateToast()
        {
            _toastRoot = new GameObject("Activity Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _toastRoot.transform.SetParent(_feedbackCanvas.transform, false);
            RectTransform rect = _toastRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 70f);
            rect.sizeDelta = new Vector2(560f, 64f);
            _toastRoot.GetComponent<Image>().color = new Color(0.06f, 0.14f, 0.2f, 0.96f);
            _toastCanvasGroup = _toastRoot.GetComponent<CanvasGroup>();
            _toastCanvasGroup.alpha = 0f;
            _toastRoot.SetActive(false);

            GameObject textObject = new GameObject("Toast Text", typeof(RectTransform));
            textObject.transform.SetParent(_toastRoot.transform, false);
            _toastText = textObject.AddComponent<TextMeshProUGUI>();
            CopyTextStyle(_toastText, stateText);
            _toastText.fontSize = 20f;
            _toastText.alignment = TextAlignmentOptions.Center;
            _toastText.color = Color.white;
            _toastText.raycastTarget = false;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);
        }

        void CreateTransitionOverlay()
        {
            GameObject overlay = new GameObject("Week Transition", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(_feedbackCanvas.transform, false);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = Color.black;
            _transitionOverlay = overlay.GetComponent<Image>();
            _transitionCanvasGroup = overlay.GetComponent<CanvasGroup>();
            _transitionCanvasGroup.alpha = 0f;
            overlay.SetActive(false);
        }

        void RefreshSlotBar(int slotsLeft)
        {
            if (_slotSegments == null)
                return;

            for (int i = 0; i < _slotSegments.Length; i++)
                _slotSegments[i].color = i < slotsLeft
                    ? new Color(0f, 0.95f, 0.25f, 1f)
                    : new Color(0.15f, 0.25f, 0.3f, 1f);
        }

        void ShowFloatingResourceChange(ResourceKind kind, float delta)
        {
            if (Mathf.Abs(delta) < 0.01f)
                return;

            EnsureFeedbackUi();
            RectTransform target = GetResourceRect(kind);
            if (target == null || _feedbackCanvasRect == null)
                return;

            GameObject valueObject = new GameObject("Resource Change", typeof(RectTransform), typeof(CanvasGroup));
            valueObject.transform.SetParent(_feedbackCanvas.transform, false);
            TMP_Text valueText = valueObject.AddComponent<TextMeshProUGUI>();
            CopyTextStyle(valueText, mentalValueText);
            valueText.text = (delta > 0f ? "+" : string.Empty) + FormatResourceValue(delta);
            valueText.fontSize = 24f;
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = delta < 0f
                ? new Color(1f, 0.12f, 0.1f, 1f)
                : new Color(0.1f, 1f, 0.25f, 1f);
            valueText.raycastTarget = false;

            RectTransform valueRect = valueObject.GetComponent<RectTransform>();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, target.position);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _feedbackCanvasRect,
                screenPoint,
                null,
                out localPoint);
            valueRect.anchoredPosition = localPoint + new Vector2(55f, 20f);
            valueRect.sizeDelta = new Vector2(100f, 36f);
            StartCoroutine(FadeFloatingValue(valueObject, valueRect));
        }

        IEnumerator FadeFloatingValue(GameObject valueObject, RectTransform valueRect)
        {
            CanvasGroup group = valueObject.GetComponent<CanvasGroup>();
            Vector2 start = valueRect.anchoredPosition;
            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                valueRect.anchoredPosition = start + new Vector2(0f, progress * 35f);
                group.alpha = 1f - progress;
                yield return null;
            }

            Destroy(valueObject);
        }

        void PlayEndWeekTransition()
        {
            EnsureFeedbackUi();
            if (_transitionCanvasGroup == null)
                return;

            if (_transitionRoutine != null)
                StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(EndWeekTransitionRoutine());
        }

        IEnumerator EndWeekTransitionRoutine()
        {
            _transitionOverlay.gameObject.SetActive(true);
            _transitionOverlay.transform.SetAsLastSibling();
            yield return FadeCanvasGroup(_transitionCanvasGroup, 0f, 1f, 0.35f);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return FadeCanvasGroup(_transitionCanvasGroup, 1f, 0f, 0.35f);
            _transitionOverlay.gameObject.SetActive(false);
            _transitionRoutine = null;
        }

        IEnumerator ShowToastRoutine()
        {
            _toastRoot.SetActive(true);
            _toastRoot.transform.SetAsLastSibling();
            yield return FadeCanvasGroup(_toastCanvasGroup, 0f, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(2.4f);
            yield return FadeCanvasGroup(_toastCanvasGroup, 1f, 0f, 0.25f);
            _toastRoot.SetActive(false);
            _toastRoutine = null;
        }

        static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        RectTransform GetResourceRect(ResourceKind kind)
        {
            if (kind == ResourceKind.Mental)
                return mentalValueText != null ? mentalValueText.rectTransform : null;
            if (kind == ResourceKind.Energy)
                return energySlider != null ? energySlider.GetComponent<RectTransform>() : null;
            return runwayValueText != null ? runwayValueText.rectTransform : null;
        }

        static string FormatResourceValue(float value)
        {
            return Mathf.Abs(value % 1f) < 0.01f
                ? Mathf.RoundToInt(Mathf.Abs(value)).ToString()
                : Mathf.Abs(value).ToString("0.#");
        }

        static void CopyTextStyle(TMP_Text target, TMP_Text source)
        {
            if (target == null || source == null)
                return;

            target.font = source.font;
            target.fontStyle = source.fontStyle;
            target.characterSpacing = source.characterSpacing;
            target.enableAutoSizing = false;
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
