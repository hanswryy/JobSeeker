using System;
using System.Collections.Generic;
using UnityEngine;
using JobGame.Logic;

namespace JobGame.Debugging
{
    [AddComponentMenu("JobGame/Debug Run Controller")]
    public sealed class DebugRunController : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [SerializeField] GameConfig config;
        [SerializeField] int seed = 42;
        [SerializeField] bool preventRunwayLoss = true;
        [SerializeField] bool showPostings = true;

        IGameSession _session;
        GameConfig _runtimeConfig;
        Vector2 _scrollPosition;
        string _lastMessage = "Belum ada aksi.";
        readonly List<string> _eventLog = new List<string>();

        void Start()
        {
            StartRun();
        }

        void OnDestroy()
        {
            Unsubscribe(_session);
            if (_runtimeConfig != null)
                Destroy(_runtimeConfig);
        }

        void OnGUI()
        {
            // test
            if (_session == null)
            {
                GUILayout.BeginArea(new Rect(20, 20, 500, 140), GUI.skin.box);
                GUILayout.Label("Debug Run belum siap.");
                GUILayout.Label("Assign GameContent dan GameConfig pada Inspector.");
                GUILayout.EndArea();
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.MinWidth(720));
            GUILayout.Label("JobSeeker - Debug Run", GUI.skin.GetStyle("boldLabel"));
            GUILayout.Label("Hanya untuk menguji lifecycle A1-A3. Interview dan TryApply belum aktif.");
            GUILayout.Space(8);
            DrawState();
            DrawActions();
            DrawContent();
            DrawLog();
            GUILayout.EndScrollView();
        }

        void DrawState()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                string.Format("Minggu {0}/{1} | Fase: {2} | Slot: {3}",
                    _session.Week, _session.TotalWeeks, _session.Phase, _session.SlotsLeft));
            GUILayout.Label(
                string.Format("Energy: {0:0.##} | Mental: {1:0.##} | Runway: {2:0.##}",
                    _session.Energy, _session.Mental, _session.Runway));
            GUILayout.Label(
                string.Format("Skill dimiliki: {0} | Experience: {1}",
                    _session.OwnedSkills.Count, _session.OwnedExperiences.Count));
            if (_session.Summary != null)
            {
                GUILayout.Label("Run selesai: " + _session.Summary.Outcome);
                GUILayout.Label(_session.Summary.Headline);
            }
            GUILayout.EndVertical();
        }

        void DrawActions()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Aksi Planning");
            bool planning = _session.Phase == GamePhase.Planning;
            bool canAct = planning && _session.SlotsLeft > 0;
            GUI.enabled = canAct;

            GUILayout.BeginHorizontal();
            foreach (SkillOption option in _session.GetSkillOptions())
            {
                if (GUILayout.Button(
                    string.Format("Upgrade {0} (Lv {1})", option.Tag.DisplayName(), option.CurrentLevel),
                    GUILayout.MinHeight(36)))
                    Execute("Upgrade skill " + option.Tag, () => _session.TryUpgradeSkill(option.Tag));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (ExperienceOption option in _session.GetExperienceOptions())
            {
                if (GUILayout.Button(
                    "Ambil " + option.Card.DisplayName,
                    GUILayout.MinHeight(36)))
                    Execute("Upgrade experience " + option.Card.Id,
                        () => _session.TryUpgradeExperience(option.Card.Id));
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Istirahat", GUILayout.MinHeight(36)))
                Execute("Istirahat", () => _session.TryRest());
            GUI.enabled = planning;
            if (GUILayout.Button("End Week", GUILayout.MinHeight(36)))
                Execute("EndWeek", () => _session.EndWeek());
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed:");
            string seedText = GUILayout.TextField(seed.ToString(), GUILayout.Width(100));
            int parsedSeed;
            if (int.TryParse(seedText, out parsedSeed))
                seed = parsedSeed;
            if (GUILayout.Button("Reset Run"))
                StartRun();
            GUILayout.EndHorizontal();
            GUILayout.Label("Status: " + _lastMessage);
            GUILayout.EndVertical();
        }

        void DrawContent()
        {
            if (!showPostings)
                return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Posting Minggu Ini (" + _session.Postings.Count + ")");
            foreach (JobPosting posting in _session.Postings)
            {
                if (posting.Template == null)
                {
                    GUILayout.Label(posting.Id + " (template null)");
                    continue;
                }

                GUILayout.Label(string.Format(
                    "{0} | {1} di {2} | Applied: {3}",
                    posting.Id,
                    posting.Template.Title,
                    posting.Template.Company,
                    posting.Applied));
            }
            GUILayout.EndVertical();
        }

        void DrawLog()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Event terakhir");
            int start = Math.Max(0, _eventLog.Count - 8);
            for (int i = start; i < _eventLog.Count; i++)
                GUILayout.Label(_eventLog[i]);
            GUILayout.EndVertical();
        }

        void StartRun()
        {
            if (content == null || config == null)
            {
                _session = null;
                _lastMessage = "GameContent dan GameConfig wajib di-assign.";
                return;
            }

            Unsubscribe(_session);
            if (_runtimeConfig != null)
                Destroy(_runtimeConfig);

            _runtimeConfig = Instantiate(config);
            _runtimeConfig.name = config.name + " (Debug Runtime)";
            if (preventRunwayLoss)
                _runtimeConfig.WeeklyRunwayBurn = 0f;

            IGameFactory factory = new GameFactory(content, _runtimeConfig);
            _session = factory.NewRun(seed);
            _eventLog.Clear();
            _lastMessage = "Run baru dibuat.";
            Subscribe(_session);
        }

        void Execute(string label, Func<ActionResult> command)
        {
            ActionResult result = command();
            _lastMessage = result.Ok
                ? label + " berhasil."
                : label + " gagal: " + result.Error + " - " + result.Message;
            if (!result.Ok)
                _eventLog.Add(_lastMessage);
        }

        void Subscribe(IGameSession session)
        {
            if (session == null)
                return;
            session.WeekStarted += OnWeekStarted;
            session.SlotsChanged += OnSlotsChanged;
            session.ResourceChanged += OnResourceChanged;
            session.PhaseChanged += OnPhaseChanged;
            session.RunEnded += OnRunEnded;
        }

        void Unsubscribe(IGameSession session)
        {
            if (session == null)
                return;
            session.WeekStarted -= OnWeekStarted;
            session.SlotsChanged -= OnSlotsChanged;
            session.ResourceChanged -= OnResourceChanged;
            session.PhaseChanged -= OnPhaseChanged;
            session.RunEnded -= OnRunEnded;
        }

        void OnWeekStarted(int week) { _eventLog.Add("WeekStarted(" + week + ")"); }
        void OnSlotsChanged(int slots) { _eventLog.Add("SlotsChanged(" + slots + ")"); }
        void OnResourceChanged(ResourceKind kind, float oldValue, float newValue)
        {
            _eventLog.Add(string.Format("ResourceChanged({0}: {1:0.##}->{2:0.##})",
                kind, oldValue, newValue));
        }
        void OnPhaseChanged(GamePhase phase) { _eventLog.Add("PhaseChanged(" + phase + ")"); }
        void OnRunEnded(RunSummary summary) { _eventLog.Add("RunEnded(" + summary.Outcome + ")"); }
    }
}
