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

        // Parameter balancing milik A.
        public float StartingEnergy = 80f;
        public float StartingMental = 70f;
        public float StartingRunway = 10f;
        public float SkillUpgradeEnergyCost = 10f;
        public float SkillUpgradeMentalCost = 5f;
        public float ExperienceUpgradeEnergyCost = 8f;
        public float ExperienceUpgradeMentalCost = 3f;
        public float ExperienceUpgradeRunwayGain = 0.5f;
        public float RestEnergyGain = 20f;
        public float RestMentalGain = 10f;
        public float WeeklyEnergyRegen = 20f;
        public float WeeklyMentalCost = 10f;
        public float WeeklyRunwayBurn = 1f;
    }
}
