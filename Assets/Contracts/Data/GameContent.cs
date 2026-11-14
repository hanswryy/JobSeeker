using System.Collections.Generic;
using UnityEngine;

namespace JobGame
{
    // Satu asset induk: Assets/Content/GameContent.asset
    public class GameContent : ScriptableObject
    {
        public List<JobTemplate> Jobs = new List<JobTemplate>();
        public List<ExperienceCard> Experiences = new List<ExperienceCard>();
    }
}
