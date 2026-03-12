using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Defines a difficulty preset with enemy/loot/XP scaling factors.
    /// Place in Resources/Difficulties/ folder.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Lobby/Difficulty Definition")]
    public class DifficultyDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int DifficultyId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public Color Color = Color.white;

        [Header("Enemy Scaling")]
        [Range(0.5f, 5f)] public float EnemyHealthScale = 1f;
        [Range(0.5f, 5f)] public float EnemyDamageScale = 1f;
        [Range(0.5f, 3f)] public float EnemySpawnRateScale = 1f;

        [Header("Loot Scaling")]
        [Range(0.5f, 3f)] public float LootQuantityScale = 1f;
        [Range(0f, 2f)] public float LootQualityBonus = 0f;

        [Header("Reward Scaling")]
        [Range(0.5f, 5f)] public float XPMultiplier = 1f;
        [Range(0.5f, 5f)] public float CurrencyMultiplier = 1f;

        [Header("Unlock")]
        [Tooltip("Minimum player level to select this difficulty (0 = always available).")]
        public int UnlockRequirement;
    }
}
