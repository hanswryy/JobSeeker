using UnityEngine;
using JobGame.Logic;

namespace JobGame.Examples
{
    // Contoh pola controller UI: bind sesi, subscribe event, lalu Refresh dari state.
    public sealed class ExampleSessionPresenter : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [SerializeField] GameConfig config;
        [SerializeField] int seed = 42;

        IGameSession _session;

        void Start()
        {
            BindNewRun(seed);
        }

        void OnDestroy()
        {
            Unsubscribe(_session);
        }

        public void BindNewRun(int newSeed)
        {
            Unsubscribe(_session);
            _session = new GameFactory(content, config).NewRun(newSeed);
            Subscribe(_session);
            Refresh();
        }

        public void OnRestClicked()
        {
            Execute(_session.TryRest());
        }

        public void OnEndWeekClicked()
        {
            Execute(_session.EndWeek());
        }

        public void OnSkillClicked(SkillTag tag)
        {
            Execute(_session.TryUpgradeSkill(tag));
        }

        public void OnExperienceClicked(string experienceId)
        {
            Execute(_session.TryUpgradeExperience(experienceId));
        }

        void Execute(ActionResult result)
        {
            if (!result.Ok)
                Debug.LogWarning(result.Error + ": " + result.Message, this);
            // Event akan memanggil Refresh. Ini hanya contoh fallback untuk command tanpa event.
            Refresh();
        }

        void Refresh()
        {
            if (_session == null)
                return;

            Debug.Log(string.Format(
                "Week {0}/{1}, Phase {2}, Slots {3}, Energy {4:0.##}, Mental {5:0.##}, Runway {6:0.##}",
                _session.Week,
                _session.TotalWeeks,
                _session.Phase,
                _session.SlotsLeft,
                _session.Energy,
                _session.Mental,
                _session.Runway), this);

            // UI nyata mengisi label, progress bar, tombol, dan list dari properti ini.
            // Contoh: _session.GetSkillOptions(), _session.Postings, _session.Applications.
        }

        void Subscribe(IGameSession session)
        {
            if (session == null)
                return;
            session.WeekStarted += OnStateChanged;
            session.SlotsChanged += OnSlotsChanged;
            session.ResourceChanged += OnResourceChanged;
            session.HoldingsChanged += OnStateChanged;
            session.PostingsChanged += OnStateChanged;
            session.PhaseChanged += OnPhaseChanged;
            session.RunEnded += OnRunEnded;
        }

        void Unsubscribe(IGameSession session)
        {
            if (session == null)
                return;
            session.WeekStarted -= OnStateChanged;
            session.SlotsChanged -= OnSlotsChanged;
            session.ResourceChanged -= OnResourceChanged;
            session.HoldingsChanged -= OnStateChanged;
            session.PostingsChanged -= OnStateChanged;
            session.PhaseChanged -= OnPhaseChanged;
            session.RunEnded -= OnRunEnded;
        }

        void OnStateChanged(int unused) { Refresh(); }
        void OnStateChanged() { Refresh(); }
        void OnSlotsChanged(int unused) { Refresh(); }
        void OnResourceChanged(ResourceKind kind, float oldValue, float newValue) { Refresh(); }
        void OnPhaseChanged(GamePhase phase) { Refresh(); }
        void OnRunEnded(RunSummary summary) { Refresh(); }
    }
}
