using System.Collections.Generic;
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace JobGame.Tests
{
    public sealed class ApplicationPipelineTests
    {
        readonly List<UnityObject> _createdObjects = new List<UnityObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityObject createdObject in _createdObjects)
                UnityObject.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void SuccessfulApplicationReachesOfferAfterInterviewAndFinalReview()
        {
            GameConfig config = CreateConfig();
            config.ScreeningBaseChance = 1f;
            config.FinalPassChance = 1f;
            IGameSession session = CreateSession(config, SkillTag.Git);

            JobPosting posting = session.Postings[0];
            Assert.That(session.TryApply(posting.Id, CvWith(SkillTag.Git)).Ok, Is.True);
            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.Screening));
            Assert.That(session.Applications[0].DueWeek, Is.EqualTo(3));

            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingInterview));
            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.InterviewReady));

            IInterviewSession interview = session.ActiveInterview;
            Assert.That(interview.Answer(CardRef.ForSkill(SkillTag.Git)), Is.Not.Null);
            Assert.That(interview.IsFinished, Is.True);
            Assert.That(session.CloseInterview().Ok, Is.True);
            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.FinalReview));

            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingOfferDecision));
            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.OfferReady));
            Assert.That(session.AcceptOffer().Ok, Is.True);
            Assert.That(session.Summary.Outcome, Is.EqualTo(RunOutcome.Won));
        }

        [Test]
        public void MissingMustHaveCanBeSubmittedAndIsRejectedAtScreening()
        {
            GameConfig config = CreateConfig();
            IGameSession session = CreateSession(config, SkillTag.Python);

            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.Screening));
            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.EndWeek().Ok, Is.True);

            Assert.That(session.Applications[0].Stage, Is.EqualTo(ApplicationStage.Rejected));
            Assert.That(session.Applications[0].RejectedAt, Is.EqualTo(RejectStage.Screening));
        }

        [Test]
        public void MultipleInterviewsAreQueuedBeforeReturningToPlanning()
        {
            GameConfig config = CreateConfig();
            config.PostingsPerWeek = 2;
            config.ScreeningBaseChance = 1f;
            config.FinalPassChance = 1f;
            IGameSession session = CreateSession(config, SkillTag.Git, 2);

            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            Assert.That(session.TryApply(session.Postings[1].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            EndToWeek(session, 3);

            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingInterview));
            Assert.That(session.ActiveInterview, Is.Not.Null);
            AnswerCurrentInterview(session);
            Assert.That(session.CloseInterview().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingInterview));
            AnswerCurrentInterview(session);
            Assert.That(session.CloseInterview().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(session.Applications.Where(item => item.Stage == ApplicationStage.FinalReview).Count(),
                Is.EqualTo(2));
            Assert.That(session.Summary, Is.Null);
        }

        [Test]
        public void MultipleOffersAreQueuedAfterFinalReview()
        {
            GameConfig config = CreateConfig();
            config.PostingsPerWeek = 2;
            config.ScreeningBaseChance = 1f;
            config.FinalPassChance = 1f;
            IGameSession session = CreateSession(config, SkillTag.Git, 2);

            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            Assert.That(session.TryApply(session.Postings[1].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            EndToWeek(session, 3);
            AnswerCurrentInterview(session);
            Assert.That(session.CloseInterview().Ok, Is.True);
            AnswerCurrentInterview(session);
            Assert.That(session.CloseInterview().Ok, Is.True);
            Assert.That(session.EndWeek().Ok, Is.True);

            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingOfferDecision));
            string firstOffer = session.PendingOffer.ApplicationId;
            Assert.That(session.DeclineOffer().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.AwaitingOfferDecision));
            Assert.That(session.PendingOffer.ApplicationId, Is.Not.EqualTo(firstOffer));
            Assert.That(session.AcceptOffer().Ok, Is.True);
            Assert.That(session.Summary.Outcome, Is.EqualTo(RunOutcome.Won));
        }

        [Test]
        public void DueWeekAndEndWeekEventOrderIncludeApplicationAndInterviewEvents()
        {
            GameConfig config = CreateConfig();
            config.ScreeningBaseChance = 1f;
            IGameSession session = CreateSession(config, SkillTag.Git);
            List<string> events = new List<string>();
            session.WeekStarted += delegate { events.Add("WeekStarted"); };
            session.SlotsChanged += delegate { events.Add("SlotsChanged"); };
            session.MarketChanged += delegate { events.Add("MarketChanged"); };
            session.PostingsChanged += delegate { events.Add("PostingsChanged"); };
            session.ApplicationUpdated += delegate { events.Add("ApplicationUpdated"); };
            session.PhaseChanged += delegate { events.Add("PhaseChanged"); };
            session.InterviewStarted += delegate { events.Add("InterviewStarted"); };

            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            events.Clear();
            Assert.That(session.EndWeek().Ok, Is.True);
            events.Clear();
            Assert.That(session.EndWeek().Ok, Is.True);

            Assert.That(session.Applications[0].DueWeek, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[]
            {
                "WeekStarted", "SlotsChanged", "MarketChanged", "PostingsChanged",
                "ApplicationUpdated", "PhaseChanged", "InterviewStarted"
            }, events);
        }

        [Test]
        public void IdenticalRunsProduceIdenticalSummary()
        {
            GameConfig config = CreateConfig();
            config.ScreeningBaseChance = 1f;
            config.FinalPassChance = 1f;
            IGameSession first = CreateSession(config, SkillTag.Git);
            IGameSession second = CreateSession(config, SkillTag.Git);
            PlayWinningRun(first);
            PlayWinningRun(second);

            Assert.That(second.Summary.Outcome, Is.EqualTo(first.Summary.Outcome));
            Assert.That(second.Summary.Headline, Is.EqualTo(first.Summary.Headline));
            Assert.That(second.Summary.FinalScore, Is.EqualTo(first.Summary.FinalScore));
            Assert.That(second.Summary.ApplicationsSent, Is.EqualTo(first.Summary.ApplicationsSent));
            Assert.That(second.Summary.ScreeningsPassed, Is.EqualTo(first.Summary.ScreeningsPassed));
            Assert.That(second.Summary.InterviewsPlayed, Is.EqualTo(first.Summary.InterviewsPlayed));
            Assert.That(second.Summary.Chain.Count, Is.EqualTo(first.Summary.Chain.Count));
            for (int i = 0; i < first.Summary.Chain.Count; i++)
            {
                Assert.That(second.Summary.Chain[i].Week, Is.EqualTo(first.Summary.Chain[i].Week));
                Assert.That(second.Summary.Chain[i].Kind, Is.EqualTo(first.Summary.Chain[i].Kind));
                Assert.That(second.Summary.Chain[i].Text, Is.EqualTo(first.Summary.Chain[i].Text));
            }
            Assert.That(second.Summary.MostMissedTags, Is.EqualTo(first.Summary.MostMissedTags));
            Assert.That(first.Summary.Chain.Count, Is.GreaterThanOrEqualTo(3));
            for (int i = 1; i < first.Summary.Chain.Count; i++)
                Assert.That(first.Summary.Chain[i].Week, Is.GreaterThanOrEqualTo(
                    first.Summary.Chain[i - 1].Week));
        }

        [Test]
        public void LostRunSummaryContainsMissedTagsAndFailureChain()
        {
            GameConfig config = CreateConfig();
            config.TotalWeeks = 2;
            config.ScreeningLagWeeks = 0;
            IGameSession session = CreateSession(config, SkillTag.Python);

            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.Applications[0].RejectedAt, Is.EqualTo(RejectStage.Screening));
            Assert.That(session.EndWeek().Ok, Is.True);

            Assert.That(session.Summary.Outcome, Is.EqualTo(RunOutcome.LostTimeUp));
            Assert.That(session.Summary.ApplicationsSent, Is.EqualTo(1));
            Assert.That(session.Summary.ScreeningsPassed, Is.EqualTo(0));
            Assert.That(session.Summary.MostMissedTags, Does.Contain(SkillTag.Python));
            Assert.That(session.Summary.Chain.Any(item => item.Kind == ChainKind.Failure), Is.True);
        }

        [Test]
        public void UpgradeHintsListJobsUnlockedBySkillAndExperience()
        {
            GameConfig config = CreateConfig();
            GameContent content = CreateContent(new[] { SkillTag.Git }, 1);
            JobTemplate job = content.Jobs[0];
            job.Title = "Job Git Baru";
            job.MustHave = new[] { SkillTag.Git, SkillTag.Python };
            job.MinExperience = 2;
            IGameSession session = CreateSession(content, config, 23);

            SkillOption skill = session.GetSkillOptions()
                .FirstOrDefault(item => item.Tag == SkillTag.Python);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill.UnlocksJobTitles, Does.Contain("Job Git Baru"));
            Assert.That(skill.HintText, Does.Contain("Job Git Baru"));
            ExperienceOption experience = session.GetExperienceOptions()
                .FirstOrDefault(item => item.Card.Id == "exp_test");
            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.UnlocksJobTitles, Does.Contain("Job Git Baru"));
        }

        GameConfig CreateConfig()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            _createdObjects.Add(config);
            config.TotalWeeks = 12;
            config.PostingsPerWeek = 1;
            config.SlotsPerWeek = 3;
            return config;
        }

        IGameSession CreateSession(GameConfig config, SkillTag requiredSkill, int jobCount = 1)
        {
            GameContent content = CreateContent(new[] { requiredSkill }, jobCount);
            return CreateSession(content, config, 23);
        }

        IGameSession CreateSession(GameContent content, GameConfig config, int seed)
        {

            Type factoryType = Type.GetType("JobGame.Logic.GameFactory, Assembly-CSharp");
            Assert.That(factoryType, Is.Not.Null);
            IGameFactory factory = (IGameFactory)Activator.CreateInstance(
                factoryType, new object[] { content, config });
            return factory.NewRun(seed);
        }

        GameContent CreateContent(SkillTag[] requiredSkills, int jobCount = 1)
        {
            GameContent content = ScriptableObject.CreateInstance<GameContent>();
            _createdObjects.Add(content);
            for (int i = 0; i < jobCount; i++)
            {
                JobTemplate job = ScriptableObject.CreateInstance<JobTemplate>();
                _createdObjects.Add(job);
                job.Id = "job-test-" + i;
                job.Title = "Pekerjaan Uji " + i;
                job.Company = "Perusahaan Uji";
                job.MustHave = requiredSkills;
                job.NiceToHave = new SkillTag[0];
                job.InterviewDifficulty = 1;
                job.FinalLagWeeks = 1;
                job.SalaryTier = 3;
                content.Jobs.Add(job);
            }
            ExperienceCard experience = ScriptableObject.CreateInstance<ExperienceCard>();
            _createdObjects.Add(experience);
            experience.Id = "exp_test";
            experience.DisplayName = "Experience Uji";
            experience.Credibility = 2;
            content.Experiences.Add(experience);
            return content;
        }

        static CvDraft CvWith(SkillTag skill)
        {
            return new CvDraft { Skills = new List<SkillTag> { skill } };
        }

        static void EndToWeek(IGameSession session, int targetWeek)
        {
            while (session.Week < targetWeek)
                Assert.That(session.EndWeek().Ok, Is.True);
        }

        static void AnswerCurrentInterview(IGameSession session)
        {
            IInterviewSession interview = session.ActiveInterview;
            while (!interview.IsFinished)
                Assert.That(interview.Answer(interview.AvailableCards[0]), Is.Not.Null);
        }

        static void PlayWinningRun(IGameSession session)
        {
            Assert.That(session.TryApply(session.Postings[0].Id, CvWith(SkillTag.Git)).Ok, Is.True);
            EndToWeek(session, 3);
            AnswerCurrentInterview(session);
            Assert.That(session.CloseInterview().Ok, Is.True);
            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.AcceptOffer().Ok, Is.True);
        }
    }
}
