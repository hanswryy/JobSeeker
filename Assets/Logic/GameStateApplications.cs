using System;
using System.Collections.Generic;
using System.Linq;

namespace JobGame.Logic
{
    sealed class ApplicationProgress
    {
        public bool ScreeningPassed;
        public bool FinalPassed;
        public int InterviewSeed;
        public Dictionary<SkillTag, int> SkillLevels;
        public JobTemplate Job;
    }

    public sealed partial class GameState
    {
        const int InterviewQuestionSeedSalt = 1618033;

        public ActionResult TryApplyInternal(string postingId, CvDraft cv)
        {
            ActionResult phaseResult = RequirePlanning();
            if (phaseResult != null) return phaseResult;
            ActionResult slotResult = RequireSlot();
            if (slotResult != null) return slotResult;

            JobPosting posting = _postings.FirstOrDefault(item => item.Id == postingId);
            if (posting == null)
                return ActionResult.Fail(ActionError.UnknownId, "Posting belum tersedia.");
            if (posting.Applied)
                return ActionResult.Fail(ActionError.AlreadyApplied, "Posting sudah pernah dilamar.");
            if (!IsValidCv(cv))
                return ActionResult.Fail(ActionError.InvalidCv, "CV tidak valid.");

            MatchResult match = PreviewMatch(posting, cv);
            string applicationId = "app-" + (++_nextApplicationNumber);
            ApplicationRecord record = new ApplicationRecord
            {
                Id = applicationId,
                PostingId = posting.Id,
                JobTitle = posting.Template.Title,
                Company = posting.Template.Company,
                SubmittedWeek = _week,
                DueWeek = _week + Math.Max(0, _config.ScreeningLagWeeks),
                Stage = ApplicationStage.Screening,
                RejectedAt = RejectStage.None,
                MatchScore = match.Score,
                MissingMustHave = new List<SkillTag>(match.MissingMustHave),
                CvSnapshot = CloneCv(cv),
                StatusText = "Menunggu hasil screening (minggu " +
                    (_week + Math.Max(0, _config.ScreeningLagWeeks)) + ")."
            };
            posting.Applied = true;
            _applications.Insert(0, record);
            _applicationProgress[record.Id] = new ApplicationProgress
            {
                ScreeningPassed = !match.IsRejected && RollScreening(match.Score),
                FinalPassed = false,
                InterviewSeed = CombineSeed(_seed ^ InterviewQuestionSeedSalt, _nextApplicationNumber),
                SkillLevels = new Dictionary<SkillTag, int>(_skillLevels),
                Job = posting.Template
            };
            AddChain(ChainKind.Apply, "Minggu " + _week + ": melamar " + record.JobTitle + ".");

            float oldEnergy = _energy;
            float oldMental = _mental;
            _energy = ClampResource(_energy - _config.ApplyEnergyCost);
            _mental = ClampResource(_mental - _config.ApplyMentalCost);
            EmitResourceChanged(oldEnergy, _energy, ResourceKind.Energy);
            EmitResourceChanged(oldMental, _mental, ResourceKind.Mental);
            if (PostingsChanged != null) PostingsChanged();
            if (ApplicationUpdated != null) ApplicationUpdated(record);
            ConsumeSlot();
            return ActionResult.Success();
        }

        public ActionResult CloseInterviewInternal()
        {
            if (_phase != GamePhase.AwaitingInterview || _activeInterview == null ||
                !_activeInterview.IsFinished)
                return WrongPhase();

            ApplicationRecord record = _interviewQueue[0];
            _interviewQueue.RemoveAt(0);
            ApplicationProgress progress = _applicationProgress[record.Id];
            if (_activeInterview.Result.Passed)
            {
                record.Stage = ApplicationStage.FinalReview;
                record.DueWeek = _week + Math.Max(0, progress.Job.FinalLagWeeks);
                progress.FinalPassed = _random.NextDouble() < _config.FinalPassChance;
                record.StatusText = "Menunggu hasil tahap akhir (minggu " + record.DueWeek + ").";
                AddChain(ChainKind.Interview, "Minggu " + _week + ": interview " + record.JobTitle + " berhasil.");
            }
            else
            {
                record.Stage = ApplicationStage.Rejected;
                record.RejectedAt = RejectStage.Interview;
                record.StatusText = "Ditolak pada tahap interview.";
                AddChain(ChainKind.Failure, "Minggu " + _week + ": interview " + record.JobTitle + " gagal.");
            }
            _interviewsPlayed++;
            if (ApplicationUpdated != null) ApplicationUpdated(record);
            _activeInterview = null;
            AdvancePipelinePhase();
            return ActionResult.Success();
        }

        public ActionResult DeclineInterviewInternal()
        {
            if (_phase != GamePhase.AwaitingInterview || _activeInterview == null ||
                _activeInterview.IsFinished || _interviewQueue.Count == 0)
                return WrongPhase();

            ApplicationRecord record = _interviewQueue[0];
            _interviewQueue.RemoveAt(0);
            record.Stage = ApplicationStage.Rejected;
            record.RejectedAt = RejectStage.Interview;
            record.StatusText = "Undangan interview ditolak.";
            AddChain(ChainKind.Failure,
                "Minggu " + _week + ": undangan interview " + record.JobTitle + " ditolak.");
            if (ApplicationUpdated != null) ApplicationUpdated(record);
            _activeInterview = null;
            AdvancePipelinePhase();
            return ActionResult.Success();
        }

        bool IsValidCv(CvDraft cv)
        {
            if (cv == null || cv.Skills == null || cv.Experiences == null ||
                cv.Skills.Count > _config.CvSkillSlots ||
                cv.Experiences.Count > _config.CvExperienceSlots ||
                cv.Skills.Distinct().Count() != cv.Skills.Count ||
                cv.Experiences.Where(item => item != null).Select(item => item.Id).Distinct().Count() !=
                    cv.Experiences.Count)
                return false;
            if (cv.Skills.Any(skill => GetSkillLevel(skill) <= 0))
                return false;
            return cv.Experiences.All(card => card != null && _ownedExperiences.Contains(card));
        }

        bool RollScreening(int score)
        {
            double chance = _config.ScreeningBaseChance + score * _config.ScreeningScoreChance;
            return _random.NextDouble() < Math.Max(0f, Math.Min(1f, (float)chance));
        }

        static CvDraft CloneCv(CvDraft cv)
        {
            return new CvDraft
            {
                Skills = new List<SkillTag>(cv.Skills),
                Experiences = new List<ExperienceCard>(cv.Experiences)
            };
        }

        void ProcessDueApplications()
        {
            _interviewQueue.Clear();
            _offerQueue.Clear();
            foreach (ApplicationRecord record in _applications
                .Where(item => item.Stage == ApplicationStage.Screening ||
                    item.Stage == ApplicationStage.FinalReview)
                .Where(item => item.DueWeek <= _week)
                .OrderBy(item => item.SubmittedWeek).ThenBy(item => item.Id)
                .ToList())
            {
                ApplicationProgress progress = _applicationProgress[record.Id];
                if (record.Stage == ApplicationStage.Screening)
                {
                    if (progress.ScreeningPassed)
                    {
                        record.Stage = ApplicationStage.InterviewReady;
                        record.StatusText = "Interview siap dimainkan.";
                        _interviewQueue.Add(record);
                        AddChain(ChainKind.Interview, "Minggu " + _week + ": screening " + record.JobTitle + " lolos.");
                    }
                    else
                    {
                        record.Stage = ApplicationStage.Rejected;
                        record.RejectedAt = RejectStage.Screening;
                        record.StatusText = GetScreeningRejectionText(record);
                        AddChain(ChainKind.Failure, "Minggu " + _week + ": screening " + record.JobTitle + " gagal.");
                    }
                    if (ApplicationUpdated != null) ApplicationUpdated(record);
                }
                else if (record.Stage == ApplicationStage.FinalReview)
                {
                    if (progress.FinalPassed)
                    {
                        record.Stage = ApplicationStage.OfferReady;
                        record.StatusText = "Offer siap diputuskan.";
                        _offerQueue.Add(record);
                        AddChain(ChainKind.Offer, "Minggu " + _week + ": offer " + record.JobTitle + " tersedia.");
                    }
                    else
                    {
                        record.Stage = ApplicationStage.Rejected;
                        record.RejectedAt = RejectStage.Final;
                        record.StatusText = "Ditolak pada tahap akhir.";
                        AddChain(ChainKind.Failure, "Minggu " + _week + ": tahap akhir " + record.JobTitle + " gagal.");
                    }
                    if (ApplicationUpdated != null) ApplicationUpdated(record);
                }
            }
        }

        void AdvancePipelinePhase()
        {
            if (_interviewQueue.Count > 0)
            {
                bool phaseChanged = _phase != GamePhase.AwaitingInterview;
                _phase = GamePhase.AwaitingInterview;
                _activeInterview = CreateInterview(_interviewQueue[0]);
                if (phaseChanged && PhaseChanged != null) PhaseChanged(_phase);
                if (InterviewStarted != null) InterviewStarted(_activeInterview);
                return;
            }
            if (_offerQueue.Count > 0)
            {
                bool phaseChanged = _phase != GamePhase.AwaitingOfferDecision;
                _phase = GamePhase.AwaitingOfferDecision;
                _pendingOffer = CreateOffer(_offerQueue[0]);
                if (phaseChanged && PhaseChanged != null) PhaseChanged(_phase);
                if (OfferReceived != null) OfferReceived(_pendingOffer);
                return;
            }
            _pendingOffer = null;
            if (_phase != GamePhase.Planning)
            {
                _phase = GamePhase.Planning;
                if (PhaseChanged != null) PhaseChanged(_phase);
            }
        }

        InterviewSession CreateInterview(ApplicationRecord record)
        {
            ApplicationProgress progress = _applicationProgress[record.Id];
            return new InterviewSession(progress.Job, record.CvSnapshot,
                progress.SkillLevels, progress.InterviewSeed);
        }

        Offer CreateOffer(ApplicationRecord record)
        {
            return new Offer
            {
                ApplicationId = record.Id,
                JobTitle = record.JobTitle,
                Company = record.Company,
                SalaryTier = _applicationProgress[record.Id].Job.SalaryTier
            };
        }

        static string GetScreeningRejectionText(ApplicationRecord record)
        {
            return record.MissingMustHave.Count > 0
                ? "Ditolak: kurang " + record.MissingMustHave[0].DisplayName() + "."
                : "Ditolak pada tahap screening.";
        }

        void AddChain(ChainKind kind, string text)
        {
            _chain.Add(new CausalLink
            {
                Week = _week,
                Kind = kind,
                Text = text.Length <= 90 ? text : text.Substring(0, 90)
            });
        }
    }

}
