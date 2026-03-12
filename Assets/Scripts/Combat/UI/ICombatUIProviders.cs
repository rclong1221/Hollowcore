using Unity.Mathematics;
using Unity.Entities;
using DIG.Targeting.Theming;

namespace DIG.Combat.UI
{
    // ==================== ENUMS ====================
    
    /// <summary>
    /// Categories for combat log filtering.
    /// </summary>
    public enum CombatLogCategory
    {
        Damage,
        Healing,
        Status,
        Kill,
        Experience,
        Loot
    }
    
    /// <summary>
    /// Style for floating text display.
    /// </summary>
    public enum FloatingTextStyle
    {
        Normal,
        Important,
        Warning,
        Success,
        Failure
    }
    
    /// <summary>
    /// Combat verb actions for floating text.
    /// </summary>
    public enum CombatVerb
    {
        Parry,
        Counter,
        PerfectBlock,
        Finisher,
        Combo,
        Immune,
        Resist,
        Absorb,
        Evade,
        Riposte
    }
    
    /// <summary>
    /// Status effect types for buffs/debuffs.
    /// </summary>
    public enum StatusEffectType : byte
    {
        None = 0,
        
        // Damage Over Time
        Burn = 1,
        Bleed = 2,
        Poison = 3,
        Frostbite = 4,
        
        // Crowd Control
        Stun = 10,
        Freeze = 11,
        Slow = 12,
        Root = 13,
        Silence = 14,
        Blind = 15,
        
        // Buffs
        Haste = 20,
        Shield = 21,
        Regen = 22,
        Strength = 23,
        Armor = 24,
        Invisibility = 25,
        
        // Debuffs
        Weakness = 30,
        Vulnerable = 31,
        Exposed = 32,
        Marked = 33,
        Fear = 34
    }
    
    /// <summary>
    /// Kill types for kill feed.
    /// </summary>
    public enum KillType : byte
    {
        Normal,
        Headshot,
        Melee,
        Explosive,
        Environmental,
        Execution,
        Combo
    }
    
    // ==================== INTERFACES ====================
    
    /// <summary>
    /// Interface for damage number display systems.
    /// Implement this to use any UI system (Asset Store, TextMeshPro, custom, etc.).
    /// EPIC 15.22: Extended with ResultFlags for contextual combat feedback.
    /// </summary>
    public interface IDamageNumberProvider
    {
        /// <summary>
        /// Display a damage number at the specified world position.
        /// </summary>
        void ShowDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType);

        /// <summary>
        /// EPIC 15.22: Display a damage number with contextual flags (Headshot, Backstab, etc.).
        /// Default implementation forwards to the base overload for backwards compatibility.
        /// </summary>
        void ShowDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType, ResultFlags flags)
        {
            ShowDamageNumber(damage, worldPosition, hitType, damageType);
        }

        /// <summary>
        /// Display a "MISS" indicator at the specified world position.
        /// </summary>
        void ShowMiss(float3 worldPosition);

        /// <summary>
        /// EPIC 15.22: Display defensive feedback text (BLOCKED, PARRIED, IMMUNE).
        /// Default implementation forwards to ShowDamageNumber with zero damage.
        /// </summary>
        void ShowDefensiveText(float3 worldPosition, HitType hitType, float mitigatedAmount)
        {
            ShowDamageNumber(mitigatedAmount, worldPosition, hitType, DamageType.Physical);
        }

        /// <summary>
        /// Display healing numbers (optional, can be empty implementation).
        /// </summary>
        void ShowHealNumber(float amount, float3 worldPosition);
    }
    
    /// <summary>
    /// EPIC 15.9: Interface for floating combat text (status effects, combat verbs).
    /// Separate from damage numbers for different visual treatment.
    /// </summary>
    public interface IFloatingTextProvider
    {
        /// <summary>
        /// Show arbitrary text at world position.
        /// </summary>
        void ShowText(string text, float3 worldPosition, FloatingTextStyle style);
        
        /// <summary>
        /// Show status effect application text.
        /// </summary>
        void ShowStatusApplied(StatusEffectType status, float3 worldPosition);
        
        /// <summary>
        /// Show combat verb (Parry, Counter, etc).
        /// </summary>
        void ShowCombatVerb(CombatVerb verb, float3 worldPosition);
    }
    
    /// <summary>
    /// EPIC 15.9: Interface for enemy health bar management.
    /// Updated in EPIC 15.19 to include aggro state.
    /// </summary>
    public interface IEnemyHealthBarProvider
    {
        /// <summary>
        /// Show/update health bar for an entity.
        /// </summary>
        void ShowHealthBar(Entity entity, float3 position, float currentHealth, float maxHealth, string name = null,
            bool isInCombat = true, float timeSinceCombatEnded = 0f, bool isInLineOfSight = true, bool hasAggroOnPlayer = false);
        
        /// <summary>
        /// Hide health bar for an entity.
        /// </summary>
        void HideHealthBar(Entity entity);
        
        /// <summary>
        /// Force hide all health bars.
        /// </summary>
        void HideAll();
    }
    
    /// <summary>
    /// EPIC 15.9: Interface for kill feed display.
    /// </summary>
    public interface IKillFeedProvider
    {
        /// <summary>
        /// Add a kill entry to the feed.
        /// </summary>
        void AddKill(KillFeedEntry entry);
        
        /// <summary>
        /// Clear all kill entries.
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// Interface for combat hit feedback (screen shake, flash, etc.).
    /// Implement this to use any feedback system.
    /// </summary>
    public interface ICombatFeedbackProvider
    {
        /// <summary>
        /// Trigger feedback when the player deals damage.
        /// </summary>
        void OnPlayerDealtDamage(float damage, HitType hitType, DamageType damageType);
        
        /// <summary>
        /// Trigger feedback when the player takes damage.
        /// </summary>
        void OnPlayerTookDamage(float damage, HitType hitType, DamageType damageType);
        
        /// <summary>
        /// Trigger feedback when an entity dies.
        /// </summary>
        void OnEntityKilled(bool wasPlayer, DamageType killingBlowType);
        
        /// <summary>
        /// Trigger hit stop/freeze frame effect.
        /// </summary>
        /// <param name="duration">Duration in seconds</param>
        void TriggerHitStop(float duration);
        
        /// <summary>
        /// Trigger camera shake.
        /// </summary>
        /// <param name="intensity">Shake intensity (0-1)</param>
        /// <param name="duration">Duration in seconds</param>
        void TriggerCameraShake(float intensity, float duration);
    }
    
    /// <summary>
    /// Interface for combat log/history display.
    /// Implement this to use any combat log UI.
    /// </summary>
    public interface ICombatLogProvider
    {
        /// <summary>
        /// Log a combat event.
        /// </summary>
        void LogCombatEvent(CombatLogEntry entry);
        
        /// <summary>
        /// Clear all log entries.
        /// </summary>
        void ClearLog();
    }
    
    // ==================== DATA STRUCTURES ====================
    
    /// <summary>
    /// Data structure for combat log entries.
    /// </summary>
    public struct CombatLogEntry
    {
        public string AttackerName;
        public string TargetName;
        public float Damage;
        public HitType HitType;
        public DamageType DamageType;
        public bool TargetKilled;
        public float Timestamp;
    }
    
    /// <summary>
    /// EPIC 15.9: Data structure for kill feed entries.
    /// </summary>
    public struct KillFeedEntry
    {
        public string KillerName;
        public string VictimName;
        public KillType Type;
        public string WeaponName;
        public bool IsLocalPlayerKiller;
        public bool IsLocalPlayerVictim;
        public float Timestamp;
    }
    
    /// <summary>
    /// EPIC 15.9: Data for active status effect display.
    /// </summary>
    public struct ActiveStatusEffect
    {
        public StatusEffectType Type;
        public float RemainingDuration;
        public float TotalDuration;
        public int Stacks;
        public bool IsDebuff;
    }
}
