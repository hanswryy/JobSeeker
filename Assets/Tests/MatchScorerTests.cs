using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace JobGame.Tests
{
    public sealed class MatchScorerTests
    {
        readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object createdObject in _createdObjects)
                UnityEngine.Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void MissingMustHaveRejectsWithZeroScore()
        {
            JobTemplate job = CreateJob(new[] { SkillTag.SQL }, new SkillTag[0], 0);
            MatchResult result = CreateScorer().Evaluate(job, new CvDraft());

            Assert.That(result.Reject, Is.EqualTo(RejectReason.MissingMustHave));
            Assert.That(result.Score, Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { SkillTag.SQL }, result.MissingMustHave);
        }

        [Test]
        public void CompleteGateGetsBaseScore()
        {
            JobTemplate job = CreateJob(new[] { SkillTag.SQL }, new SkillTag[0], 0);
            MatchResult result = CreateScorer().Evaluate(job, CvWithSkills(SkillTag.SQL));

            Assert.That(result.Reject, Is.EqualTo(RejectReason.None));
            Assert.That(result.Score, Is.EqualTo(50));
            Assert.That(result.MatchedNiceToHave, Is.Empty);
        }

        [Test]
        public void NiceToHaveSkillsAddOneBonusPerDistinctTag()
        {
            JobTemplate job = CreateJob(
                new[] { SkillTag.SQL },
                new[] { SkillTag.Python, SkillTag.Docker },
                0);
            MatchResult result = CreateScorer().Evaluate(
                job, CvWithSkills(SkillTag.SQL, SkillTag.Python, SkillTag.Docker));

            Assert.That(result.Score, Is.EqualTo(70));
            CollectionAssert.AreEqual(
                new[] { SkillTag.Python, SkillTag.Docker },
                result.MatchedNiceToHave);
        }

        [Test]
        public void DuplicateSkillsAndCardsAreCountedOnce()
        {
            ExperienceCard card = CreateExperience("exp", 2);
            JobTemplate job = CreateJob(new[] { SkillTag.SQL }, new[] { SkillTag.Python }, 0);
            CvDraft cv = CvWithSkills(SkillTag.SQL, SkillTag.SQL, SkillTag.Python, SkillTag.Python);
            cv.Experiences.Add(card);
            cv.Experiences.Add(card);

            MatchResult result = CreateScorer().Evaluate(job, cv);

            Assert.That(result.Score, Is.EqualTo(80));
            Assert.That(result.ExperienceCredibility, Is.EqualTo(2));
            Assert.That(result.MatchedNiceToHave.Count, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientExperienceRejectsAfterSkillGate()
        {
            ExperienceCard card = CreateExperience("exp", 1);
            JobTemplate job = CreateJob(new[] { SkillTag.SQL }, new SkillTag[0], 2);
            CvDraft cv = CvWithSkills(SkillTag.SQL);
            cv.Experiences.Add(card);

            MatchResult result = CreateScorer().Evaluate(job, cv);

            Assert.That(result.Reject, Is.EqualTo(RejectReason.ExperienceTooLow));
            Assert.That(result.Score, Is.EqualTo(0));
            Assert.That(result.ExperienceCredibility, Is.EqualTo(1));
        }

        [Test]
        public void ScoreIsCappedAtOneHundred()
        {
            JobTemplate job = CreateJob(
                new[] { SkillTag.SQL },
                new[] { SkillTag.Python, SkillTag.Docker, SkillTag.Git },
                0);
            MatchResult result = CreateScorer(90, 20, 20).Evaluate(
                job, CvWithSkills(SkillTag.SQL, SkillTag.Python, SkillTag.Docker, SkillTag.Git));

            Assert.That(result.Score, Is.EqualTo(100));
        }

        static CvDraft CvWithSkills(params SkillTag[] skills)
        {
            return new CvDraft { Skills = new List<SkillTag>(skills) };
        }

        static IMatchScorer CreateScorer(int baseScore = 50, int niceToHaveScore = 10,
            int experienceScore = 10)
        {
            Type scorerType = Type.GetType("JobGame.Logic.MatchScorer, Assembly-CSharp");
            Assert.That(scorerType, Is.Not.Null);
            return (IMatchScorer)Activator.CreateInstance(
                scorerType, new object[] { baseScore, niceToHaveScore, experienceScore });
        }

        JobTemplate CreateJob(SkillTag[] mustHave, SkillTag[] niceToHave, int minExperience)
        {
            JobTemplate job = ScriptableObject.CreateInstance<JobTemplate>();
            _createdObjects.Add(job);
            job.Id = "test-job";
            job.MustHave = mustHave;
            job.NiceToHave = niceToHave;
            job.MinExperience = minExperience;
            return job;
        }

        ExperienceCard CreateExperience(string id, int credibility)
        {
            ExperienceCard card = ScriptableObject.CreateInstance<ExperienceCard>();
            _createdObjects.Add(card);
            card.Id = id;
            card.Credibility = credibility;
            return card;
        }
    }

    public sealed class PostingGeneratorTests
    {
        readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object createdObject in _createdObjects)
                UnityEngine.Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void PostingsHaveConfiguredCountUniqueIdsAndStayStableWithinWeek()
        {
            GameConfig config = CreateConfig();
            config.PostingsPerWeek = 3;
            GameContent content = CreateContent(5);
            IGameSession session = CreateSession(content, config, 42);

            IReadOnlyList<JobPosting> first = session.Postings;
            IReadOnlyList<JobPosting> second = session.Postings;

            Assert.That(first.Count, Is.EqualTo(3));
            Assert.That(first[0].Id, Is.EqualTo(second[0].Id));
            Assert.That(first[1].Id, Is.EqualTo(second[1].Id));
            Assert.That(first[2].Id, Is.EqualTo(second[2].Id));
            Assert.That(first[0].WeekPosted, Is.EqualTo(1));
            Assert.That(new HashSet<string>(new[] { first[0].Id, first[1].Id, first[2].Id }).Count,
                Is.EqualTo(3));
        }

        [Test]
        public void SameSeedProducesTheSamePostingIdsForEachWeek()
        {
            GameConfig firstConfig = CreateConfig();
            GameConfig secondConfig = CreateConfig();
            GameContent firstContent = CreateContent(5);
            GameContent secondContent = CreateContent(5);
            IGameSession first = CreateSession(firstContent, firstConfig, 77);
            IGameSession second = CreateSession(secondContent, secondConfig, 77);

            for (int week = 1; week <= 3; week++)
            {
                Assert.That(first.Postings.Count, Is.EqualTo(second.Postings.Count));
                for (int i = 0; i < first.Postings.Count; i++)
                    Assert.That(first.Postings[i].Id, Is.EqualTo(second.Postings[i].Id));
                Assert.That(first.EndWeek().Ok, Is.True);
                Assert.That(second.EndWeek().Ok, Is.True);
            }
        }

        GameContent CreateContent(int jobCount)
        {
            GameContent content = ScriptableObject.CreateInstance<GameContent>();
            _createdObjects.Add(content);
            for (int i = 0; i < jobCount; i++)
            {
                JobTemplate job = CreateJob("job-" + i);
                content.Jobs.Add(job);
            }
            return content;
        }

        JobTemplate CreateJob(string id)
        {
            JobTemplate job = ScriptableObject.CreateInstance<JobTemplate>();
            _createdObjects.Add(job);
            job.Id = id;
            job.Title = id;
            return job;
        }

        GameConfig CreateConfig()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            _createdObjects.Add(config);
            return config;
        }

        static IGameSession CreateSession(GameContent content, GameConfig config, int seed)
        {
            Type factoryType = Type.GetType("JobGame.Logic.GameFactory, Assembly-CSharp");
            Assert.That(factoryType, Is.Not.Null);
            IGameFactory factory = (IGameFactory)Activator.CreateInstance(
                factoryType, new object[] { content, config });
            return factory.NewRun(seed);
        }
    }
}
