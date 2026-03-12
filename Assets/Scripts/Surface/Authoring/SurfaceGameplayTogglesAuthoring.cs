using Unity.Entities;
using UnityEngine;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 8: Singleton authoring for runtime toggling of surface gameplay features.
    /// Place on a single GameObject in your SubScene. If absent, all features default to enabled.
    /// </summary>
    public class SurfaceGameplayTogglesAuthoring : MonoBehaviour
    {
        [Tooltip("Enable speed and friction modifiers from surface type.")]
        public bool EnableMovementModifiers = true;

        [Tooltip("Enable noise multipliers from surface type (affects stealth/hearing).")]
        public bool EnableStealthModifiers = true;

        [Tooltip("Enable ice/slippery surface physics.")]
        public bool EnableSlipPhysics = true;

        [Tooltip("Enable surface-aware fall damage multipliers.")]
        public bool EnableFallDamageModifiers = true;

        [Tooltip("Enable lava/acid/hazard damage zones.")]
        public bool EnableSurfaceDamageZones = true;
    }

    public class SurfaceGameplayTogglesBaker : Baker<SurfaceGameplayTogglesAuthoring>
    {
        public override void Bake(SurfaceGameplayTogglesAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SurfaceGameplayToggles
            {
                EnableMovementModifiers = authoring.EnableMovementModifiers,
                EnableStealthModifiers = authoring.EnableStealthModifiers,
                EnableSlipPhysics = authoring.EnableSlipPhysics,
                EnableFallDamageModifiers = authoring.EnableFallDamageModifiers,
                EnableSurfaceDamageZones = authoring.EnableSurfaceDamageZones
            });
        }
    }
}
