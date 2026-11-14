using UnityEngine;

namespace JobGame
{
    // Data milik A (task A1). B hanya membaca.
    public class ExperienceCard : ScriptableObject
    {
        public string Id;                 // unik, contoh "exp_magang"
        public string DisplayName;
        public string Description;
        public int Credibility;           // 1-3
        public SkillTag[] ProvesTags;     // tag yang dibuktikan kartu ini (dipakai di interview)
        public bool OwnedAtStart;
    }
}
