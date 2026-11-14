#pragma warning disable 67   // event belum dipakai pada skeleton
using System;
using System.Collections.Generic;

namespace JobGame.Logic
{
    public sealed class GameFactory : IGameFactory
    {
        readonly GameContent _content; readonly GameConfig _config;
        public GameFactory(GameContent content, GameConfig config)
        { _content = content; _config = config; }
        public IGameSession NewRun(int seed) { return new GameState(_content, _config, seed); }
    }

    // SKELETON: A meng-commit ini dalam 1 jam setelah kickoff agar B bisa compile.
    // Semua command mengembalikan Fail sampai diimplementasi.
    public sealed class GameState : IGameSession
    {
        readonly GameConfig _config;
        public GameState(GameContent content, GameConfig config, int seed) { _config = config; }

        public GamePhase Phase { get { return GamePhase.Planning; } }
        public int Week { get { return 1; } }
        public int TotalWeeks { get { return _config.TotalWeeks; } }
        public int SlotsLeft { get { return _config.SlotsPerWeek; } }
        public float Energy { get { return 80f; } }
        public float Mental { get { return 70f; } }
        public float Runway { get { return 10f; } }
        public MarketEvent Market { get { return new MarketEvent(); } }
        public IReadOnlyList<JobPosting> Postings { get { return new List<JobPosting>(); } }
        public IReadOnlyList<SkillTag> OwnedSkills { get { return new List<SkillTag>(); } }
        public IReadOnlyList<ExperienceCard> OwnedExperiences { get { return new List<ExperienceCard>(); } }
        public int GetSkillLevel(SkillTag tag) { return 0; }
        public IReadOnlyList<ApplicationRecord> Applications { get { return new List<ApplicationRecord>(); } }
        public IInterviewSession ActiveInterview { get { return null; } }
        public Offer PendingOffer { get { return null; } }
        public RunSummary Summary { get { return null; } }
        public GameConfig Config { get { return _config; } }

        public MatchResult PreviewMatch(JobPosting posting, CvDraft cv) { return new MatchResult(); }
        public IReadOnlyList<SkillOption> GetSkillOptions() { return new List<SkillOption>(); }
        public IReadOnlyList<ExperienceOption> GetExperienceOptions() { return new List<ExperienceOption>(); }

        static ActionResult Todo() { return ActionResult.Fail(ActionError.WrongPhase, "Belum diimplementasi"); }
        public ActionResult TryApply(string postingId, CvDraft cv) { return Todo(); }
        public ActionResult TryUpgradeSkill(SkillTag tag) { return Todo(); }
        public ActionResult TryUpgradeExperience(string experienceId) { return Todo(); }
        public ActionResult TryRest() { return Todo(); }
        public ActionResult EndWeek() { return Todo(); }
        public ActionResult CloseInterview() { return Todo(); }
        public ActionResult AcceptOffer() { return Todo(); }
        public ActionResult DeclineOffer() { return Todo(); }

        public event Action<int> WeekStarted;
        public event Action<int> SlotsChanged;
        public event Action<ResourceKind, float, float> ResourceChanged;
        public event Action<MarketEvent> MarketChanged;
        public event Action PostingsChanged;
        public event Action HoldingsChanged;
        public event Action<ApplicationRecord> ApplicationUpdated;
        public event Action<GamePhase> PhaseChanged;
        public event Action<IInterviewSession> InterviewStarted;
        public event Action<Offer> OfferReceived;
        public event Action<RunSummary> RunEnded;
    }
}
