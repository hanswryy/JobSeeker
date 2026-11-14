using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
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
        public IInterviewSession ActiveInterview { get { return _activeInterview; } }
        public Offer PendingOffer { get { return _pendingOffer; } }
        public RunSummary Summary { get { return _summary; } }
        public GameConfig Config { get { return _config; } }

        public MatchResult PreviewMatch(JobPosting posting, CvDraft cv)
        {
            return posting == null ? new MatchResult() : _matchScorer.Evaluate(posting.Template, cv);
        }

        public IReadOnlyList<SkillOption> GetSkillOptions()
        {
            return _skillOptions.Select(tag => new SkillOption
            {
                Tag = tag,
                CurrentLevel = GetSkillLevel(tag),
                UnlocksJobTitles = GetSkillUnlocks(tag),
                HintText = GetSkillUnlocks(tag).Count == 0
                    ? "Belum membuka job baru"
                    : "Membuka: " + string.Join(", ", GetSkillUnlocks(tag))
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
                    UnlocksJobTitles = GetExperienceUnlocks(card),
                    HintText = GetExperienceUnlocks(card).Count == 0
                        ? "Belum membuka job baru"
                        : "Membuka: " + string.Join(", ", GetExperienceUnlocks(card))
                })
                .ToList()
                .AsReadOnly();
        }
    }
}
