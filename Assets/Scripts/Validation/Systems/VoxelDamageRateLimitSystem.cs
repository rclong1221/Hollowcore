using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Rate-limit interceptor for VoxelDamageRpcRequest.
    /// Runs before VoxelDamageRpcReceiveSystem (which lives in DIG.Voxel asmdef
    /// and cannot reference Assembly-CSharp). Destroys rate-limited RPC entities
    /// before the Voxel system sees them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Voxel.VoxelDamageRpcReceiveSystem))]
    public partial class VoxelDamageRateLimitSystem : SystemBase
    {
        private EntityQuery _rpcQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<DIG.Voxel.VoxelDamageRpcRequest>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<ValidationConfig>();
        }

        protected override void OnUpdate()
        {
            if (_rpcQuery.CalculateEntityCount() == 0) return;

            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var connection = receives[i].SourceConnection;
                if (connection == Entity.Null) continue;

                // Resolve player from connection
                if (!SystemAPI.HasComponent<CommandTarget>(connection)) continue;
                var playerEntity = SystemAPI.GetComponent<CommandTarget>(connection).targetEntity;
                if (playerEntity == Entity.Null) continue;

                if (!EntityManager.HasComponent<ValidationLink>(playerEntity)) continue;
                var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;

                if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.VOXEL_DAMAGE))
                {
                    RateLimitHelper.CreateViolation(EntityManager, playerEntity,
                        ViolationType.RateLimit, 0.3f, RpcTypeIds.VOXEL_DAMAGE, 0);

                    // Destroy the RPC entity before VoxelDamageRpcReceiveSystem processes it
                    EntityManager.DestroyEntity(entities[i]);
                }
            }

            entities.Dispose();
            receives.Dispose();
        }
    }
}
