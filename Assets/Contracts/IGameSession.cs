using System;
using System.Collections.Generic;

namespace JobGame
{
    public interface IGameFactory
    {
        // Sesi baru = objek baru. Seed sama + perintah sama = hasil identik.
        IGameSession NewRun(int seed);
    }

    public interface IGameSession
    {
        // ---------- State (hanya baca; sumber kebenaran) ----------
        GamePhase Phase { get; }
        int Week { get; }                                  // 1..TotalWeeks
        int TotalWeeks { get; }
        int SlotsLeft { get; }
        float Energy { get; }                              // 0-100
        float Mental { get; }                              // 0-100
        float Runway { get; }                              // sisa "minggu uang"
        MarketEvent Market { get; }
        IReadOnlyList<JobPosting> Postings { get; }        // posting minggu ini
        IReadOnlyList<SkillTag> OwnedSkills { get; }
        IReadOnlyList<ExperienceCard> OwnedExperiences { get; }
        int GetSkillLevel(SkillTag tag);                   // 0 = belum punya
        IReadOnlyList<ApplicationRecord> Applications { get; }   // terbaru di indeks 0
        IInterviewSession ActiveInterview { get; }         // non-null saat AwaitingInterview
        Offer PendingOffer { get; }                        // non-null saat AwaitingOfferDecision
        RunSummary Summary { get; }                        // non-null setelah Ended
        GameConfig Config { get; }

        // ---------- Query (tidak mengubah state dan tidak memakai RNG) ----------
        MatchResult PreviewMatch(JobPosting posting, CvDraft cv);
        IReadOnlyList<SkillOption> GetSkillOptions();              // 3 opsi per minggu
        IReadOnlyList<ExperienceOption> GetExperienceOptions();    // 0-3 opsi

        // ---------- Command (hanya fase Planning, kecuali dinyatakan lain) ----------
        ActionResult TryApply(string postingId, CvDraft cv);       // 1 slot
        ActionResult TryUpgradeSkill(SkillTag tag);                // 1 slot
        ActionResult TryUpgradeExperience(string experienceId);    // 1 slot
        ActionResult TryRest();                                    // 1 slot
        ActionResult EndWeek();                                    // slot sisa hangus
        ActionResult CloseInterview();                             // fase AwaitingInterview
        ActionResult AcceptOffer();                                // fase AwaitingOfferDecision
        ActionResult DeclineOffer();                               // fase AwaitingOfferDecision

        // ---------- Event (sinkron; lihat aturan di dokumen) ----------
        event Action<int> WeekStarted;                             // nomor minggu baru
        event Action<int> SlotsChanged;                            // sisa slot
        event Action<ResourceKind, float, float> ResourceChanged;  // jenis, lama, baru
        event Action<MarketEvent> MarketChanged;
        event Action PostingsChanged;
        event Action HoldingsChanged;                              // skill atau experience berubah
        event Action<ApplicationRecord> ApplicationUpdated;
        event Action<GamePhase> PhaseChanged;
        event Action<IInterviewSession> InterviewStarted;
        event Action<Offer> OfferReceived;
        event Action<RunSummary> RunEnded;
    }
}
