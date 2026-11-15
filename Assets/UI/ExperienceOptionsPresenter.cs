using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using JobGame;

namespace JobGame.UI
{
    public sealed class ExperienceOptionsPresenter : MonoBehaviour
    {
        const int ExperienceOptionCount = 3;

        [SerializeField] SessionHudPresenter sessionPresenter;
        [SerializeField] GameObject[] experienceOptionCards = new GameObject[ExperienceOptionCount];

        sealed class ExperienceOptionCard
        {
            public GameObject Root;
            public TMP_Text NameText;
            public Button LearnButton;
            public UnityAction LearnAction;
        }

        IGameSession _session;
        ExperienceOptionCard[] _cards;
        readonly ExperienceOption[] _visibleOptions = new ExperienceOption[ExperienceOptionCount];

        void Awake()
        {
            ResolveCards();
        }

        void OnEnable()
        {
            if (_cards == null)
                ResolveCards();

            AddButtonListeners();

            if (sessionPresenter == null)
            {
                Debug.LogError("ExperienceOptionsPresenter membutuhkan SessionHudPresenter.", this);
                Refresh();
                return;
            }

            sessionPresenter.SessionBound += Bind;
            Bind(sessionPresenter.Session);
        }

        void OnDisable()
        {
            RemoveButtonListeners();

            if (sessionPresenter != null)
                sessionPresenter.SessionBound -= Bind;

            Bind(null);
        }

        void ResolveCards()
        {
            if (experienceOptionCards == null ||
                experienceOptionCards.Length != ExperienceOptionCount)
            {
                Debug.LogError(
                    "ExperienceOptionsPresenter membutuhkan tepat 3 kotak EXP pada Inspector.",
                    this);
                _cards = new ExperienceOptionCard[0];
                return;
            }

            _cards = new ExperienceOptionCard[experienceOptionCards.Length];
            for (int i = 0; i < experienceOptionCards.Length; i++)
            {
                GameObject root = experienceOptionCards[i];
                if (root == null)
                {
                    Debug.LogError("Kotak EXP ke-" + (i + 1) + " belum di-assign.", this);
                    continue;
                }

                ExperienceOptionCard card = new ExperienceOptionCard
                {
                    Root = root,
                    NameText = FindText(root, "EXP Name"),
                    LearnButton = root.GetComponentInChildren<Button>(true)
                };
                int slot = i;
                card.LearnAction = delegate { OnLearnClicked(slot); };

                if (card.NameText == null)
                    Debug.LogError("Kotak EXP " + root.name + " tidak memiliki EXP Name.", root);
                if (card.LearnButton == null)
                    Debug.LogError("Kotak EXP " + root.name + " tidak memiliki tombol Learn Button.", root);

                _cards[i] = card;
            }
        }

        static TMP_Text FindText(GameObject root, string containerName)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name != containerName)
                    continue;

                TMP_Text text = descendants[i].GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    return text;
            }

            return null;
        }

        void AddButtonListeners()
        {
            if (_cards == null)
                return;

            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i] != null && _cards[i].LearnButton != null)
                    _cards[i].LearnButton.onClick.AddListener(_cards[i].LearnAction);
        }

        void RemoveButtonListeners()
        {
            if (_cards == null)
                return;

            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i] != null && _cards[i].LearnButton != null)
                    _cards[i].LearnButton.onClick.RemoveListener(_cards[i].LearnAction);
        }

        void Bind(IGameSession session)
        {
            Unsubscribe(_session);
            _session = session;
            Subscribe(_session);
            Refresh();
        }

        void Subscribe(IGameSession session)
        {
            if (session == null || !isActiveAndEnabled)
                return;

            session.WeekStarted += OnWeekStarted;
            session.HoldingsChanged += Refresh;
            session.SlotsChanged += OnSlotsChanged;
            session.PhaseChanged += OnPhaseChanged;
        }

        void Unsubscribe(IGameSession session)
        {
            if (session == null)
                return;

            session.WeekStarted -= OnWeekStarted;
            session.HoldingsChanged -= Refresh;
            session.SlotsChanged -= OnSlotsChanged;
            session.PhaseChanged -= OnPhaseChanged;
        }

        void Refresh()
        {
            if (_cards == null)
                return;

            if (_session == null)
            {
                ClearCards();
                return;
            }

            IReadOnlyList<ExperienceOption> options = _session.GetExperienceOptions();
            if (options == null)
            {
                Debug.LogError("IGameSession.GetExperienceOptions() tidak boleh null.", this);
                ClearCards();
                return;
            }

            bool canLearn = _session.Phase == GamePhase.Planning && _session.SlotsLeft > 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                ExperienceOptionCard card = _cards[i];
                if (card == null || card.Root == null)
                    continue;
                if (card.NameText == null)
                {
                    card.Root.SetActive(false);
                    _visibleOptions[i] = null;
                    continue;
                }

                ExperienceOption option = i < options.Count ? options[i] : null;
                bool hasValidName = option != null && option.Card != null &&
                    !string.IsNullOrWhiteSpace(option.Card.DisplayName);
                if (option != null && !hasValidName)
                    Debug.LogError("Experience option tidak memiliki kartu atau DisplayName.", this);

                _visibleOptions[i] = hasValidName ? option : null;
                card.Root.SetActive(hasValidName);
                if (!hasValidName)
                    continue;

                card.NameText.text = option.Card.DisplayName;
                if (card.LearnButton != null)
                    card.LearnButton.interactable = canLearn;
            }
        }

        void ClearCards()
        {
            for (int i = 0; i < _visibleOptions.Length; i++)
                _visibleOptions[i] = null;

            if (_cards == null)
                return;

            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i] != null && _cards[i].Root != null)
                    _cards[i].Root.SetActive(false);
        }

        void OnLearnClicked(int slot)
        {
            if (_session == null || slot < 0 || slot >= _visibleOptions.Length)
                return;

            ExperienceOption option = _visibleOptions[slot];
            if (option == null || option.Card == null)
                return;

            ActionResult result = _session.TryUpgradeExperience(option.Card.Id);
            if (!result.Ok)
                Debug.LogWarning(
                    "Gagal menambahkan experience " + option.Card.DisplayName + ": " +
                    result.Error + " - " + result.Message,
                    this);

            Refresh();
        }

        void OnWeekStarted(int week) { Refresh(); }
        void OnSlotsChanged(int slots) { Refresh(); }
        void OnPhaseChanged(GamePhase phase) { Refresh(); }
    }
}
