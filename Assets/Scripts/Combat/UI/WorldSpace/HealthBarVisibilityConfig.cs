using System;
using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Primary visibility mode - determines the core condition for showing health bars.
    /// Only ONE primary mode can be active at a time.
    /// </summary>
    public enum HealthBarVisibilityMode
    {
        /// <summary>Always show health bars for all valid entities.</summary>
        Always = 0,
        
        /// <summary>Never show health bars (hardcore/immersive mode).</summary>
        Never = 1,
        
        /// <summary>Show when CurrentHP less than MaxHP.</summary>
        WhenDamaged = 2,
        
        /// <summary>Show when damaged, hide after timeout with no new damage.</summary>
        WhenDamagedWithTimeout = 3,
        
        /// <summary>Show when entity is in combat state (has CombatState component or similar).</summary>
        WhenInCombat = 4,
        
        /// <summary>Show during combat, hide after timeout when combat ends.</summary>
        WhenInCombatWithTimeout = 5,
        
        /// <summary>Show only for the currently targeted entity.</summary>
        WhenTargeted = 6,
        
        /// <summary>Target always shows, others show when damaged.</summary>
        WhenTargetedOrDamaged = 7,
        
        /// <summary>Show when within proximity distance of player.</summary>
        WhenInProximity = 8,
        
        /// <summary>Show when in proximity AND damaged.</summary>
        WhenInProximityAndDamaged = 9,
        
        /// <summary>Show when player has line of sight to entity.</summary>
        WhenInLineOfSight = 10,
        
        /// <summary>Show when mouse hovers over entity (or look-at on gamepad).</summary>
        WhenHovered = 11,
        
        /// <summary>Show when entity has aggro/threat on player.</summary>
        WhenAggroed = 12,
        
        /// <summary>Show only if local player dealt damage to this entity.</summary>
        WhenPlayerDealtDamage = 13,
        
        /// <summary>Show if player dealt damage, with timeout.</summary>
        WhenPlayerDealtDamageWithTimeout = 14,
        
        /// <summary>Show only when HP falls below threshold percentage.</summary>
        WhenBelowHealthThreshold = 15,
        
        /// <summary>Show when aggroed OR when damaged (EPIC 15.19: Recommended default).</summary>
        WhenAggroedOrDamaged = 16,
        
        /// <summary>Show when aggroed, or briefly after damage with timeout (hides when aggro lost + timeout expires).</summary>
        WhenAggroedOrDamagedWithTimeout = 17,
        
        /// <summary>Custom - evaluated by delegate/scriptable object.</summary>
        Custom = 99
    }
    
    /// <summary>
    /// Modifier flags that can be combined with any primary mode.
    /// These add additional conditions or behaviors.
    /// </summary>
    [Flags]
    public enum HealthBarVisibilityFlags
    {
        None = 0,
        
        /// <summary>Smooth fade in/out transitions instead of instant show/hide.</summary>
        UseFadeTransitions = 1 << 0,
        
        /// <summary>Delay showing the bar by DelayBeforeShow seconds.</summary>
        UseShowDelay = 1 << 1,
        
        /// <summary>Hide even if conditions are met when HP is full.</summary>
        HideAtFullHealth = 1 << 2,
        
        /// <summary>Only show for entities the player has "discovered" (bestiary system).</summary>
        RequireDiscovered = 1 << 3,
        
        /// <summary>Only show for Boss-tier entities.</summary>
        BossesOnly = 1 << 4,
        
        /// <summary>Only show for Elite/Champion-tier entities.</summary>
        ElitesOnly = 1 << 5,
        
        /// <summary>Only show for named (unique) entities.</summary>
        NamedOnly = 1 << 6,
        
        /// <summary>Only show for hostile entities.</summary>
        HostileOnly = 1 << 7,
        
        /// <summary>Also show for friendly entities.</summary>
        IncludeFriendlies = 1 << 8,
        
        /// <summary>Also show for neutral entities.</summary>
        IncludeNeutrals = 1 << 9,
        
        /// <summary>Requires a scan/analyze ability to have been used on entity.</summary>
        RequireScanned = 1 << 10,
        
        /// <summary>Requires player to have unlocked a specific skill/perk.</summary>
        RequireSkillUnlock = 1 << 11,
        
        /// <summary>Show enemy level alongside health bar.</summary>
        ShowLevel = 1 << 12,
        
        /// <summary>Show enemy name above health bar.</summary>
        ShowName = 1 << 13,
        
        /// <summary>Show status effect icons.</summary>
        ShowStatusEffects = 1 << 14,
        
        /// <summary>Color bar based on threat/difficulty relative to player.</summary>
        ColorByThreatLevel = 1 << 15,
        
        /// <summary>Scale bar size based on entity importance (bigger for bosses).</summary>
        ScaleByImportance = 1 << 16,
        
        /// <summary>Override: Show player's own health bar in world space.</summary>
        ShowPlayerHealthBar = 1 << 17,
        
        /// <summary>Override: Show party member health bars.</summary>
        ShowPartyHealthBars = 1 << 18
    }
    
    /// <summary>
    /// Entity tier classification for filtering which entities show health bars.
    /// </summary>
    public enum EntityTier
    {
        Normal = 0,
        Elite = 1,
        Champion = 2,
        MiniBoss = 3,
        Boss = 4,
        WorldBoss = 5
    }
    
    /// <summary>
    /// Faction relation for filtering.
    /// </summary>
    public enum FactionRelation
    {
        Hostile = 0,
        Neutral = 1,
        Friendly = 2,
        Player = 3,
        Party = 4
    }
    
    /// <summary>
    /// ScriptableObject configuration for health bar visibility.
    /// Can be swapped at runtime for different game modes or player preferences.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthBarVisibilityConfig", menuName = "DIG/Combat/Health Bar Visibility Config")]
    public class HealthBarVisibilityConfig : ScriptableObject
    {
        [Header("Primary Visibility Mode")]
        [Tooltip("The main condition that determines when health bars appear.")]
        public HealthBarVisibilityMode primaryMode = HealthBarVisibilityMode.WhenAggroedOrDamagedWithTimeout; // EPIC 15.19: With timeout so bars hide
        
        [Header("Modifier Flags")]
        [Tooltip("Additional conditions and behaviors to apply.")]
        public HealthBarVisibilityFlags flags = HealthBarVisibilityFlags.UseFadeTransitions | 
                                                  HealthBarVisibilityFlags.HostileOnly;
        
        [Header("Timing Settings")]
        [Tooltip("How long to show health bar after last damage (for timeout modes).")]
        [Range(0.5f, 30f)]
        public float hideAfterSeconds = 5f;
        
        [Tooltip("Delay before showing health bar (when UseShowDelay is enabled).")]
        [Range(0f, 2f)]
        public float delayBeforeShow = 0f;
        
        [Tooltip("Fade in duration in seconds.")]
        [Range(0f, 1f)]
        public float fadeInDuration = 0.2f;
        
        [Tooltip("Fade out duration in seconds.")]
        [Range(0f, 2f)]
        public float fadeOutDuration = 0.5f;
        
        [Header("Distance Settings")]
        [Tooltip("Maximum distance for culling health bars entirely (camera culling). Default 50m.")]
        [Range(10f, 200f)]
        public float maxShowDistance = 50f;
        
        [Tooltip("Maximum distance to show health bars (for proximity modes).")]
        [Range(1f, 100f)]
        public float proximityDistance = 30f;
        
        [Tooltip("Distance at which bars start to fade (should be less than proximityDistance).")]
        [Range(1f, 100f)]
        public float fadeStartDistance = 25f;
        
        [Tooltip("Position match tolerance for target lock entity lookup (meters).")]
        [Range(0.5f, 5f)]
        public float positionMatchTolerance = 2f;
        
        [Header("Threshold Settings")]
        [Tooltip("Health percentage threshold (for WhenBelowHealthThreshold mode). 0.5 = 50%.")]
        [Range(0f, 1f)]
        public float healthThreshold = 0.5f;
        
        [Header("Tier Filtering")]
        [Tooltip("Minimum entity tier required to show health bar.")]
        public EntityTier minimumTier = EntityTier.Normal;
        
        [Header("Visual Scaling")]
        [Tooltip("Base scale for Normal tier health bars.")]
        public float normalScale = 1f;
        
        [Tooltip("Scale multiplier for Elite tier.")]
        public float eliteScaleMultiplier = 1.2f;
        
        [Tooltip("Scale multiplier for Boss tier.")]
        public float bossScaleMultiplier = 1.5f;
        
        [Header("Advanced")]
        [Tooltip("If true, this config can be overridden by per-entity settings.")]
        public bool allowPerEntityOverride = true;
        
        /// <summary>
        /// Evaluates whether a health bar should be visible for the given state.
        /// This is the main entry point for visibility checks.
        /// </summary>
        public HealthBarVisibilityResult Evaluate(in HealthBarVisibilityContext context)
        {
            var result = new HealthBarVisibilityResult
            {
                ShouldShow = false,
                Alpha = 1f,
                Scale = normalScale
            };
            
            // Check tier filter first (fast rejection)
            if (context.Tier < minimumTier)
                return result;
            
            // Check flags-based filters
            if (!PassesFilterFlags(in context))
                return result;
            
            // Evaluate primary mode
            bool primaryConditionMet = EvaluatePrimaryMode(in context);
            
            if (!primaryConditionMet)
            {
                // Check if we should fade out instead of instant hide
                if ((flags & HealthBarVisibilityFlags.UseFadeTransitions) != 0 && context.CurrentAlpha > 0)
                {
                    result.ShouldShow = true;
                    result.IsFadingOut = true;
                    result.Alpha = 0f; // Target alpha
                }
                return result;
            }
            
            // Apply HideAtFullHealth flag
            if ((flags & HealthBarVisibilityFlags.HideAtFullHealth) != 0 && 
                context.CurrentHP >= context.MaxHP)
            {
                return result;
            }
            
            // Primary condition met - show the bar
            result.ShouldShow = true;
            result.Alpha = CalculateAlpha(in context);
            result.Scale = CalculateScale(in context);
            
            return result;
        }
        
        private bool PassesFilterFlags(in HealthBarVisibilityContext context)
        {
            // Boss-only check
            if ((flags & HealthBarVisibilityFlags.BossesOnly) != 0 && 
                context.Tier < EntityTier.Boss)
                return false;
            
            // Elite-only check
            if ((flags & HealthBarVisibilityFlags.ElitesOnly) != 0 && 
                context.Tier < EntityTier.Elite)
                return false;
            
            // Named-only check
            if ((flags & HealthBarVisibilityFlags.NamedOnly) != 0 && 
                !context.IsNamed)
                return false;
            
            // Faction checks
            if ((flags & HealthBarVisibilityFlags.HostileOnly) != 0 &&
                context.Relation != FactionRelation.Hostile)
            {
                // Check override flags
                if (context.Relation == FactionRelation.Friendly &&
                    (flags & HealthBarVisibilityFlags.IncludeFriendlies) == 0)
                    return false;
                    
                if (context.Relation == FactionRelation.Neutral &&
                    (flags & HealthBarVisibilityFlags.IncludeNeutrals) == 0)
                    return false;
            }
            
            // Discovery check
            if ((flags & HealthBarVisibilityFlags.RequireDiscovered) != 0 &&
                !context.IsDiscovered)
                return false;
            
            // Scan check
            if ((flags & HealthBarVisibilityFlags.RequireScanned) != 0 &&
                !context.IsScanned)
                return false;
            
            // Skill unlock check
            if ((flags & HealthBarVisibilityFlags.RequireSkillUnlock) != 0 &&
                !context.HasRequiredSkill)
                return false;
            
            return true;
        }
        
        private bool EvaluatePrimaryMode(in HealthBarVisibilityContext context)
        {
            switch (primaryMode)
            {
                case HealthBarVisibilityMode.Always:
                    // Organic visibility: require proximity + line-of-sight
                    // Without this, bars appear on enemies behind walls at max range
                    return context.DistanceToPlayer <= proximityDistance && context.IsInLineOfSight;
                    
                case HealthBarVisibilityMode.Never:
                    return false;
                    
                case HealthBarVisibilityMode.WhenDamaged:
                    // Use small epsilon to avoid floating-point flicker
                    return context.CurrentHP < context.MaxHP - 0.01f;
                    
                case HealthBarVisibilityMode.WhenDamagedWithTimeout:
                    if (context.CurrentHP >= context.MaxHP - 0.01f) return false;
                    return context.TimeSinceLastDamage < hideAfterSeconds;
                    
                case HealthBarVisibilityMode.WhenInCombat:
                    return context.IsInCombat;
                    
                case HealthBarVisibilityMode.WhenInCombatWithTimeout:
                    return context.IsInCombat || context.TimeSinceCombatEnded < hideAfterSeconds;
                    
                case HealthBarVisibilityMode.WhenTargeted:
                    return context.IsTargeted;
                    
                case HealthBarVisibilityMode.WhenTargetedOrDamaged:
                    return context.IsTargeted || context.CurrentHP < context.MaxHP;
                    
                case HealthBarVisibilityMode.WhenInProximity:
                    return context.DistanceToPlayer <= proximityDistance;
                    
                case HealthBarVisibilityMode.WhenInProximityAndDamaged:
                    return context.DistanceToPlayer <= proximityDistance && 
                           context.CurrentHP < context.MaxHP;
                    
                case HealthBarVisibilityMode.WhenInLineOfSight:
                    return context.IsInLineOfSight;
                    
                case HealthBarVisibilityMode.WhenHovered:
                    return context.IsHovered;
                    
                case HealthBarVisibilityMode.WhenAggroed:
                    return context.HasAggroOnPlayer;
                    
                case HealthBarVisibilityMode.WhenAggroedOrDamaged:
                    return context.HasAggroOnPlayer || context.CurrentHP < context.MaxHP;
                    
                case HealthBarVisibilityMode.WhenAggroedOrDamagedWithTimeout:
                    // Show if aggroed, or if damaged within timeout period
                    // This allows bar to hide after aggro is lost + timeout expires
                    if (context.HasAggroOnPlayer) return true;
                    if (context.CurrentHP < context.MaxHP && context.TimeSinceLastDamage < hideAfterSeconds) return true;
                    return false;
                    
                case HealthBarVisibilityMode.WhenPlayerDealtDamage:
                    return context.PlayerDealtDamage;
                    
                case HealthBarVisibilityMode.WhenPlayerDealtDamageWithTimeout:
                    return context.PlayerDealtDamage && 
                           context.TimeSincePlayerDamage < hideAfterSeconds;
                    
                case HealthBarVisibilityMode.WhenBelowHealthThreshold:
                    return (context.CurrentHP / context.MaxHP) <= healthThreshold;
                    
                case HealthBarVisibilityMode.Custom:
                    // Custom evaluation would be handled by derived class or delegate
                    return context.CustomConditionMet;
                    
                default:
                    return false;
            }
        }
        
        private float CalculateAlpha(in HealthBarVisibilityContext context)
        {
            float alpha = 1f;
            
            // Distance-based fade (Always mode + proximity modes)
            if (primaryMode == HealthBarVisibilityMode.Always ||
                primaryMode == HealthBarVisibilityMode.WhenInProximity ||
                primaryMode == HealthBarVisibilityMode.WhenInProximityAndDamaged)
            {
                if (context.DistanceToPlayer > fadeStartDistance)
                {
                    float fadeRange = proximityDistance - fadeStartDistance;
                    float fadeProgress = (context.DistanceToPlayer - fadeStartDistance) / fadeRange;
                    alpha = Mathf.Clamp01(1f - fadeProgress);
                }
            }
            
            // Timeout-based fade for relevant modes
            if (primaryMode == HealthBarVisibilityMode.WhenDamagedWithTimeout ||
                primaryMode == HealthBarVisibilityMode.WhenInCombatWithTimeout ||
                primaryMode == HealthBarVisibilityMode.WhenPlayerDealtDamageWithTimeout)
            {
                float timeRemaining = hideAfterSeconds - context.TimeSinceLastDamage;
                if (timeRemaining < fadeOutDuration)
                {
                    alpha = Mathf.Clamp01(timeRemaining / fadeOutDuration);
                }
            }
            
            return alpha;
        }
        
        private float CalculateScale(in HealthBarVisibilityContext context)
        {
            if ((flags & HealthBarVisibilityFlags.ScaleByImportance) == 0)
                return normalScale;
            
            return context.Tier switch
            {
                EntityTier.Elite or EntityTier.Champion => normalScale * eliteScaleMultiplier,
                EntityTier.MiniBoss or EntityTier.Boss or EntityTier.WorldBoss => normalScale * bossScaleMultiplier,
                _ => normalScale
            };
        }
    }
    
    /// <summary>
    /// All the context data needed to evaluate health bar visibility.
    /// Gathered from various systems and passed to the config for evaluation.
    /// </summary>
    public struct HealthBarVisibilityContext
    {
        // Entity identification
        public Entity Entity;
        public EntityTier Tier;
        public FactionRelation Relation;
        public bool IsNamed;
        
        // Health data
        public float CurrentHP;
        public float MaxHP;
        
        // Timing data
        public float TimeSinceLastDamage;
        public float TimeSinceCombatEnded;
        public float TimeSincePlayerDamage;
        
        // State flags
        public bool IsInCombat;
        public bool IsTargeted;
        public bool IsHovered;
        public bool HasAggroOnPlayer;
        public bool PlayerDealtDamage;
        public bool IsInLineOfSight;
        public bool IsDiscovered;
        public bool IsScanned;
        public bool HasRequiredSkill;
        public bool CustomConditionMet;
        
        // Spatial data
        public float DistanceToPlayer;
        
        // Current visual state (for fade calculations)
        public float CurrentAlpha;
    }
    
    /// <summary>
    /// Result of visibility evaluation.
    /// </summary>
    public struct HealthBarVisibilityResult
    {
        public bool ShouldShow;
        public float Alpha;
        public float Scale;
        public bool IsFadingOut;
    }
}
