using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    sealed class InterviewSession : IInterviewSession
    {
        static readonly string[] QuestionTemplates =
        {
            "Ceritakan pengalaman Anda menggunakan {0}.",
            "Bagaimana Anda menerapkan {0} untuk menyelesaikan masalah?",
            "Apa tantangan yang pernah Anda hadapi saat menggunakan {0}?"
        };

        readonly IReadOnlyList<InterviewQuestion> _questions;
        readonly IReadOnlyList<CardRef> _availableCards;
        readonly Dictionary<SkillTag, int> _skillLevels;
        readonly IReadOnlyList<ExperienceCard> _experiences;
        readonly int _difficulty;
        int _currentIndex;
        int _strongCount;
        int _okayCount;
        int _weakCount;
        InterviewResult _result;

        public InterviewSession(
            JobTemplate job,
            CvDraft cv,
            Dictionary<SkillTag, int> skillLevels,
            int questionSeed)
        {
            JobTitle = job.Title;
            Company = job.Company;
            _difficulty = Math.Max(1, job.InterviewDifficulty);
            _skillLevels = new Dictionary<SkillTag, int>(skillLevels);
            _experiences = (cv.Experiences ?? new List<ExperienceCard>()).AsReadOnly();
            _availableCards = BuildAvailableCards(cv).AsReadOnly();
            _questions = BuildQuestions(job, new Random(questionSeed)).AsReadOnly();
        }

        public string JobTitle { get; private set; }
        public string Company { get; private set; }
        public IReadOnlyList<InterviewQuestion> Questions { get { return _questions; } }
        public int CurrentIndex { get { return _currentIndex; } }
        public IReadOnlyList<CardRef> AvailableCards { get { return _availableCards; } }
        public bool IsFinished { get { return _result != null; } }
        public InterviewResult Result { get { return _result; } }

        public AnswerFeedback Answer(CardRef card)
        {
            if (IsFinished || !ContainsCard(card))
                return null;

            SkillTag topic = _questions[_currentIndex].Topic;
            AnswerQuality quality = GetQuality(card, topic);
            if (quality == AnswerQuality.Strong) _strongCount++;
            else if (quality == AnswerQuality.Okay) _okayCount++;
            else _weakCount++;

            AnswerFeedback feedback = new AnswerFeedback
            {
                QuestionIndex = _currentIndex,
                Quality = quality,
                Text = GetFeedbackText(quality)
            };
            _currentIndex++;
            if (_currentIndex == _questions.Count)
                Finish();
            return feedback;
        }

        List<CardRef> BuildAvailableCards(CvDraft cv)
        {
            List<CardRef> cards = new List<CardRef>();
            if (cv != null && cv.Skills != null)
            {
                foreach (SkillTag skill in cv.Skills.Distinct())
                    cards.Add(CardRef.ForSkill(skill));
            }
            if (cv != null && cv.Experiences != null)
            {
                foreach (ExperienceCard card in cv.Experiences)
                {
                    if (card != null && !cards.Any(item =>
                        item.Kind == CardKind.Experience && item.ExperienceId == card.Id))
                        cards.Add(CardRef.ForExperience(card.Id));
                }
            }
            return cards;
        }

        List<InterviewQuestion> BuildQuestions(JobTemplate job, Random random)
        {
            List<SkillTag> topics = new List<SkillTag>();
            AddDistinct(topics, job.MustHave);
            AddDistinct(topics, job.NiceToHave);
            if (topics.Count == 0)
                topics.Add(SkillTag.Git);

            for (int i = topics.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                SkillTag swap = topics[i];
                topics[i] = topics[swapIndex];
                topics[swapIndex] = swap;
            }

            int questionCount = Math.Min(3, topics.Count);
            List<InterviewQuestion> questions = new List<InterviewQuestion>();
            for (int i = 0; i < questionCount; i++)
            {
                string topicName = topics[i].DisplayName();
                questions.Add(new InterviewQuestion
                {
                    Index = i,
                    Topic = topics[i],
                    Prompt = string.Format(
                        QuestionTemplates[random.Next(QuestionTemplates.Length)],
                        topicName)
                });
            }
            return questions;
        }

        static void AddDistinct(List<SkillTag> target, SkillTag[] source)
        {
            if (source == null) return;
            foreach (SkillTag tag in source)
                if (!target.Contains(tag)) target.Add(tag);
        }

        bool ContainsCard(CardRef card)
        {
            return _availableCards.Any(item =>
                item.Kind == card.Kind &&
                (card.Kind == CardKind.Skill
                    ? item.Skill == card.Skill
                    : item.ExperienceId == card.ExperienceId));
        }

        AnswerQuality GetQuality(CardRef card, SkillTag topic)
        {
            if (card.Kind == CardKind.Experience)
            {
                ExperienceCard experience = _experiences.FirstOrDefault(item =>
                    item != null && item.Id == card.ExperienceId);
                return experience != null && experience.ProvesTags != null &&
                    experience.ProvesTags.Contains(topic)
                    ? AnswerQuality.Strong
                    : AnswerQuality.Weak;
            }

            int level;
            return _skillLevels.TryGetValue(card.Skill, out level) && card.Skill == topic
                ? (level >= 2 ? AnswerQuality.Strong : AnswerQuality.Okay)
                : AnswerQuality.Weak;
        }

        void Finish()
        {
            int required = Math.Min(_difficulty == 1 ? 1 : 2, _questions.Count);
            bool passed = (_strongCount + _okayCount) >= required;
            if (_difficulty >= 3)
                passed = passed && _strongCount >= 1;
            _result = new InterviewResult
            {
                Passed = passed,
                StrongCount = _strongCount,
                OkayCount = _okayCount,
                WeakCount = _weakCount,
                Summary = passed ? "Interview berhasil." : "Interview belum memenuhi standar."
            };
        }

        static string GetFeedbackText(AnswerQuality quality)
        {
            switch (quality)
            {
                case AnswerQuality.Strong: return "Jawaban sangat kuat.";
                case AnswerQuality.Okay: return "Jawaban cukup baik.";
                default: return "Jawaban masih lemah.";
            }
        }
    }
}
