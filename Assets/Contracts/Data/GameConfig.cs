using UnityEngine;

namespace JobGame
{
    public class GameConfig : ScriptableObject
    {
        // Dibaca UI (read-only bagi B):
        public int TotalWeeks = 12;
        public int SlotsPerWeek = 3;
        public int PostingsPerWeek = 3;
        public int CvSkillSlots = 4;
        public int CvExperienceSlots = 2;
        public int MaxSkillLevel = 5;
        public int ScreeningLagWeeks = 2;

        // Parameter balancing lain (biaya aksi, regen, bobot, pasar) milik A.
        // A boleh menambah field di bawah ini tanpa memberi tahu B.
    }
}
