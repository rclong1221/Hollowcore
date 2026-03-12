using UnityEngine;
using DIG.Items;

namespace DIG.Loot.Config
{
    /// <summary>
    /// EPIC 16.6: Per-rarity visual configuration for loot drops.
    /// Controls beam, glow, aura prefabs, and label colors.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Loot/Visual Config", order = 5)]
    public class LootVisualConfig : ScriptableObject
    {
        [System.Serializable]
        public struct RarityVisual
        {
            [Tooltip("Color for floating label text.")]
            public Color LabelColor;

            [Tooltip("Vertical beam prefab (Rare+).")]
            public GameObject BeamPrefab;

            [Tooltip("Pulsing glow prefab (Epic+).")]
            public GameObject GlowPrefab;

            [Tooltip("Ground aura prefab (Legendary+).")]
            public GameObject AuraPrefab;
        }

        [Header("Rarity Visuals")]
        public RarityVisual Common = new() { LabelColor = new Color(0.9f, 0.9f, 0.9f) };
        public RarityVisual Uncommon = new() { LabelColor = new Color(0.12f, 1f, 0f) };
        public RarityVisual Rare = new() { LabelColor = new Color(0f, 0.44f, 0.87f) };
        public RarityVisual Epic = new() { LabelColor = new Color(0.64f, 0.21f, 0.93f) };
        public RarityVisual Legendary = new() { LabelColor = new Color(1f, 0.5f, 0f) };
        public RarityVisual Unique = new() { LabelColor = new Color(1f, 0.84f, 0f) };

        public RarityVisual GetVisual(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => Common,
                ItemRarity.Uncommon => Uncommon,
                ItemRarity.Rare => Rare,
                ItemRarity.Epic => Epic,
                ItemRarity.Legendary => Legendary,
                ItemRarity.Unique => Unique,
                _ => Common
            };
        }
    }
}
