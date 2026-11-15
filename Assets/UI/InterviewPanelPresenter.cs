using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JobGame.UI
{
    public sealed class InterviewPanelPresenter : MonoBehaviour
    {
        [SerializeField] SessionHudPresenter sessionPresenter;
        [SerializeField] GameObject sessionHudPanel;
        [SerializeField] TMP_Text interviewDialogueText;
        [SerializeField] TMP_Text[] choiceTexts;

        sealed class ChoiceSlot
        {
            public GameObject Root;
            public TMP_Text Label;
            public Button Button;
            public UnityAction Handler;
        }

        readonly List<ChoiceSlot> _choiceSlots = new List<ChoiceSlot>();
        readonly List<GameObject> _extraChoiceRoots = new List<GameObject>();
        IGameSession _session;
        IInterviewSession _interview;
        bool _sessionHudHidden;
        bool _sessionHudWasActive;

        void OnEnable()
        {
            if (!ValidateSetup())
                return;

            if (sessionHudPanel == gameObject || transform.IsChildOf(sessionHudPanel.transform))
            {
                Debug.LogError(
                    "InterviewPanelPresenter harus berada di luar Session HUD agar tetap aktif saat HUD disembunyikan.",
                    this);
                return;
            }

            if (!BuildBaseChoiceSlots())
                return;

            TMP_InputField dialogueInput = interviewDialogueText.GetComponentInParent<TMP_InputField>();
            if (dialogueInput != null)
                dialogueInput.interactable = false;

            sessionPresenter.SessionBound += Bind;
            Bind(sessionPresenter.Session);
        }

        void OnDisable()
        {
            if (sessionPresenter != null)
                sessionPresenter.SessionBound -= Bind;
            Unbind(_session);
            RestoreSessionHud();
            ClearChoiceBindings();
            RemoveExtraChoiceRoots();
            _session = null;
            _interview = null;
        }

        public void Bind(IGameSession session)
        {
            RestoreSessionHud();
            Unbind(_session);
            _session = session;
            _interview = null;
            ClearChoiceBindings();

            if (_session == null)
            {
                interviewDialogueText.text = string.Empty;
                return;
            }

            _session.InterviewStarted += OnInterviewStarted;
            _session.PhaseChanged += OnPhaseChanged;

            if (_session.Phase == GamePhase.AwaitingInterview &&
                _session.ActiveInterview != null)
                OpenInterview(_session.ActiveInterview);
            else
                interviewDialogueText.text = string.Empty;
        }

        bool ValidateSetup()
        {
            if (sessionPresenter != null && sessionHudPanel != null &&
                interviewDialogueText != null &&
                choiceTexts != null && choiceTexts.Length == 3 &&
                choiceTexts[0] != null && choiceTexts[1] != null && choiceTexts[2] != null)
                return true;

            Debug.LogError(
                "InterviewPanelPresenter: assign SessionHudPresenter, Session HUD, " +
                "Interview Dialogue TMP, dan tiga TMP pada Choice 1-3 di Inspector.",
                this);
            return false;
        }

        bool BuildBaseChoiceSlots()
        {
            if (_choiceSlots.Count > 0)
                return true;

            for (int i = 0; i < choiceTexts.Length; i++)
            {
                TMP_Text label = choiceTexts[i];
                Transform rootTransform = label.transform.parent;
                if (rootTransform == null)
                {
                    Debug.LogError("Text pilihan harus menjadi child langsung dari kotak pilihannya.", label);
                    return false;
                }

                ChoiceSlot slot = CreateChoiceSlot(rootTransform.gameObject, label, false);
                if (slot == null)
                    return false;

                _choiceSlots.Add(slot);
                slot.Root.SetActive(false);
            }

            return true;
        }

        ChoiceSlot CreateChoiceSlot(GameObject root, TMP_Text label, bool isClone)
        {
            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.AddComponent<Button>();

            if (button.targetGraphic == null)
            {
                Image image = root.GetComponent<Image>();
                if (image == null)
                {
                    Debug.LogError("Kotak pilihan membutuhkan Image agar dapat diklik.", root);
                    return null;
                }
                button.targetGraphic = image;
            }

            if (isClone)
                button.onClick.RemoveAllListeners();

            button.interactable = false;
            return new ChoiceSlot
            {
                Root = root,
                Label = label,
                Button = button
            };
        }

        void Unbind(IGameSession session)
        {
            if (session == null)
                return;

            session.InterviewStarted -= OnInterviewStarted;
            session.PhaseChanged -= OnPhaseChanged;
        }

        void OnInterviewStarted(IInterviewSession interview)
        {
            OpenInterview(interview);
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.AwaitingInterview && _session != null &&
                _session.ActiveInterview == null && _interview != null)
            {
                _interview = null;
                interviewDialogueText.text = string.Empty;
                ClearChoiceBindings();
                RestoreSessionHud();
            }
        }

        void OpenInterview(IInterviewSession interview)
        {
            if (interview == null)
            {
                Debug.LogError("InterviewStarted mengirim sesi interview kosong.", this);
                return;
            }

            _interview = interview;
            HideSessionHud();
            DisplayQuestion(null);
        }

        void HideSessionHud()
        {
            if (sessionHudPanel == null)
            {
                Debug.LogError("Session HUD belum di-assign pada InterviewPanelPresenter.", this);
                return;
            }

            if (!_sessionHudHidden)
            {
                _sessionHudWasActive = sessionHudPanel.activeSelf;
                _sessionHudHidden = true;
            }

            sessionHudPanel.SetActive(false);
        }

        void RestoreSessionHud()
        {
            if (!_sessionHudHidden)
                return;

            if (sessionHudPanel == null)
                Debug.LogError("Session HUD tidak tersedia untuk ditampilkan kembali.", this);
            else
                sessionHudPanel.SetActive(_sessionHudWasActive);

            _sessionHudHidden = false;
        }

        void DisplayQuestion(string feedback)
        {
            if (_interview == null || _interview.IsFinished)
            {
                DisplayFinished(feedback);
                return;
            }

            if (_interview.Questions == null ||
                _interview.CurrentIndex < 0 ||
                _interview.CurrentIndex >= _interview.Questions.Count)
            {
                Debug.LogError("Indeks pertanyaan interview berada di luar daftar Questions.", this);
                ClearChoiceBindings();
                return;
            }

            InterviewQuestion question = _interview.Questions[_interview.CurrentIndex];
            string prompt = string.Format(
                "Pertanyaan {0}/{1}\n{2}",
                _interview.CurrentIndex + 1,
                _interview.Questions.Count,
                question.Prompt);
            interviewDialogueText.text = string.IsNullOrEmpty(feedback)
                ? prompt
                : feedback + "\n\n" + prompt;

            DisplayAnswerCards();
        }

        void DisplayAnswerCards()
        {
            ClearChoiceBindings();
            IReadOnlyList<CardRef> cards = _interview.AvailableCards;
            if (!EnsureChoiceSlots(cards.Count))
                return;

            for (int i = 0; i < _choiceSlots.Count; i++)
            {
                ChoiceSlot slot = _choiceSlots[i];
                if (i >= cards.Count)
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                CardRef card = cards[i];
                string label;
                if (!TryGetCardLabel(card, out label))
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                slot.Label.text = (i + 1) + ". " + label;
                slot.Root.SetActive(true);
                slot.Button.interactable = true;
                slot.Handler = delegate { OnAnswerSelected(card); };
                slot.Button.onClick.AddListener(slot.Handler);
            }
        }

        bool EnsureChoiceSlots(int requiredCount)
        {
            while (_choiceSlots.Count < requiredCount)
            {
                int slotIndex = _choiceSlots.Count;
                ChoiceSlot template = _choiceSlots[0];
                GameObject cloneRoot = Instantiate(
                    template.Root,
                    template.Root.transform.parent,
                    false);
                cloneRoot.name = "Interview Choice " + (slotIndex + 1);

                TMP_Text cloneLabel = cloneRoot.GetComponentInChildren<TMP_Text>(true);
                if (cloneLabel == null)
                {
                    Debug.LogError("Kotak pilihan duplikat tidak memiliki TMP_Text.", cloneRoot);
                    DestroyChoiceRoot(cloneRoot);
                    return false;
                }

                ChoiceSlot clone = CreateChoiceSlot(cloneRoot, cloneLabel, true);
                if (clone == null)
                {
                    DestroyChoiceRoot(cloneRoot);
                    return false;
                }

                RectTransform templateRect = (RectTransform)template.Root.transform;
                RectTransform cloneRect = (RectTransform)cloneRoot.transform;
                cloneRect.anchorMin = templateRect.anchorMin;
                cloneRect.anchorMax = templateRect.anchorMax;
                cloneRect.pivot = templateRect.pivot;
                cloneRect.sizeDelta = templateRect.sizeDelta;
                cloneRect.anchoredPosition = new Vector2(
                    templateRect.anchoredPosition.x,
                    templateRect.anchoredPosition.y +
                        slotIndex * (templateRect.sizeDelta.y + 10f));
                cloneRoot.SetActive(false);
                _extraChoiceRoots.Add(cloneRoot);
                _choiceSlots.Add(clone);
            }

            return true;
        }

        bool TryGetCardLabel(CardRef card, out string label)
        {
            if (card.Kind == CardKind.Skill)
            {
                label = "Skill: " + card.Skill.DisplayName();
                return true;
            }

            if (card.Kind == CardKind.Experience && _session != null)
            {
                foreach (ExperienceCard experience in _session.OwnedExperiences)
                {
                    if (experience != null && experience.Id == card.ExperienceId)
                    {
                        label = "Experience: " + experience.DisplayName;
                        return true;
                    }
                }
            }

            Debug.LogError("Kartu interview tidak ditemukan pada data sesi: " + card.Kind + ".", this);
            label = string.Empty;
            return false;
        }

        void OnAnswerSelected(CardRef card)
        {
            if (_session == null || _interview == null ||
                _session.Phase != GamePhase.AwaitingInterview ||
                !object.ReferenceEquals(_session.ActiveInterview, _interview))
            {
                Debug.LogWarning("Interview tidak lagi aktif untuk menerima jawaban.", this);
                return;
            }

            AnswerFeedback feedback = _interview.Answer(card);
            if (feedback == null)
            {
                Debug.LogWarning("Kartu jawaban tidak diterima oleh IInterviewSession.", this);
                DisplayQuestion(null);
                return;
            }

            if (_interview.IsFinished)
                DisplayFinished(feedback.Text);
            else
                DisplayQuestion(feedback.Text);
        }

        void DisplayFinished(string feedback)
        {
            ClearChoiceBindings();
            if (_interview == null || _interview.Result == null)
            {
                Debug.LogError("Interview selesai tanpa InterviewResult.", this);
                return;
            }

            interviewDialogueText.text =
                (string.IsNullOrEmpty(feedback) ? string.Empty : feedback + "\n\n") +
                _interview.Result.Summary;

            ChoiceSlot finishSlot = _choiceSlots[0];
            finishSlot.Root.SetActive(true);
            finishSlot.Label.text = "Selesaikan Interview";
            finishSlot.Button.interactable = true;
            finishSlot.Handler = OnFinishClicked;
            finishSlot.Button.onClick.AddListener(finishSlot.Handler);
        }

        void OnFinishClicked()
        {
            if (_session == null || _interview == null ||
                !_interview.IsFinished ||
                !object.ReferenceEquals(_session.ActiveInterview, _interview))
            {
                Debug.LogWarning("Interview belum siap ditutup.", this);
                return;
            }

            ActionResult result = _session.CloseInterview();
            if (!result.Ok)
            {
                Debug.LogWarning(
                    "Menutup interview gagal: " + result.Error + " - " + result.Message,
                    this);
                DisplayFinished(null);
            }
        }

        void ClearChoiceBindings()
        {
            foreach (ChoiceSlot slot in _choiceSlots)
            {
                if (slot.Handler != null)
                {
                    slot.Button.onClick.RemoveListener(slot.Handler);
                    slot.Handler = null;
                }

                slot.Button.interactable = false;
                slot.Root.SetActive(false);
            }
        }

        void RemoveExtraChoiceRoots()
        {
            foreach (GameObject root in _extraChoiceRoots)
                if (root != null)
                    DestroyChoiceRoot(root);

            _extraChoiceRoots.Clear();
            _choiceSlots.Clear();
        }

        static void DestroyChoiceRoot(GameObject root)
        {
            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }
    }
}
