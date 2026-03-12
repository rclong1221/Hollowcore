using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.Abilities;
using DIG.Core.Feedback;
using Audio.Systems;

namespace DIG.Player.Systems.Presentation
{
    /// <summary>
    /// EPIC 13.14.2: Surface Impact Presentation System
    ///
    /// Handles VFX and audio spawning when player lands on surfaces.
    /// Consumes SurfaceImpactRequest enableable component set by FallDetectionSystem.
    ///
    /// Features:
    /// - Surface-type aware (dirt, metal, water etc.)
    /// - Velocity-based intensity scaling
    /// - VFX and audio spawning via AudioManager/VFXManager
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SurfaceImpactPresentationSystem : SystemBase
    {
        // private AudioManager _audioManager;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<SurfaceImpactRequest>();
        }

        protected override void OnUpdate()
        {
            // if (_audioManager == null) ...

            // Process all entities with enabled SurfaceImpactRequest
            foreach (var (impactRequest, entity) in
                     SystemAPI.Query<RefRO<SurfaceImpactRequest>>()
                     .WithAll<SurfaceImpactRequest>()
                     .WithEntityAccess())
            {
                // Check if component is enabled
                if (!SystemAPI.IsComponentEnabled<SurfaceImpactRequest>(entity))
                {
                    continue;
                }

                var request = impactRequest.ValueRO;

                // Calculate intensity from impact velocity (more negative = harder impact)
                // MinSurfaceImpactVelocity is -4, lethal might be -15 or so
                float intensity = math.clamp(math.abs(request.ImpactVelocity) / 15f, 0.1f, 1f);

                // Resolve surface material ID
                int materialId = request.SurfaceMaterialId;
                if (materialId == 0)
                {
                    materialId = SurfaceDetectionService.GetDefaultId();
                }

                // Get surface material for VFX/audio lookup
                var surfaceMaterial = SurfaceDetectionService.GetMaterial(materialId);

                // Delegate to GameplayFeedbackManager (Handles Audio + VFX)
                // Note: Normal alignment for VFX is currently simplified to vertical/prefab default 
                // by the MMF_ParticlesInstantiation. If normal alignment is critical, 
                // GameplayFeedbackManager needs extension.
                
                GameplayFeedbackManager.TriggerLand(intensity, materialId, request.ContactPoint);

                // Disable the request component (consumed)
                SystemAPI.SetComponentEnabled<SurfaceImpactRequest>(entity, false);
            }
        }
    }
}
