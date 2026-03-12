using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting.Theming
{
    /// <summary>
    /// Runtime context passed to indicators for theming decisions.
    /// Aggregates all possible theming triggers.
    /// </summary>
    public struct IndicatorThemeContext
    {
        // ========== TARGET-BASED ==========
        public Entity TargetEntity;
        public TargetFaction TargetFaction;      // Enemy, Ally, Neutral
        public TargetCategory TargetCategory;    // Normal, Elite, Boss
        public byte TargetRace;                   // Game-specific race ID
        public float TargetHealthPercent;         // 0-1
        
        // ========== COMBAT-BASED ==========
        public DamageType LastDamageType;         // Fire, Ice, Lightning, etc.
        public HitType LastHitType;               // Miss, Hit, Crit
        public float ThreatLevel;                 // 0 = no threat, 1 = max
        
        // ========== PLAYER-BASED ==========
        public byte PlayerClass;                  // Game-specific class ID
        public byte PlayerRace;                   // Game-specific race ID
        public byte WeaponCategory;               // From WeaponCategoryDefinition
        
        // ========== GAME STATE ==========
        public bool IsPvPMode;
        public bool IsBossFight;
        public bool IsStealthMode;
        public bool FriendlyFireEnabled;
        
        // ========== ACCESSIBILITY ==========
        public bool HighContrastMode;
        public byte ColorblindMode;               // 0=off, 1=protanopia, 2=deuteranopia, 3=tritanopia
        public float SizeScale;                   // 1.0 = normal
        
        // ========== TARGETING STATE ==========
        public TargetingMode Mode;
        public bool HasValidTarget;
        public bool IsLocked;
        public float3 TargetPosition;
        
        public static IndicatorThemeContext Default => new IndicatorThemeContext
        {
            TargetFaction = TargetFaction.Neutral,
            TargetCategory = TargetCategory.Normal,
            SizeScale = 1f
        };
    }
    
    /// <summary>
    /// Target faction for theming.
    /// </summary>
    public enum TargetFaction : byte
    {
        Neutral = 0,
        Enemy = 1,
        Ally = 2,
        Hostile = 3  // PvP opponent
    }
    
    /// <summary>
    /// Target category for indicator styling.
    /// </summary>
    public enum TargetCategory : byte
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
        Miniboss = 3,
        Destructible = 4,
        Interactable = 5
    }
    
    /// <summary>
    /// Damage type for combat-based theming.
    /// </summary>
    public enum DamageType : byte
    {
        Physical = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Poison = 4,
        Holy = 5,
        Shadow = 6,
        Arcane = 7
    }
    
    /// <summary>
    /// Hit type for combat-based theming.
    /// EPIC 15.22: Extended with defensive feedback and execute types.
    /// </summary>
    public enum HitType : byte
    {
        None = 0,
        Miss = 1,
        Graze = 2,
        Hit = 3,
        Critical = 4,
        Blocked = 5,
        Parried = 6,
        Immune = 7,
        Execute = 8
    }

    /// <summary>
    /// EPIC 15.22: Contextual flags for combat result feedback.
    /// Multiple flags can be combined (e.g. Headshot + Weakness).
    /// </summary>
    [System.Flags]
    public enum ResultFlags : byte
    {
        None = 0,
        Headshot = 1 << 0,
        Backstab = 1 << 1,
        Weakness = 1 << 2,
        Resistance = 1 << 3,
        PoiseBreak = 1 << 4
    }
}
