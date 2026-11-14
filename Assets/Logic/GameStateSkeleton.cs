using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    public sealed class GameFactory : IGameFactory
    {
        readonly GameContent _content; readonly GameConfig _config;
        public GameFactory(GameContent content, GameConfig config)
        { _content = content; _config = config; }
        public IGameSession NewRun(int seed) { return new GameState(_content, _config, seed); }
    }

    public sealed class GameState : IGameSession
    {
        static readonly SkillTag[] AllSkills = (SkillTag[])Enum.GetValues(typeof(SkillTag));
        readonly GameConfig _config;
        readonly GameContent _content;
        readonly int _seed;
        readonly Dictionary<SkillTag, int> _skillLevels = new Dictionary<SkillTag, int>();
        readonly List<ExperienceCard> _ownedExperiences = new List<ExperienceCard>();
        readonly List<ApplicationRecord> _applications = new List<ApplicationRecord>();
        readonly List<JobPosting> _postings = new List<JobPosting>();
        readonly List<SkillTag> _skillOptions = new List<SkillTag>();
        readonly List<string> _experienceOptions = new List<string>();
        GamePhase _phase;
        int _week;
        int _slotsLeft;
        float _energy;
        float _mental;
        float _runway;
        MarketEvent _market;
        RunSummary _summary;

        public GameState(GameContent content, GameConfig config, int seed)
        {
            _content = content;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _seed = seed;
            _phase = GamePhase.Planning;
            _week = 1;
            _slotsLeft = Math.Max(0, _config.SlotsPerWeek);
            _energy = ClampResource(_config.StartingEnergy);
            _mental = ClampResource(_config.StartingMental);
            _runway = Math.Max(0f, _config.StartingRunway);
            _market = new MarketEvent { Kind = MarketKind.Normal, Label = string.Empty };

            _skillLevels[SkillTag.Git] = 1;
            _skillLevels[SkillTag.Komunikasi] = 1;
            if (_content != null && _content.Experiences != null)
            {
                _ownedExperiences.AddRange(_content.Experiences.Where(card => card != null && card.OwnedAtStart));
            }
            RefreshWeeklyOptions();
        }

        public GamePhase Phase { get { return _phase; } }
        public int Week { get { return _week; } }
        public int TotalWeeks { get { return _config.TotalWeeks; } }
        public int SlotsLeft { get { return _slotsLeft; } }
        public float Energy { get { return _energy; } }
        public float Mental { get { return _mental; } }
        public float Runway { get { return _runway; } }
        public MarketEvent Market { get { return _market; } }
        public IReadOnlyList<JobPosting> Postings { get { return _postings.AsReadOnly(); } }
        public IReadOnlyList<SkillTag> OwnedSkills
        {
            get { return AllSkills.Where(tag => GetSkillLevel(tag) > 0).ToList().AsReadOnly(); }
        }
        public IReadOnlyList<ExperienceCard> OwnedExperiences { get { return _ownedExperiences.AsReadOnly(); } }
        public int GetSkillLevel(SkillTag tag)
        {
            int level;
            return _skillLevels.TryGetValue(tag, out level) ? level : 0;
        }
        public IReadOnlyList<ApplicationRecord> Applications { get { return _applications.AsReadOnly(); } }
        public IInterviewSession ActiveInterview { get { return null; } }
        public Offer PendingOffer { get { return null; } }
        public RunSummary Summary { get { return _summary; } }
        public GameConfig Config { get { return _config; } }

        public MatchResult PreviewMatch(JobPosting posting, CvDraft cv) { return new MatchResult(); }
        public IReadOnlyList<SkillOption> GetSkillOptions()
        {
            return _skillOptions.Select(tag => new SkillOption
            {
                Tag = tag,
                CurrentLevel = GetSkillLevel(tag),
                HintText = "Pilihan upgrade skill"
            }).ToList().AsReadOnly();
        }
        public IReadOnlyList<ExperienceOption> GetExperienceOptions()
        {
            if (_content == null || _content.Experiences == null)
                return new List<ExperienceOption>().AsReadOnly();

            return _experienceOptions
                .Select(id => _content.Experiences.FirstOrDefault(card => card != null && card.Id == id))
                .Where(card => card != null)
                .Select(card => new ExperienceOption
                {
                    Card = card,
                    HintText = "Pilihan upgrade experience"
                })
                .ToList()
                .AsReadOnly();
        }

        public ActionResult TryApply(string postingId, CvDraft cv)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            return ActionResult.Fail(ActionError.UnknownId, "Posting belum tersedia.");
        }

        public ActionResult TryUpgradeSkill(SkillTag tag)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            if (!_skillOptions.Contains(tag))
                return ActionResult.Fail(ActionError.NotOffered, "Skill tidak sedang ditawarkan.");
            if (GetSkillLevel(tag) >= _config.MaxSkillLevel)
                return ActionResult.Fail(ActionError.NotOffered, "Skill sudah mencapai level maksimum.");

            float oldEnergy = _energy;
            float oldMental = _mental;
            _energy = ClampResource(_energy - _config.SkillUpgradeEnergyCost);
            _mental = ClampResource(_mental - _config.SkillUpgradeMentalCost);
            _skillLevels[tag] = GetSkillLevel(tag) + 1;
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            if (HoldingsChanged != null) HoldingsChanged();
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult TryUpgradeExperience(string experienceId)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            if (!_experienceOptions.Contains(experienceId))
                return ActionResult.Fail(ActionError.NotOffered, "Experience tidak sedang ditawarkan.");

            ExperienceCard card = _content.Experiences.First(item => item != null && item.Id == experienceId);
            float oldEnergy = _energy;
            float oldMental = _mental;
            float oldRunway = _runway;
            _energy = ClampResource(_energy - _config.ExperienceUpgradeEnergyCost);
            _mental = ClampResource(_mental - _config.ExperienceUpgradeMentalCost);
            _runway = Math.Max(0f, _runway + _config.ExperienceUpgradeRunwayGain);
            _ownedExperiences.Add(card);
            _experienceOptions.Remove(experienceId);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            EmitResourceChanged(oldRunway, _runway, ResourceKind.Runway);
            if (HoldingsChanged != null) HoldingsChanged();
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult TryRest()
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;

            float oldEnergy = _energy;
            float oldMental = _mental;
            _energy = ClampResource(_energy + _config.RestEnergyGain);
            _mental = ClampResource(_mental + _config.RestMentalGain);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult EndWeek()
        {
            if (_phase == GamePhase.Ended)
                return ActionResult.Fail(ActionError.RunEnded, "Run sudah berakhir.");
            if (_phase != GamePhase.Planning)
                return ActionResult.Fail(ActionError.WrongPhase, "Aksi hanya tersedia saat planning.");

            float oldEnergy = _energy;
            float oldMental = _mental;
            float oldRunway = _runway;
            _energy = ClampResource(_energy + _config.WeeklyEnergyRegen);
            _mental = ClampResource(_mental - _config.WeeklyMentalCost);
            _runway = Math.Max(0f, _runway - _config.WeeklyRunwayBurn);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            EmitResourceChanged(oldRunway, _runway, ResourceKind.Runway);

            if (_runway <= 0f)
                return EndRun(RunOutcome.LostRunway, "Runway habis.");
            if (_mental <= 0f)
                return EndRun(RunOutcome.LostMental, "Mental habis.");
            if (_week >= _config.TotalWeeks)
                return EndRun(RunOutcome.LostTimeUp, "Waktu permainan habis.");

            _week++;
            _slotsLeft = Math.Max(0, _config.SlotsPerWeek);
            RefreshWeeklyOptions();
            if (WeekStarted != null) WeekStarted(_week);
            if (SlotsChanged != null) SlotsChanged(_slotsLeft);
            if (MarketChanged != null) MarketChanged(_market);
            if (PostingsChanged != null) PostingsChanged();
            return ActionResult.Success();
        }

        public ActionResult CloseInterview() { return WrongPhase(); }
        public ActionResult AcceptOffer() { return WrongPhase(); }
        public ActionResult DeclineOffer() { return WrongPhase(); }

        ActionResult RequirePlanning()
        {
            if (_phase == GamePhase.Ended)
                return ActionResult.Fail(ActionError.RunEnded, "Run sudah berakhir.");
            return _phase == GamePhase.Planning
                ? null
                : ActionResult.Fail(ActionError.WrongPhase, "Aksi hanya tersedia saat planning.");
        }

        ActionResult RequireSlot()
        {
            return _slotsLeft > 0
                ? null
                : ActionResult.Fail(ActionError.NoSlotsLeft, "Slot aksi minggu ini sudah habis.");
        }

        ActionResult WrongPhase()
        {
            return _phase == GamePhase.Ended
                ? ActionResult.Fail(ActionError.RunEnded, "Run sudah berakhir.")
                : ActionResult.Fail(ActionError.WrongPhase, "Fase belum mendukung aksi ini.");
        }

        void ConsumeSlot()
        {
            _slotsLeft--;
            if (SlotsChanged != null) SlotsChanged(_slotsLeft);
        }

        ActionResult EndRun(RunOutcome outcome, string headline)
        {
            _phase = GamePhase.Ended;
            _summary = new RunSummary
            {
                Outcome = outcome,
                Headline = headline,
                WeeksPlayed = _week,
                FinalScore = 0
            };
            if (PhaseChanged != null) PhaseChanged(_phase);
            if (RunEnded != null) RunEnded(_summary);
            return ActionResult.Success();
        }

        void RefreshWeeklyOptions()
        {
            _skillOptions.Clear();
            int offset = PositiveModulo(_seed + (_week * 31), AllSkills.Length);
            for (int i = 0; i < AllSkills.Length && _skillOptions.Count < 3; i++)
            {
                SkillTag tag = AllSkills[(offset + i) % AllSkills.Length];
                if (GetSkillLevel(tag) < _config.MaxSkillLevel)
                    _skillOptions.Add(tag);
            }

            _experienceOptions.Clear();
            if (_content == null || _content.Experiences == null) return;
            foreach (ExperienceCard card in _content.Experiences)
            {
                if (card != null && !_ownedExperiences.Contains(card) && !_experienceOptions.Contains(card.Id))
                    _experienceOptions.Add(card.Id);
                if (_experienceOptions.Count == 3) break;
            }
        }

        static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        float ClampResource(float value)
        {
            return Math.Max(0f, Math.Min(100f, value));
        }

        void EmitResourceChanged(float oldValue, float newValue, ResourceKind kind)
        {
            if (Math.Abs(oldValue - newValue) > 0.0001f && ResourceChanged != null)
                ResourceChanged(kind, oldValue, newValue);
        }

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
