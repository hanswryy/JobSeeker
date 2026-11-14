using System;

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
            _phase = GamePhase.Ended;
            _summary = new RunSummary
            {
                Outcome = outcome,
                Headline = headline,
                WeeksPlayed = _week,
                FinalScore = 0
            };
            if (PhaseChanged != null) PhaseChanged(_phase);
            if (RunEnded != null) RunEnded(_summary);
            return ActionResult.Success();
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
