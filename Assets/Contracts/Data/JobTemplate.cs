using UnityEngine;

namespace JobGame
{
    public class JobTemplate : ScriptableObject
    {
        public string Id;                 // unik, contoh "J05"
        public string Title;
        public string Company;            // fiktif
        public SkillTag[] MustHave;       // wajib; kurang satu = auto-reject
        public SkillTag[] NiceToHave;     // bonus skor
        public int MinExperience;         // total kredibilitas kartu experience di CV
        public int SalaryTier;            // 1-5, menentukan skor akhir
        public int InterviewDifficulty;   // 1-3
        public int FinalLagWeeks = 1;     // jeda dari lulus interview ke hasil tahap akhir
        public string Twist;              // teks tambahan di posting, boleh kosong
    }
}
