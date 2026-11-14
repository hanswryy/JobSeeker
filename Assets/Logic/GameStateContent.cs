using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
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

        static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
