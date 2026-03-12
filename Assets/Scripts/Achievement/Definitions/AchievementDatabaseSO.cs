using System.Collections.Generic;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Central registry of all achievement definitions.
    /// Load from Resources/AchievementDatabase by AchievementBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Achievement/Achievement Database")]
    public class AchievementDatabaseSO : ScriptableObject
    {
        [Tooltip("All achievement definitions")]
        public List<AchievementDefinitionSO> Achievements = new();

        [Tooltip("Run validator checks in build pipeline")]
        public bool ValidateOnBuild = true;
    }
}
