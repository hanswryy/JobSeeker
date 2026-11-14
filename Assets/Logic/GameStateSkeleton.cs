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

    public sealed partial class GameState : IGameSession
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
        readonly IMatchScorer _matchScorer;
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
            _matchScorer = new MatchScorer(
                _config.MatchBaseScore,
                _config.MatchNiceToHaveScore,
                _config.MatchExperienceScore);

            _skillLevels[SkillTag.Git] = 1;
            _skillLevels[SkillTag.Komunikasi] = 1;
            if (_content != null && _content.Experiences != null)
            {
                _ownedExperiences.AddRange(_content.Experiences.Where(card => card != null && card.OwnedAtStart));
            }
            RefreshPostings();
            RefreshWeeklyOptions();
        }
    }
}
