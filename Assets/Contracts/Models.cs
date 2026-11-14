using System.Collections.Generic;

namespace JobGame
{
    public sealed class JobPosting
    {
        public string Id;                 // unik per minggu, contoh "wk3-J05"
        public JobTemplate Template;
        public int WeekPosted;
        public bool Applied;              // true setelah TryApply berhasil
    }

    // CV yang disusun pemain. Dibuat dan diubah oleh UI, hanya dibaca oleh logic.
    public sealed class CvDraft
    {
        public List<SkillTag> Skills = new List<SkillTag>();
        public List<ExperienceCard> Experiences = new List<ExperienceCard>();
    }

    public sealed class MatchResult
    {
        public int Score;                                  // 0-100, 0 jika ditolak gate
        public RejectReason Reject;
        public List<SkillTag> MissingMustHave = new List<SkillTag>();
        public List<SkillTag> MatchedNiceToHave = new List<SkillTag>();
        public int ExperienceCredibility;                  // total kartu experience di CV
        public bool IsRejected => Reject != RejectReason.None;
    }

    public interface IMatchScorer
    {
        // Murni: tanpa random, tanpa state. Skill duplikat diabaikan.
        // Batas slot TIDAK dicek di sini.
        MatchResult Evaluate(JobTemplate job, CvDraft cv);
    }

    public struct CardRef
    {
        public CardKind Kind;
        public SkillTag Skill;            // dipakai jika Kind == Skill
        public string ExperienceId;       // dipakai jika Kind == Experience

        public static CardRef ForSkill(SkillTag t)
        { return new CardRef { Kind = CardKind.Skill, Skill = t }; }
        public static CardRef ForExperience(string id)
        { return new CardRef { Kind = CardKind.Experience, ExperienceId = id }; }
    }

    public sealed class MarketEvent
    {
        public MarketKind Kind;
        public string Label;              // banner siap tampil, "" jika Normal
    }

    public sealed class ActionResult
    {
        public bool Ok;
        public ActionError Error;
        public string Message;            // siap tampil, boleh null jika Ok

        public static ActionResult Success()
        { return new ActionResult { Ok = true, Error = ActionError.None }; }
        public static ActionResult Fail(ActionError e, string message)
        { return new ActionResult { Ok = false, Error = e, Message = message }; }
    }

    public sealed class SkillOption
    {
        public SkillTag Tag;
        public int CurrentLevel;                           // 0 = belum punya
        public List<string> UnlocksJobTitles = new List<string>();
        public string HintText;                            // contoh "Membuka: Data Analyst"
    }

    public sealed class ExperienceOption
    {
        public ExperienceCard Card;
        public List<string> UnlocksJobTitles = new List<string>();
        public string HintText;
    }

    // Dibuat dan diubah hanya oleh logic. UI hanya membaca.
    public sealed class ApplicationRecord
    {
        public string Id;
        public string PostingId;
        public string JobTitle;
        public string Company;
        public int SubmittedWeek;
        public int DueWeek;                                // minggu hasil tahap berikut diumumkan
        public ApplicationStage Stage;
        public RejectStage RejectedAt;
        public int MatchScore;                             // dibekukan saat submit
        public List<SkillTag> MissingMustHave = new List<SkillTag>();   // dibekukan saat submit
        public CvDraft CvSnapshot;                         // salinan CV saat submit
        public string StatusText;                          // siap tampil
    }

    public sealed class Offer
    {
        public string ApplicationId;
        public string JobTitle;
        public string Company;
        public int SalaryTier;                             // 1-5
    }
}
