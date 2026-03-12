using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Authoring component for AttackStats.
    /// Add to player/enemy prefabs to enable stat-based combat.
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackStatsAuthoring : MonoBehaviour
    {
        [Header("Offensive Stats")]
        [Tooltip("Flat damage bonus added to weapon damage")]
        public float AttackPower = 0f;
        
        [Tooltip("Spell/magic power for magical attacks")]
        public float SpellPower = 0f;
        
        [Tooltip("Critical hit chance (0-1)")]
        [Range(0f, 1f)]
        public float CritChance = 0.05f;
        
        [Tooltip("Critical hit damage multiplier")]
        [Min(1f)]
        public float CritMultiplier = 1.5f;
        
        [Tooltip("Hit accuracy for stat-roll systems")]
        [Min(0f)]
        public float Accuracy = 1f;
        
        public class Baker : Baker<AttackStatsAuthoring>
        {
            public override void Bake(AttackStatsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AttackStats
                {
                    AttackPower = authoring.AttackPower,
                    SpellPower = authoring.SpellPower,
                    CritChance = authoring.CritChance,
                    CritMultiplier = authoring.CritMultiplier,
                    Accuracy = authoring.Accuracy
                });
            }
        }
    }
    
    /// <summary>
    /// Authoring component for DefenseStats.
    /// Add to player/enemy prefabs to enable stat-based damage reduction.
    /// </summary>
    [DisallowMultipleComponent]
    public class DefenseStatsAuthoring : MonoBehaviour
    {
        [Header("Defensive Stats")]
        [Tooltip("Base defense (damage reduction)")]
        [Min(0f)]
        public float Defense = 0f;
        
        [Tooltip("Armor rating (alternative damage reduction)")]
        [Min(0f)]
        public float Armor = 0f;
        
        [Tooltip("Evasion/dodge chance (0-1)")]
        [Range(0f, 1f)]
        public float Evasion = 0f;
        
        public class Baker : Baker<DefenseStatsAuthoring>
        {
            public override void Bake(DefenseStatsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DefenseStats
                {
                    Defense = authoring.Defense,
                    Armor = authoring.Armor,
                    Evasion = authoring.Evasion
                });
            }
        }
    }
    
    /// <summary>
    /// Authoring component for ElementalResistances.
    /// Add to player/enemy prefabs to enable elemental damage mitigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalResistancesAuthoring : MonoBehaviour
    {
        [Header("Elemental Resistances (0-1)")]
        [Range(0f, 1f)] public float Physical = 0f;
        [Range(0f, 1f)] public float Fire = 0f;
        [Range(0f, 1f)] public float Ice = 0f;
        [Range(0f, 1f)] public float Lightning = 0f;
        [Range(0f, 1f)] public float Poison = 0f;
        [Range(0f, 1f)] public float Holy = 0f;
        [Range(0f, 1f)] public float Shadow = 0f;
        [Range(0f, 1f)] public float Arcane = 0f;
        
        public class Baker : Baker<ElementalResistancesAuthoring>
        {
            public override void Bake(ElementalResistancesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ElementalResistances
                {
                    Physical = authoring.Physical,
                    Fire = authoring.Fire,
                    Ice = authoring.Ice,
                    Lightning = authoring.Lightning,
                    Poison = authoring.Poison,
                    Holy = authoring.Holy,
                    Shadow = authoring.Shadow,
                    Arcane = authoring.Arcane
                });
            }
        }
    }
    
    /// <summary>
    /// Authoring component for CharacterAttributes.
    /// Add to player/enemy prefabs for RPG-style attribute systems.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAttributesAuthoring : MonoBehaviour
    {
        [Header("Primary Attributes")]
        [Tooltip("Strength - affects physical damage")]
        [Min(1)]
        public int Strength = 10;
        
        [Tooltip("Dexterity - affects accuracy, crit")]
        [Min(1)]
        public int Dexterity = 10;
        
        [Tooltip("Intelligence - affects spell power")]
        [Min(1)]
        public int Intelligence = 10;
        
        [Tooltip("Vitality - affects health")]
        [Min(1)]
        public int Vitality = 10;
        
        [Header("Level")]
        [Tooltip("Character level")]
        [Min(1)]
        public int Level = 1;
        
        public class Baker : Baker<CharacterAttributesAuthoring>
        {
            public override void Bake(CharacterAttributesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CharacterAttributes
                {
                    Strength = authoring.Strength,
                    Dexterity = authoring.Dexterity,
                    Intelligence = authoring.Intelligence,
                    Vitality = authoring.Vitality,
                    Level = authoring.Level
                });
            }
        }
    }
}
