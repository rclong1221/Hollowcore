using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for Opsive Agility Pack abilities.
    /// Adds DodgeState, RollState, VaultState, and related components to the player entity.
    ///
    /// Note: This is for Opsive animation integration. The actual dodge/roll gameplay
    /// may be handled by separate systems (DodgeRollSystem, DodgeDiveSystem).
    /// Bridge systems sync gameplay state to these animation states.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Agility Authoring")]
    public class AgilityAuthoring : MonoBehaviour
    {
        [Header("Ability Toggles")]
        [Tooltip("Enable dodge ability (Opsive AbilityIndex 101)")]
        public bool CanDodge = true;

        [Tooltip("Enable roll ability (Opsive AbilityIndex 102)")]
        public bool CanRoll = true;

        [Tooltip("Enable vault ability (Opsive AbilityIndex 105)")]
        public bool CanVault = true;

        [Tooltip("Enable crawl ability (Opsive AbilityIndex 103)")]
        public bool CanCrawl = true;

        [Header("Dodge Settings")]
        [Tooltip("Duration of dodge animation in seconds")]
        public float DodgeDuration = 0.5f;

        [Tooltip("Cooldown between dodges in seconds")]
        public float DodgeCooldown = 0.3f;

        [Header("Roll Settings")]
        [Tooltip("Duration of roll animation in seconds")]
        public float RollDuration = 0.8f;

        [Tooltip("Cooldown between rolls in seconds")]
        public float RollCooldown = 0.2f;

        [Header("Vault Settings")]
        [Tooltip("Minimum obstacle height for vault (meters)")]
        public float MinVaultHeight = 0.3f;

        [Tooltip("Maximum obstacle height for vault (meters)")]
        public float MaxVaultHeight = 1.2f;

        [Tooltip("Duration of vault animation in seconds")]
        public float VaultDuration = 0.6f;

        class Baker : Baker<AgilityAuthoring>
        {
            public override void Bake(AgilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add configuration component
                AddComponent(entity, new AgilityConfig
                {
                    CanDodge = authoring.CanDodge,
                    CanRoll = authoring.CanRoll,
                    CanVault = authoring.CanVault,
                    CanCrawl = authoring.CanCrawl,
                    MinVaultHeight = authoring.MinVaultHeight,
                    MaxVaultHeight = authoring.MaxVaultHeight
                });

                // Add tag for queries
                AddComponent<HasAgilityAbilities>(entity);

                // Add ability state components (for GhostField replication)
                if (authoring.CanDodge)
                {
                    AddComponent(entity, new DodgeState
                    {
                        IsDodging = false,
                        Direction = 0,
                        TimeRemaining = 0f,
                        CooldownRemaining = 0f,
                        DodgeDuration = authoring.DodgeDuration,
                        DodgeCooldown = authoring.DodgeCooldown
                    });
                }

                if (authoring.CanRoll)
                {
                    AddComponent(entity, new RollState
                    {
                        IsRolling = false,
                        RollType = 0,
                        TimeRemaining = 0f,
                        CooldownRemaining = 0f,
                        RollDuration = authoring.RollDuration,
                        RollCooldown = authoring.RollCooldown
                    });
                }

                if (authoring.CanVault)
                {
                    AddComponent(entity, new VaultState
                    {
                        IsVaulting = false,
                        StartVelocity = 0f,
                        VaultHeight = 0f,
                        TimeRemaining = 0f,
                        VaultDuration = authoring.VaultDuration
                    });
                }

                if (authoring.CanCrawl)
                {
                    AddComponent(entity, new CrawlState
                    {
                        IsCrawling = false,
                        CrawlSubState = 0
                    });
                }

                // Add animation event queue component
                AddComponent(entity, AgilityAnimationEvents.Default);
            }
        }
    }
}
