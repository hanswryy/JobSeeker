using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] bool showApplications = true;

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
            GUILayout.Label("Debug A1-A4: content, resource, posting, apply, screening, interview, dan offer.");
            GUILayout.Space(8);
            DrawState();
            DrawActions();
            DrawContent();
            DrawPipeline();
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
                GUILayout.Label(string.Format(
                    "Summary: {0} apply | {1} screening | {2} interview | Score {3}",
                    _session.Summary.ApplicationsSent,
                    _session.Summary.ScreeningsPassed,
                    _session.Summary.InterviewsPlayed,
                    _session.Summary.FinalScore));
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
                    string.Format("Upgrade {0} (Lv {1})\n{2}",
                    option.Tag.DisplayName(), option.CurrentLevel, option.HintText),
                    GUILayout.MinHeight(36)))
                    Execute("Upgrade skill " + option.Tag, () => _session.TryUpgradeSkill(option.Tag));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (ExperienceOption option in _session.GetExperienceOptions())
            {
                if (GUILayout.Button(
                    "Ambil " + option.Card.DisplayName + "\n" + option.HintText,
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
                DrawPostingActions(posting);
            }
            GUILayout.EndVertical();
        }

        void DrawPostingActions(JobPosting posting)
        {
            CvDraft cv = BuildCvFor(posting);
            MatchResult match = _session.PreviewMatch(posting, cv);
            GUILayout.Label(string.Format(
                "  CV debug: {0} skill, {1} experience | Score {2} | Gate {3}",
                cv.Skills.Count,
                cv.Experiences.Count,
                match.Score,
                match.IsRejected ? match.Reject.ToString() : "lolos"));
            if (match.MissingMustHave.Count > 0)
                GUILayout.Label("  Kurang: " + string.Join(", ",
                    match.MissingMustHave.Select(item => item.DisplayName()).ToArray()));

            bool canApply = _session.Phase == GamePhase.Planning &&
                _session.SlotsLeft > 0 && !posting.Applied;
            GUI.enabled = canApply;
            if (GUILayout.Button("Lamar dengan CV debug", GUILayout.MinHeight(30)))
                Execute("Lamar " + posting.Id, () => _session.TryApply(posting.Id, cv));
            GUI.enabled = true;
        }

        void DrawPipeline()
        {
            if (showApplications)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Daftar Lamaran");
                if (_session.Applications.Count == 0)
                    GUILayout.Label("Belum ada lamaran.");
                foreach (ApplicationRecord application in _session.Applications)
                    GUILayout.Label(string.Format(
                        "{0}: {1} | Due W{2} | {3}",
                        application.Id,
                        application.JobTitle,
                        application.DueWeek,
                        application.StatusText));
                GUILayout.EndVertical();
            }

            if (_session.Phase == GamePhase.AwaitingInterview && _session.ActiveInterview != null)
                DrawInterview(_session.ActiveInterview);

            if (_session.Phase == GamePhase.AwaitingOfferDecision && _session.PendingOffer != null)
                DrawOffer(_session.PendingOffer);

            if (_session.Summary != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Causal Chain");
                foreach (CausalLink link in _session.Summary.Chain)
                    GUILayout.Label(string.Format("W{0} [{1}] {2}", link.Week, link.Kind, link.Text));
                if (_session.Summary.MostMissedTags.Count > 0)
                    GUILayout.Label("Most missed: " + string.Join(", ",
                        _session.Summary.MostMissedTags.Select(item => item.DisplayName()).ToArray()));
                GUILayout.EndVertical();
            }
        }

        void DrawInterview(IInterviewSession interview)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Interview: " + interview.JobTitle + " di " + interview.Company);
            if (!interview.IsFinished)
            {
                InterviewQuestion question = interview.Questions[interview.CurrentIndex];
                GUILayout.Label(string.Format("Pertanyaan {0}/{1}: {2}",
                    question.Index + 1, interview.Questions.Count, question.Prompt));
                foreach (CardRef card in interview.AvailableCards)
                {
                    if (GUILayout.Button(CardLabel(card), GUILayout.MinHeight(30)))
                        AnswerInterview(card);
                }
            }
            else
            {
                GUILayout.Label("Hasil: " + interview.Result.Summary);
                GUILayout.Label(string.Format("Strong {0} | Okay {1} | Weak {2}",
                    interview.Result.StrongCount,
                    interview.Result.OkayCount,
                    interview.Result.WeakCount));
                if (GUILayout.Button("Close Interview", GUILayout.MinHeight(30)))
                    Execute("Close interview", () => _session.CloseInterview());
            }
            GUILayout.EndVertical();
        }

        void DrawOffer(Offer offer)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(string.Format("Offer: {0} di {1} | Salary tier {2}",
                offer.JobTitle, offer.Company, offer.SalaryTier));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Accept Offer", GUILayout.MinHeight(32)))
                Execute("Accept offer", () => _session.AcceptOffer());
            if (GUILayout.Button("Decline Offer", GUILayout.MinHeight(32)))
                Execute("Decline offer", () => _session.DeclineOffer());
            GUILayout.EndHorizontal();
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
            session.MarketChanged += OnMarketChanged;
            session.PostingsChanged += OnPostingsChanged;
            session.ApplicationUpdated += OnApplicationUpdated;
            session.InterviewStarted += OnInterviewStarted;
            session.OfferReceived += OnOfferReceived;
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
            session.MarketChanged -= OnMarketChanged;
            session.PostingsChanged -= OnPostingsChanged;
            session.ApplicationUpdated -= OnApplicationUpdated;
            session.InterviewStarted -= OnInterviewStarted;
            session.OfferReceived -= OnOfferReceived;
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
        void OnMarketChanged(MarketEvent market) { _eventLog.Add("MarketChanged(" + market.Kind + ")"); }
        void OnPostingsChanged() { _eventLog.Add("PostingsChanged"); }
        void OnApplicationUpdated(ApplicationRecord application)
        {
            _eventLog.Add("ApplicationUpdated(" + application.Id + ":" + application.Stage + ")");
        }
        void OnInterviewStarted(IInterviewSession interview)
        {
            _eventLog.Add("InterviewStarted(" + interview.JobTitle + ")");
        }
        void OnOfferReceived(Offer offer)
        {
            _eventLog.Add("OfferReceived(" + offer.JobTitle + ")");
        }

        void AnswerInterview(CardRef card)
        {
            AnswerFeedback feedback = _session.ActiveInterview.Answer(card);
            if (feedback == null)
            {
                _lastMessage = "Jawaban tidak valid.";
                return;
            }
            _lastMessage = "Jawaban: " + feedback.Quality + " - " + feedback.Text;
        }

        static string CardLabel(CardRef card)
        {
            return card.Kind == CardKind.Skill
                ? "Skill: " + card.Skill.DisplayName()
                : "Experience: " + card.ExperienceId;
        }

        CvDraft BuildCvFor(JobPosting posting)
        {
            int skillLimit = Math.Max(0, _session.Config.CvSkillSlots);
            int experienceLimit = Math.Max(0, _session.Config.CvExperienceSlots);
            IEnumerable<SkillTag> preferredSkills = posting.Template != null &&
                posting.Template.MustHave != null
                ? posting.Template.MustHave
                    .Where(tag => _session.OwnedSkills.Contains(tag))
                    .Concat(_session.OwnedSkills)
                : _session.OwnedSkills;
            return new CvDraft
            {
                Skills = preferredSkills.Distinct().Take(skillLimit).ToList(),
                Experiences = _session.OwnedExperiences.Take(experienceLimit).ToList()
            };
        }
    }
}
