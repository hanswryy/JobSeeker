using System.Collections.Generic;

namespace JobGame
{
    public sealed class CausalLink
    {
        public int Week;
        public ChainKind Kind;            // untuk memilih ikon
        public string Text;               // siap tampil, maksimal sekitar 90 karakter
    }

    public sealed class RunSummary
    {
        public RunOutcome Outcome;
        public string Headline;           // contoh "Offer: Data Analyst di Angka Mas"
        public int WeeksPlayed;
        public int FinalScore;            // 0 jika kalah
        public Offer AcceptedOffer;       // null jika kalah
        public int ApplicationsSent;
        public int ScreeningsPassed;
        public int InterviewsPlayed;

        // Urut naik menurut Week. Menang: minimal 3 entri. Kalah: minimal 1 entri (Kind=Failure).
        public List<CausalLink> Chain = new List<CausalLink>();

        // Tag yang paling sering kurang pada lamaran yang ditolak (maksimal 3, terbanyak dulu).
        public List<SkillTag> MostMissedTags = new List<SkillTag>();
    }
}
