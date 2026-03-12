using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/VaultAuthoring")]
    public class VaultAuthoring : MonoBehaviour
    {
        [Header("Vault Settings")]
        [Tooltip("Enable vaulting ability")]
        public bool CanVault = true;

        [Tooltip("Minimum obstacle height for vault (meters)")]
        public float MinVaultHeight = 0.3f;

        [Tooltip("Maximum obstacle height for vault (meters)")]
        public float MaxVaultHeight = 1.2f;

        [Tooltip("Duration of vault animation in seconds")]
        public float VaultDuration = 0.6f;

        class Baker : Baker<VaultAuthoring>
        {
            public override void Bake(VaultAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Bake AgilityConfig (required by VaultAbilitySystem)
                // We default other abilities to false here as they are handled by their own bridge systems
                // or legacy systems, and AgilityConfig is primarily used by VaultSystem currently.
                AddComponent(entity, new AgilityConfig
                {
                    CanVault = authoring.CanVault,
                    MinVaultHeight = authoring.MinVaultHeight,
                    MaxVaultHeight = authoring.MaxVaultHeight,
                    
                    // Defaults for others strings they aren't driven by this authoring
                    CanDodge = false,
                    CanRoll = false,
                    CanCrawl = false
                });

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
                
                // Add tag component if not present
                AddComponent<HasAgilityAbilities>(entity);
                
                // Ensure AgilityAnimationEvents is present for event queueing
                AddComponent(entity, AgilityAnimationEvents.Default);
            }
        }
    }
}
