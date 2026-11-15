using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using JobGame;

namespace JobGame.UI
{
    public sealed class SkillOptionsPresenter : MonoBehaviour
    {
        const int SkillOptionCount = 3;

        [SerializeField] SessionHudPresenter sessionPresenter;
        [SerializeField] GameObject[] skillOptionCards = new GameObject[SkillOptionCount];

        sealed class SkillOptionCard
        {
            public GameObject Root;
            public TMP_Text NameText;
            public TMP_Text DescriptionText;
            public Button LearnButton;
            public UnityAction LearnAction;
        }

        IGameSession _session;
        SkillOptionCard[] _cards;
        readonly SkillOption[] _visibleOptions = new SkillOption[SkillOptionCount];

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
                Debug.LogError("SkillOptionsPresenter membutuhkan SessionHudPresenter.", this);
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
            if (skillOptionCards == null || skillOptionCards.Length != SkillOptionCount)
            {
                Debug.LogError(
                    "SkillOptionsPresenter membutuhkan tepat 3 kotak skill pada Inspector.",
                    this);
                _cards = new SkillOptionCard[0];
                return;
            }

            _cards = new SkillOptionCard[skillOptionCards.Length];
            for (int i = 0; i < skillOptionCards.Length; i++)
            {
                GameObject root = skillOptionCards[i];
                if (root == null)
                {
                    Debug.LogError("Kotak skill ke-" + (i + 1) + " belum di-assign.", this);
                    continue;
                }

                SkillOptionCard card = new SkillOptionCard
                {
                    Root = root,
                    NameText = FindText(root, "Name of The Skill"),
                    DescriptionText = FindText(root, "Description Text"),
                    LearnButton = root.GetComponentInChildren<Button>(true)
                };
                int slot = i;
                card.LearnAction = delegate { OnLearnClicked(slot); };

                if (card.NameText == null)
                    Debug.LogError("Kotak skill " + root.name + " tidak memiliki Name of The Skill.", root);
                if (card.DescriptionText == null)
                    Debug.LogError("Kotak skill " + root.name + " tidak memiliki Description Text.", root);
                if (card.LearnButton == null)
                    Debug.LogError("Kotak skill " + root.name + " tidak memiliki tombol Learn Button.", root);

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

            IReadOnlyList<SkillOption> options = _session.GetSkillOptions();
            if (options == null)
            {
                Debug.LogError("IGameSession.GetSkillOptions() tidak boleh null.", this);
                ClearCards();
                return;
            }

            bool canLearn = _session.Phase == GamePhase.Planning && _session.SlotsLeft > 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                SkillOptionCard card = _cards[i];
                if (card == null || card.Root == null)
                    continue;

                SkillOption option = i < options.Count ? options[i] : null;
                _visibleOptions[i] = option;
                card.Root.SetActive(option != null);

                if (option == null)
                    continue;

                if (card.NameText != null)
                    card.NameText.text = option.Tag.DisplayName();
                if (card.DescriptionText != null)
                {
                    string hint = string.IsNullOrEmpty(option.HintText)
                        ? string.Empty
                        : "\n" + option.HintText;
                    card.DescriptionText.text = "Lv. " + option.CurrentLevel + " -> " +
                        (option.CurrentLevel + 1) + hint;
                }
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

            SkillOption option = _visibleOptions[slot];
            if (option == null)
                return;

            ActionResult result = _session.TryUpgradeSkill(option.Tag);
            if (!result.Ok)
                Debug.LogWarning(
                    "Gagal mempelajari " + option.Tag.DisplayName() + ": " +
                    result.Error + " - " + result.Message,
                    this);

            Refresh();
        }

        void OnWeekStarted(int week) { Refresh(); }
        void OnSlotsChanged(int slots) { Refresh(); }
        void OnPhaseChanged(GamePhase phase) { Refresh(); }
    }
}
