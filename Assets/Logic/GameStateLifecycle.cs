using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
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
            return EndRun(outcome, headline, null);
        }

        ActionResult EndRun(RunOutcome outcome, string headline, Offer acceptedOffer)
        {
            if (outcome != RunOutcome.Won)
                AddChain(ChainKind.Failure, "Minggu " + _week + ": " + headline);

            _phase = GamePhase.Ended;
            _summary = new RunSummary
            {
                Outcome = outcome,
                Headline = headline,
                WeeksPlayed = _week,
                FinalScore = acceptedOffer == null
                    ? 0
                    : acceptedOffer.SalaryTier * 100 + (_config.TotalWeeks - _week) * 10,
                AcceptedOffer = acceptedOffer,
                ApplicationsSent = _applications.Count,
                ScreeningsPassed = _applications.Count(item =>
                    item.Stage != ApplicationStage.Screening &&
                    item.RejectedAt != RejectStage.Screening),
                InterviewsPlayed = _interviewsPlayed,
                Chain = new List<CausalLink>(_chain),
                MostMissedTags = GetMostMissedTags()
            };
            if (_summary.Chain.Count == 0)
                _summary.Chain.Add(new CausalLink
                {
                    Week = _week,
                    Kind = ChainKind.Failure,
                    Text = headline.Length <= 90 ? headline : headline.Substring(0, 90)
                });
            if (PhaseChanged != null) PhaseChanged(_phase);
            if (RunEnded != null) RunEnded(_summary);
            return ActionResult.Success();
        }

        List<SkillTag> GetMostMissedTags()
        {
            Dictionary<SkillTag, int> counts = new Dictionary<SkillTag, int>();
            foreach (ApplicationRecord application in _applications)
            {
                if (application.RejectedAt != RejectStage.Screening) continue;
                foreach (SkillTag tag in application.MissingMustHave)
                    counts[tag] = counts.ContainsKey(tag) ? counts[tag] + 1 : 1;
            }

            return counts.OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key)
                .Take(3)
                .Select(item => item.Key)
                .ToList();
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
    }
}
