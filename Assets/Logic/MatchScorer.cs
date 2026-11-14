using System;
using System.Collections.Generic;

namespace JobGame.Logic
{
    public sealed class MatchScorer : IMatchScorer
    {
        readonly int _baseScore;
        readonly int _niceToHaveScore;
        readonly int _experienceScore;

        public MatchScorer(int baseScore = 50, int niceToHaveScore = 10, int experienceScore = 10)
        {
            _baseScore = baseScore;
            _niceToHaveScore = niceToHaveScore;
            _experienceScore = experienceScore;
        }

        public MatchResult Evaluate(JobTemplate job, CvDraft cv)
        {
            MatchResult result = new MatchResult();
            if (job == null)
                return result;

            HashSet<SkillTag> skills = new HashSet<SkillTag>();
            if (cv != null && cv.Skills != null)
                skills.UnionWith(cv.Skills);

            if (job.MustHave != null)
            {
                foreach (SkillTag required in job.MustHave)
                {
                    if (!skills.Contains(required) && !result.MissingMustHave.Contains(required))
                        result.MissingMustHave.Add(required);
                }
            }

            result.ExperienceCredibility = GetExperienceCredibility(cv);
            if (result.MissingMustHave.Count > 0)
            {
                result.Reject = RejectReason.MissingMustHave;
                return result;
            }

            if (result.ExperienceCredibility < Math.Max(0, job.MinExperience))
            {
                result.Reject = RejectReason.ExperienceTooLow;
                return result;
            }

            if (job.NiceToHave != null)
            {
                foreach (SkillTag niceToHave in job.NiceToHave)
                {
                    if (skills.Contains(niceToHave) && !result.MatchedNiceToHave.Contains(niceToHave))
                        result.MatchedNiceToHave.Add(niceToHave);
                }
            }

            result.Score = Math.Max(0, Math.Min(100,
                _baseScore +
                result.MatchedNiceToHave.Count * _niceToHaveScore +
                result.ExperienceCredibility * _experienceScore));
            return result;
        }

        static int GetExperienceCredibility(CvDraft cv)
        {
            if (cv == null || cv.Experiences == null)
                return 0;

            int total = 0;
            HashSet<string> ids = new HashSet<string>();
            foreach (ExperienceCard card in cv.Experiences)
            {
                if (card == null)
                    continue;
                string identity = string.IsNullOrEmpty(card.Id) ? card.name : card.Id;
                if (ids.Add(identity))
                    total += Math.Max(0, card.Credibility);
            }
            return total;
        }
    }
}
