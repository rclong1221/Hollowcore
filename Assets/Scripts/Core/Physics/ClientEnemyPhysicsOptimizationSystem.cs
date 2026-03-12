using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;

namespace DIG.Core.Physics
{
    /// <summary>
    /// EPIC 15.23: Strips unnecessary solver contacts from enemy ghosts on remote clients.
    ///
    /// On ClientSimulation, enemy ghosts are interpolated and don't need physics solver contacts —
    /// only broadphase presence for raycasts/targeting. This system sets their CollidesWith
    /// to PlayerProjectile only, eliminating all solver work while preserving raycast hits.
    ///
    /// Raycasts still work because:
    ///   - enemy.BelongsTo (Creature=bit8) &amp; Default.CollidesWith (~0u) != 0  ✓
    ///   - Default.BelongsTo (~0u) &amp; enemy.CollidesWith (bit4) != 0          ✓
    ///
    /// Runs once per entity (tracked via ClientPhysicsOptimized tag).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ClientEnemyPhysicsOptimizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool anyProcessed = false;

            foreach (var (physicsCollider, entity) in
                SystemAPI.Query<RefRW<PhysicsCollider>>()
                    .WithAll<ShowHealthBarTag>()
                    .WithNone<GhostOwnerIsLocal>()
                    .WithNone<ClientPhysicsOptimized>()
                    .WithEntityAccess())
            {
                if (!physicsCollider.ValueRO.IsValid)
                    continue;

                var filter = physicsCollider.ValueRO.Value.Value.GetCollisionFilter();

                // Keep BelongsTo unchanged (Creature layer) for incoming raycasts.
                // Set CollidesWith to PlayerProjectile only — no dynamic bodies have
                // BelongsTo=PlayerProjectile, so zero solver contacts are generated.
                filter.CollidesWith = CollisionLayers.PlayerProjectile;
                physicsCollider.ValueRW.Value.Value.SetCollisionFilter(filter);

                ecb.AddComponent<ClientPhysicsOptimized>(entity);
                anyProcessed = true;
            }

            if (anyProcessed)
            {
                ecb.Playback(state.EntityManager);
            }
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Tag component marking that a client-side enemy ghost's physics has been optimized.
    /// Prevents reprocessing by ClientEnemyPhysicsOptimizationSystem.
    /// </summary>
    public struct ClientPhysicsOptimized : IComponentData { }
}
