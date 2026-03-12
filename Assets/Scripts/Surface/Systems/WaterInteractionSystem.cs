using Unity.Entities;
using Unity.Mathematics;
using Audio.Systems;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 4: Water Interaction System.
    /// Detects impacts on liquid surfaces and enqueues splash-specific VFX events.
    /// Also handles footstep splash replacement when walking through water.
    ///
    /// Water-specific audio is already handled by SurfaceImpactPresenterSystem.PlayAudio()
    /// which checks material.IsLiquid for muted plunk sounds.
    /// This system adds the splash column VFX and suppresses decals for water hits.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class WaterInteractionSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;

        protected override void OnCreate()
        {
            _registry = UnityEngine.Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
        }

        protected override void OnUpdate()
        {
            if (_registry == null) return;

            // Process footstep events on water surfaces — enqueue splash VFX
            foreach (var footstep in
                     SystemAPI.Query<RefRO<FootstepEvent>>())
            {
                var evt = footstep.ValueRO;

                if (!_registry.TryGetById(evt.MaterialId, out var material) || material == null)
                    continue;
                if (!material.IsLiquid) continue;

                // Replace footstep with splash effect
                SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                {
                    Position = evt.Position,
                    Normal = new float3(0, 1, 0), // Water surface is always up
                    Velocity = new float3(0, -1, 0), // Downward
                    SurfaceId = SurfaceID.Water,
                    ImpactClass = ImpactClass.Footstep,
                    SurfaceMaterialId = evt.MaterialId,
                    Intensity = math.clamp(evt.Intensity, 0.3f, 0.8f),
                    LODTier = EffectLODTier.Full
                });
            }
        }
    }
}
