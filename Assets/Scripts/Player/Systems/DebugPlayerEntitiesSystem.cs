using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Temporary diagnostic system to log all player entities and their Simulate status.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DebugPlayerEntitiesSystem : SystemBase
    {
        private float _lastLogTime;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            // Only log every 2 seconds
            if (SystemAPI.Time.ElapsedTime - _lastLogTime < 2.0f)
                return;

            _lastLogTime = (float)SystemAPI.Time.ElapsedTime;

            // Debug.Log("========== PLAYER ENTITIES DEBUG ==========");

            // Log all player entities
            foreach (var (rollState, entity) in SystemAPI.Query<RefRO<Player.Components.DodgeRollState>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                bool hasSimulate = EntityManager.HasComponent<Simulate>(entity);
                var roll = rollState.ValueRO;
                // Debug.Log($"[PlayerEntities] Entity {entity.Index}: HasSimulate={hasSimulate}, IsActive={roll.IsActive}, StartFrame={roll.StartFrame}");
            }

            // Debug.Log("===========================================");
        }
    }
}
