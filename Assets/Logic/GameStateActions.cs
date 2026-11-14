using System;
using System.Linq;

namespace JobGame.Logic
{
    public sealed partial class GameState
    {
        public ActionResult TryApply(string postingId, CvDraft cv)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            return ActionResult.Fail(ActionError.UnknownId, "Posting belum tersedia.");
        }

        public ActionResult TryUpgradeSkill(SkillTag tag)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            if (!_skillOptions.Contains(tag))
                return ActionResult.Fail(ActionError.NotOffered, "Skill tidak sedang ditawarkan.");
            if (GetSkillLevel(tag) >= _config.MaxSkillLevel)
                return ActionResult.Fail(ActionError.NotOffered, "Skill sudah mencapai level maksimum.");

            float oldEnergy = _energy;
            float oldMental = _mental;
            _energy = ClampResource(_energy - _config.SkillUpgradeEnergyCost);
            _mental = ClampResource(_mental - _config.SkillUpgradeMentalCost);
            _skillLevels[tag] = GetSkillLevel(tag) + 1;
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            if (HoldingsChanged != null) HoldingsChanged();
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult TryUpgradeExperience(string experienceId)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;
            if (!_experienceOptions.Contains(experienceId))
                return ActionResult.Fail(ActionError.NotOffered, "Experience tidak sedang ditawarkan.");

            ExperienceCard card = _content.Experiences.First(item => item != null && item.Id == experienceId);
            float oldEnergy = _energy;
            float oldMental = _mental;
            float oldRunway = _runway;
            _energy = ClampResource(_energy - _config.ExperienceUpgradeEnergyCost);
            _mental = ClampResource(_mental - _config.ExperienceUpgradeMentalCost);
            _runway = Math.Max(0f, _runway + _config.ExperienceUpgradeRunwayGain);
            _ownedExperiences.Add(card);
            _experienceOptions.Remove(experienceId);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            EmitResourceChanged(oldRunway, _runway, ResourceKind.Runway);
            if (HoldingsChanged != null) HoldingsChanged();
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult TryRest()
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;

            float oldEnergy = _energy;
            float oldMental = _mental;
            _energy = ClampResource(_energy + _config.RestEnergyGain);
            _mental = ClampResource(_mental + _config.RestMentalGain);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult EndWeek()
        {
            if (_phase == GamePhase.Ended)
                return ActionResult.Fail(ActionError.RunEnded, "Run sudah berakhir.");
            if (_phase != GamePhase.Planning)
                return ActionResult.Fail(ActionError.WrongPhase, "Aksi hanya tersedia saat planning.");

            float oldEnergy = _energy;
            float oldMental = _mental;
            float oldRunway = _runway;
            _energy = ClampResource(_energy + _config.WeeklyEnergyRegen);
            _mental = ClampResource(_mental - _config.WeeklyMentalCost);
            _runway = Math.Max(0f, _runway - _config.WeeklyRunwayBurn);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            EmitResourceChanged(oldRunway, _runway, ResourceKind.Runway);

            if (_runway <= 0f) return EndRun(RunOutcome.LostRunway, "Runway habis.");
            if (_mental <= 0f) return EndRun(RunOutcome.LostMental, "Mental habis.");
            if (_week >= _config.TotalWeeks) return EndRun(RunOutcome.LostTimeUp, "Waktu permainan habis.");

            _week++;
            _slotsLeft = Math.Max(0, _config.SlotsPerWeek);
            RefreshPostings();
            RefreshWeeklyOptions();
            if (WeekStarted != null) WeekStarted(_week);
            if (SlotsChanged != null) SlotsChanged(_slotsLeft);
            if (MarketChanged != null) MarketChanged(_market);
            if (PostingsChanged != null) PostingsChanged();
            return ActionResult.Success();
        }

        public ActionResult CloseInterview() { return WrongPhase(); }
        public ActionResult AcceptOffer() { return WrongPhase(); }
        public ActionResult DeclineOffer() { return WrongPhase(); }
    }
}
