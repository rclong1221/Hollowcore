using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Default consumer for SpawnRequest entities. Logs spawn info and destroys the request.
    /// Games should replace this with their own consumer that bridges SpawnRequest to their
    /// enemy instantiation pipeline (e.g., EnemySpawner, object pooling, etc).
    ///
    /// To disable: set Enabled = false on this system and implement your own consumer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpawnDirectorSystem))]
    public partial class DefaultSpawnRequestConsumerSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(
                ComponentType.ReadOnly<SpawnRequest>(),
                ComponentType.Exclude<SpawnRequestConsumed>());
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.IsEmpty) return;

            CompleteDependency();

            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<SpawnRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var req = requests[i];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SpawnConsumer] Request: poolEntry={req.PoolEntryIndex}, " +
                          $"pos=({req.Position.x:F1},{req.Position.y:F1},{req.Position.z:F1}), " +
                          $"elite={req.IsElite}, diff={req.Difficulty:F2}");
#endif

                // Destroy the request. Games override this system to actually spawn enemies.
                EntityManager.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            requests.Dispose();
        }
    }
}
