using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Audio.Systems;
using DIG.Surface.Config;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 4: Footprint Decal Spawner.
    /// Consumes FootstepEvent components and spawns footprint decals via DecalManager.
    /// Left/right alternation uses FootIndex from the event.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class FootprintDecalSpawnerSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;
        private DecalManager _decalManager;

        protected override void OnCreate()
        {
            _registry = UnityEngine.Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
        }

        protected override void OnUpdate()
        {
            if (_decalManager == null)
                _decalManager = DecalManager.Instance;
            if (_registry == null || _decalManager == null) return;

            // Phase 7: Paradigm can disable footprints entirely (e.g. MOBA)
            var paradigmConfig = ParadigmSurfaceConfig.Instance;
            if (paradigmConfig != null && paradigmConfig.ActiveProfile != null && !paradigmConfig.ActiveProfile.FootprintsEnabled)
                return;

            foreach (var (footstep, entity) in
                     SystemAPI.Query<RefRO<FootstepEvent>>()
                     .WithEntityAccess())
            {
                var evt = footstep.ValueRO;

                if (!_registry.TryGetById(evt.MaterialId, out var material) || material == null)
                    material = _registry.DefaultMaterial;
                if (material == null) continue;
                if (!material.AllowFootprints || material.FootprintDecal == null) continue;

                // Compute footprint rotation: forward = movement direction (approximated by position delta)
                // Use a default forward if no direction available
                var rotation = quaternion.identity;

                // Flip for right foot
                bool isRightFoot = evt.FootIndex == 1;
                if (isRightFoot)
                {
                    rotation = math.mul(rotation, quaternion.AxisAngle(new float3(0, 1, 0), math.PI));
                }

                _decalManager.SpawnDecal(
                    material.FootprintDecal,
                    evt.Position,
                    rotation,
                    GetFootprintLifetime(material)
                );
            }
        }

        /// <summary>
        /// Surface-specific footprint fade time.
        /// </summary>
        private float GetFootprintLifetime(SurfaceMaterial material)
        {
            var sid = SurfaceIdResolver.FromMaterial(material);
            return sid switch
            {
                SurfaceID.Snow => 60f,
                SurfaceID.Mud => 30f,
                SurfaceID.Sand => 15f,
                SurfaceID.Dirt => 20f,
                _ => 10f
            };
        }
    }
}
