using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Audio.Systems;
using DIG.Core.Feedback;
using DIG.Core.Settings;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 11: Bridges surface impacts to haptic feedback.
    /// Reads recently processed impacts from SurfaceImpactPresenterSystem,
    /// resolves per-surface haptic profiles, and triggers GameplayFeedbackManager.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SurfaceHapticBridgeSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;
        private const float MaxHapticDistance = 5f;

        protected override void OnUpdate()
        {
            if (!GameplayFeedbackManager.Instance) return;

            // Check global intensity — skip if motion effects are disabled
            float globalIntensity = 1f;
            if (MotionIntensitySettings.HasInstance)
            {
                globalIntensity = MotionIntensitySettings.Instance.GlobalIntensity;
                if (globalIntensity <= 0.01f) return;
            }

            // Read recently processed impacts
            var recentImpacts = SurfaceImpactPresenterSystem.RecentImpacts;
            if (recentImpacts == null || recentImpacts.Count == 0) return;

            var cam = Camera.main;
            if (cam == null) return;
            float3 camPos = cam.transform.position;

            if (_registry == null)
                _registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");

            // Find strongest nearby impact for haptic trigger
            float strongestHaptic = 0f;

            for (int i = 0; i < recentImpacts.Count; i++)
            {
                var impact = recentImpacts[i];
                float dist = math.distance(impact.Position, camPos);
                if (dist > MaxHapticDistance) continue;

                // Resolve surface material for haptic profile
                SurfaceMaterial material = null;
                if (_registry != null)
                {
                    _registry.TryGetById(impact.SurfaceMaterialId, out material);
                    material ??= _registry.DefaultMaterial;
                }

                float hapticIntensity = material != null ? material.HapticIntensity : 0.5f;

                // Scale by distance attenuation, ImpactClass weight, and global intensity
                float distFactor = 1f - math.saturate(dist / MaxHapticDistance);
                float classWeight = GetImpactClassHapticWeight(impact.ImpactClass);
                float scaledIntensity = hapticIntensity * distFactor * classWeight * impact.Intensity * globalIntensity;

                if (scaledIntensity > strongestHaptic)
                {
                    strongestHaptic = scaledIntensity;
                }
            }

            // Trigger haptic feedback for the strongest impact
            if (strongestHaptic > 0.01f)
            {
                GameplayFeedbackManager.Instance.OnDamage(math.saturate(strongestHaptic));
            }
        }

        private float GetImpactClassHapticWeight(ImpactClass impactClass)
        {
            return impactClass switch
            {
                ImpactClass.Bullet_Light => 0.2f,
                ImpactClass.Bullet_Medium => 0.4f,
                ImpactClass.Bullet_Heavy => 0.6f,
                ImpactClass.Melee_Light => 0.3f,
                ImpactClass.Melee_Heavy => 0.5f,
                ImpactClass.Explosion_Small => 0.6f,
                ImpactClass.Explosion_Large => 1f,
                ImpactClass.Footstep => 0.05f,
                ImpactClass.BodyFall => 0.3f,
                ImpactClass.Environmental => 0.15f,
                _ => 0.3f
            };
        }
    }
}
