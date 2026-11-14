using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace JobGame.Tests
{
    public sealed class GameStateContractTests
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
        public void TwelveRestAndEndWeekCommandsEndTheRunAsLoss()
        {
            IGameSession session = CreateSession(17);

            for (int i = 0; i < 12; i++)
            {
                if (session.Phase == GamePhase.Planning && session.SlotsLeft > 0)
                    Assert.That(session.TryRest().Ok, Is.True);

                ActionResult result = session.EndWeek();
                if (i < 10)
                    Assert.That(result.Ok, Is.True);
                else
                    Assert.That(result.Error, Is.EqualTo(ActionError.RunEnded));
            }

            Assert.That(session.Phase, Is.EqualTo(GamePhase.Ended));
            Assert.That(session.Summary, Is.Not.Null);
            Assert.That(
                session.Summary.Outcome == RunOutcome.LostRunway ||
                session.Summary.Outcome == RunOutcome.LostMental ||
                session.Summary.Outcome == RunOutcome.LostTimeUp,
                Is.True);
            Assert.That(session.Week, Is.LessThanOrEqualTo(session.TotalWeeks));
        }

        [Test]
        public void PlanningPhaseCommandMatrixReturnsContractErrors()
        {
            Assert.That(CreateSession(1).TryApply("missing", new CvDraft()).Error,
                Is.EqualTo(ActionError.UnknownId));
            Assert.That(CreateSession(1).TryUpgradeSkill((SkillTag)999).Error,
                Is.EqualTo(ActionError.NotOffered));
            Assert.That(CreateSession(1).TryUpgradeExperience("missing").Error,
                Is.EqualTo(ActionError.NotOffered));
            Assert.That(CreateSession(1).CloseInterview().Error,
                Is.EqualTo(ActionError.WrongPhase));
            Assert.That(CreateSession(1).AcceptOffer().Error,
                Is.EqualTo(ActionError.WrongPhase));
            Assert.That(CreateSession(1).DeclineOffer().Error,
                Is.EqualTo(ActionError.WrongPhase));
        }

        [Test]
        public void EndedPhaseRejectsEveryCommandWithoutEvents()
        {
            GameConfig config = CreateConfig();
            config.TotalWeeks = 1;
            IGameSession session = CreateSession(2, config);
            Assert.That(session.EndWeek().Ok, Is.True);
            Assert.That(session.Phase, Is.EqualTo(GamePhase.Ended));

            int eventCount = 0;
            session.WeekStarted += delegate { eventCount++; };
            session.SlotsChanged += delegate { eventCount++; };
            session.ResourceChanged += delegate { eventCount++; };
            session.MarketChanged += delegate { eventCount++; };
            session.PostingsChanged += delegate { eventCount++; };
            session.HoldingsChanged += delegate { eventCount++; };
            session.ApplicationUpdated += delegate { eventCount++; };
            session.PhaseChanged += delegate { eventCount++; };
            session.InterviewStarted += delegate { eventCount++; };
            session.OfferReceived += delegate { eventCount++; };
            session.RunEnded += delegate { eventCount++; };

            Assert.That(session.TryApply("missing", new CvDraft()).Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.TryUpgradeSkill(SkillTag.SQL).Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.TryUpgradeExperience("missing").Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.TryRest().Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.EndWeek().Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.CloseInterview().Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.AcceptOffer().Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(session.DeclineOffer().Error, Is.EqualTo(ActionError.RunEnded));
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void SuccessfulCommandsEmitEventsInContractOrder()
        {
            IGameSession session = CreateSession(3);
            List<string> events = new List<string>();
            session.ResourceChanged += delegate(ResourceKind kind, float oldValue, float newValue)
            {
                events.Add("ResourceChanged:" + kind);
            };
            session.HoldingsChanged += delegate { events.Add("HoldingsChanged"); };
            session.SlotsChanged += delegate(int slots) { events.Add("SlotsChanged"); };
            session.WeekStarted += delegate(int week) { events.Add("WeekStarted"); };
            session.MarketChanged += delegate(MarketEvent market) { events.Add("MarketChanged"); };
            session.PostingsChanged += delegate { events.Add("PostingsChanged"); };

            SkillTag offeredSkill = session.GetSkillOptions()[0].Tag;
            Assert.That(session.TryUpgradeSkill(offeredSkill).Ok, Is.True);
            Assert.That(events, Is.EqualTo(new[]
            {
                "ResourceChanged:Energy",
                "ResourceChanged:Mental",
                "HoldingsChanged",
                "SlotsChanged"
            }));

            events.Clear();
            Assert.That(session.EndWeek().Ok, Is.True);
            CollectionAssert.AreEqual(new[]
            {
                "ResourceChanged:Energy",
                "ResourceChanged:Mental",
                "ResourceChanged:Runway",
                "WeekStarted",
                "SlotsChanged",
                "MarketChanged",
                "PostingsChanged"
            }, events);
        }

        [Test]
        public void SameSeedAndCommandsProduceTheSameState()
        {
            IGameSession first = CreateSession(99);
            IGameSession second = CreateSession(99);

            for (int week = 0; week < 3; week++)
            {
                SkillTag firstSkill = first.GetSkillOptions()[0].Tag;
                SkillTag secondSkill = second.GetSkillOptions()[0].Tag;
                Assert.That(firstSkill, Is.EqualTo(secondSkill));
                Assert.That(first.TryUpgradeSkill(firstSkill).Ok, Is.True);
                Assert.That(second.TryUpgradeSkill(secondSkill).Ok, Is.True);
                Assert.That(first.TryRest().Ok, Is.True);
                Assert.That(second.TryRest().Ok, Is.True);
                Assert.That(first.EndWeek().Ok, Is.True);
                Assert.That(second.EndWeek().Ok, Is.True);
                AssertEquivalentState(first, second);
            }
        }

        IGameSession CreateSession(int seed, GameConfig config = null)
        {
            if (config == null)
                config = CreateConfig();

            GameContent content = ScriptableObject.CreateInstance<GameContent>();
            _createdObjects.Add(content);
            Type factoryType = Type.GetType("JobGame.Logic.GameFactory, Assembly-CSharp");
            Assert.That(factoryType, Is.Not.Null, "GameFactory must be available in the Logic assembly.");
            IGameFactory factory = (IGameFactory)Activator.CreateInstance(
                factoryType, new object[] { content, config });
            return factory.NewRun(seed);
        }

        GameConfig CreateConfig()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            _createdObjects.Add(config);
            return config;
        }

        static void AssertEquivalentState(IGameSession first, IGameSession second)
        {
            Assert.That(second.Phase, Is.EqualTo(first.Phase));
            Assert.That(second.Week, Is.EqualTo(first.Week));
            Assert.That(second.SlotsLeft, Is.EqualTo(first.SlotsLeft));
            Assert.That(second.Energy, Is.EqualTo(first.Energy));
            Assert.That(second.Mental, Is.EqualTo(first.Mental));
            Assert.That(second.Runway, Is.EqualTo(first.Runway));
            Assert.That(second.Market.Kind, Is.EqualTo(first.Market.Kind));
            Assert.That(second.GetSkillOptions().Count, Is.EqualTo(first.GetSkillOptions().Count));
            for (int i = 0; i < first.GetSkillOptions().Count; i++)
            {
                Assert.That(second.GetSkillOptions()[i].Tag, Is.EqualTo(first.GetSkillOptions()[i].Tag));
                Assert.That(second.GetSkillOptions()[i].CurrentLevel,
                    Is.EqualTo(first.GetSkillOptions()[i].CurrentLevel));
            }
            Assert.That(second.OwnedSkills, Is.EqualTo(first.OwnedSkills));
        }
    }
}
