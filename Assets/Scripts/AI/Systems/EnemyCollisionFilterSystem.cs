using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using DIG.AI.Components;
using DIG.AI.Profiling;
using DIG.Player.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.23: Enforces correct collision filters on enemy PhysicsCollider blobs at spawn.
    /// Sets BelongsTo=Creature and CollidesWith=CreatureCollidesWith (excludes Creature bit)
    /// to eliminate O(n²) creature-creature contact pairs in the physics solver.
    ///
    /// Runs once per entity (tracked via EnemyCollisionFilterEnforced tag).
    /// Follows GroupIndexOverrideSystem pattern for SetCollisionFilter on shared blobs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct EnemyCollisionFilterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (AIProfilerMarkers.EnemyCollisionFilter.Auto())
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (physicsCollider, entity) in
                    SystemAPI.Query<RefRW<PhysicsCollider>>()
                        .WithAll<AIBrain>()
                        .WithNone<EnemyCollisionFilterEnforced>()
                        .WithEntityAccess())
                {
                    if (!physicsCollider.ValueRO.IsValid)
                        continue;

                    var filter = physicsCollider.ValueRO.Value.Value.GetCollisionFilter();

                    // Set correct creature layer and remove creature-creature collision
                    filter.BelongsTo = CollisionLayers.Creature;
                    filter.CollidesWith = CollisionLayers.CreatureCollidesWith;

                    physicsCollider.ValueRW.Value.Value.SetCollisionFilter(filter);

                    ecb.AddComponent<EnemyCollisionFilterEnforced>(entity);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }
    }

    /// <summary>
    /// Tag component marking that an enemy entity's collision filter has been enforced.
    /// Prevents reprocessing by EnemyCollisionFilterSystem.
    /// </summary>
    public struct EnemyCollisionFilterEnforced : IComponentData { }
}
