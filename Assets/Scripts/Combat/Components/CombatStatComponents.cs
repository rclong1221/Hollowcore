using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Offensive combat statistics for an entity.
    /// Used by stat-based combat resolvers.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct AttackStats : IComponentData
    {
        /// <summary>
        /// Flat damage bonus added to weapon damage.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float AttackPower;
        
        /// <summary>
        /// Spell/magic power for magical attacks.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float SpellPower;
        
        /// <summary>
        /// Critical hit chance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float CritChance;
        
        /// <summary>
        /// Critical hit damage multiplier (e.g., 1.5 = 150% damage).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CritMultiplier;
        
        /// <summary>
        /// Hit accuracy bonus for stat-roll systems.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Accuracy;
        
        /// <summary>
        /// Creates default attack stats.
        /// </summary>
        public static AttackStats Default => new AttackStats
        {
            AttackPower = 0f,
            SpellPower = 0f,
            CritChance = 0.05f,
            CritMultiplier = 1.5f,
            Accuracy = 1f
        };
    }
    
    /// <summary>
    /// Defensive combat statistics for an entity.
    /// Used by stat-based combat resolvers.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DefenseStats : IComponentData
    {
        /// <summary>
        /// Base defense (damage reduction).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Defense;
        
        /// <summary>
        /// Armor rating (alternative damage reduction).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Armor;
        
        /// <summary>
        /// Evasion/dodge chance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Evasion;
        
        /// <summary>
        /// Creates default defense stats.
        /// </summary>
        public static DefenseStats Default => new DefenseStats
        {
            Defense = 0f,
            Armor = 0f,
            Evasion = 0f
        };
    }
    
    /// <summary>
    /// Elemental resistances for an entity.
    /// Separate component to avoid bloating DefenseStats.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ElementalResistances : IComponentData
    {
        /// <summary>
        /// Physical damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Physical;
        
        /// <summary>
        /// Fire damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Fire;
        
        /// <summary>
        /// Ice damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Ice;
        
        /// <summary>
        /// Lightning damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Lightning;
        
        /// <summary>
        /// Poison damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Poison;
        
        /// <summary>
        /// Holy damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Holy;
        
        /// <summary>
        /// Shadow damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Shadow;
        
        /// <summary>
        /// Arcane damage resistance (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float Arcane;
        
        /// <summary>
        /// Get resistance value for a specific damage type.
        /// </summary>
        public float GetResistance(DIG.Targeting.Theming.DamageType damageType)
        {
            return damageType switch
            {
                DIG.Targeting.Theming.DamageType.Physical => Physical,
                DIG.Targeting.Theming.DamageType.Fire => Fire,
                DIG.Targeting.Theming.DamageType.Ice => Ice,
                DIG.Targeting.Theming.DamageType.Lightning => Lightning,
                DIG.Targeting.Theming.DamageType.Poison => Poison,
                DIG.Targeting.Theming.DamageType.Holy => Holy,
                DIG.Targeting.Theming.DamageType.Shadow => Shadow,
                DIG.Targeting.Theming.DamageType.Arcane => Arcane,
                _ => 0f
            };
        }
        
        /// <summary>
        /// Creates default resistances (all zero).
        /// </summary>
        public static ElementalResistances Default => new ElementalResistances();
    }
    
    /// <summary>
    /// Core character attributes that feed into combat stats.
    /// Optional - only needed for games with RPG attribute systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CharacterAttributes : IComponentData
    {
        /// <summary>
        /// Strength attribute (affects physical damage).
        /// </summary>
        [GhostField]
        public int Strength;
        
        /// <summary>
        /// Dexterity attribute (affects accuracy, crit).
        /// </summary>
        [GhostField]
        public int Dexterity;
        
        /// <summary>
        /// Intelligence attribute (affects spell power).
        /// </summary>
        [GhostField]
        public int Intelligence;
        
        /// <summary>
        /// Vitality/Constitution (affects health).
        /// </summary>
        [GhostField]
        public int Vitality;
        
        /// <summary>
        /// Character level.
        /// </summary>
        [GhostField]
        public int Level;
        
        /// <summary>
        /// Creates default attributes for level 1.
        /// </summary>
        public static CharacterAttributes Default => new CharacterAttributes
        {
            Strength = 10,
            Dexterity = 10,
            Intelligence = 10,
            Vitality = 10,
            Level = 1
        };
    }
}
