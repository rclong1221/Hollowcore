using UnityEngine;
using System.Collections.Generic;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// The "Brain" of a weapon. Defines its behavior, stats, and combos.
    /// Acts as the single source of truth for generating Server logic entities.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponConfig", menuName = "DIG/Items/Weapon Config")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Identity")]
        public string WeaponName;
        public int ItemID; // Maps to global ID

        [Header("Stats")]
        public float BaseDamage = 10f;
        public float AttackRate = 1.0f; // Attacks per second (for guns) or Speed Mult (for melee)

        [Header("Melee Configuration")]
        [Tooltip("List of steps in the combo chain. Step 0 is the first hit.")]
        public List<ComboStepDefinition> ComboChain = new List<ComboStepDefinition>();

        [Header("Range Configuration")]
        public float MaxRange = 50f;
        public float ReloadTime = 2.0f;
        public int ClipSize = 30;

        // Validation helper
        public bool IsValid()
        {
            return ItemID > 0 && !string.IsNullOrEmpty(WeaponName);
        }
    }
}
