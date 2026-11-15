using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
        const int SkillOptionSeedSalt = 104729;

        void RefreshWeeklyOptions()
        {
            List<SkillTag> previousSkills = new List<SkillTag>(_skillOptions);
            _skillOptions.Clear();

            List<SkillTag> candidates = AllSkills
                .Where(tag => GetSkillLevel(tag) < _config.MaxSkillLevel)
                .ToList();
            Random random = new Random(CombineSeed(_seed ^ SkillOptionSeedSalt, _week));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                SkillTag swap = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = swap;
            }

            _skillOptions.AddRange(candidates.Take(3));
            List<SkillTag> unlocks = candidates
                .Where(tag => GetSkillUnlocks(tag).Count > 0)
                .ToList();
            if (unlocks.Count > 0 && !_skillOptions.Any(unlocks.Contains))
                _skillOptions[random.Next(_skillOptions.Count)] = unlocks[random.Next(unlocks.Count)];

            bool sameAsPrevious = previousSkills.Count == _skillOptions.Count &&
                previousSkills.All(_skillOptions.Contains);
            if (sameAsPrevious && candidates.Count > _skillOptions.Count)
            {
                List<SkillTag> alternatives = candidates
                    .Where(tag => !_skillOptions.Contains(tag))
                    .ToList();
                int protectedUnlockIndex = unlocks.Count == 0
                    ? -1
                    : _skillOptions.FindIndex(unlocks.Contains);
                List<int> replaceableIndices = Enumerable.Range(0, _skillOptions.Count)
                    .Where(index => index != protectedUnlockIndex)
                    .ToList();

                if (alternatives.Count > 0 && replaceableIndices.Count > 0)
                {
                    int index = replaceableIndices[random.Next(replaceableIndices.Count)];
                    _skillOptions[index] = alternatives[random.Next(alternatives.Count)];
                }
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

        List<string> GetSkillUnlocks(SkillTag tag)
        {
            if (GetSkillLevel(tag) > 0 || _content == null || _content.Jobs == null)
                return new List<string>();

            return _content.Jobs
                .Where(job => job != null && job.MustHave != null)
                .Where(job => job.MustHave.All(required =>
                    GetSkillLevel(required) > 0 || required == tag))
                .Where(job => job.MustHave.Any(required => required == tag))
                .Select(job => job.Title)
                .Distinct()
                .ToList();
        }

        List<string> GetExperienceUnlocks(ExperienceCard candidate)
        {
            if (candidate == null || _content == null || _content.Jobs == null)
                return new List<string>();

            int currentCredibility = BestExperienceCredibility(_ownedExperiences);
            List<ExperienceCard> after = new List<ExperienceCard>(_ownedExperiences) { candidate };
            int nextCredibility = BestExperienceCredibility(after);
            return _content.Jobs
                .Where(job => job != null && currentCredibility < job.MinExperience &&
                    nextCredibility >= job.MinExperience)
                .Select(job => job.Title)
                .Distinct()
                .ToList();
        }

        static int BestExperienceCredibility(IEnumerable<ExperienceCard> cards)
        {
            return cards.Where(card => card != null)
                .Select(card => Math.Max(0, card.Credibility))
                .OrderByDescending(value => value)
                .Take(2)
                .Sum();
        }

        void RefreshPostings()
        {
            _postings.Clear();
            if (_content == null || _content.Jobs == null || _content.Jobs.Count == 0) return;

            List<JobTemplate> jobs = _content.Jobs.Where(job => job != null)
                .GroupBy(job => job.Id).Select(group => group.First()).ToList();
            if (jobs.Count == 0) return;

            Random random = new Random(CombineSeed(_seed, _week));
            for (int i = jobs.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                JobTemplate swap = jobs[i];
                jobs[i] = jobs[swapIndex];
                jobs[swapIndex] = swap;
            }

            int count = Math.Min(Math.Max(0, _config.PostingsPerWeek), jobs.Count);
            for (int i = 0; i < count; i++)
            {
                JobTemplate job = jobs[i];
                _postings.Add(new JobPosting
                {
                    Id = "wk" + _week + "-" + job.Id,
                    Template = job,
                    WeekPosted = _week,
                    Applied = false
                });
            }
        }

        static int CombineSeed(int seed, int week)
        {
            unchecked { return seed * 397 ^ week * 7919; }
        }

    }
}
