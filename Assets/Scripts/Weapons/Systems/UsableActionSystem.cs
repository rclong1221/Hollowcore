using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Base system for all usable actions.
    /// Manages cooldowns and use state transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct UsableActionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool isServer = state.WorldUnmanaged.IsServer();

            foreach (var (action, request, charItem, entity) in
                     SystemAPI.Query<RefRW<UsableAction>, RefRO<UseRequest>, RefRO<CharacterItem>>()
                     .WithEntityAccess())
            {
                // Client-side: Only process weapons owned by the local player
                // Remote players' UsableAction state comes from server replication
                // Note: GhostOwnerIsLocal is an enableable component - must check both HasComponent AND IsComponentEnabled
                if (!isServer)
                {
                    Entity owner = charItem.ValueRO.OwnerEntity;
                    if (owner == Entity.Null ||
                        !SystemAPI.HasComponent<GhostOwnerIsLocal>(owner) ||
                        !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(owner))
                        continue;
                }

                ref var actionRef = ref action.ValueRW;

                // Update cooldown
                if (actionRef.CooldownRemaining > 0)
                {
                    actionRef.CooldownRemaining -= deltaTime;
                    if (actionRef.CooldownRemaining < 0)
                        actionRef.CooldownRemaining = 0;
                }

                // Update use time if using
                if (actionRef.IsUsing)
                {
                    actionRef.UseTime += deltaTime;
                }

                // Determine if can use
                // Note: Specific resource checks (Ammo, Energy) are handled by specific systems (WeaponFireSystem, etc.)
                actionRef.CanUse = actionRef.CooldownRemaining <= 0 && 
                                   !actionRef.IsUsing;

                // Handle use request
                if (request.ValueRO.StartUse && actionRef.CanUse)
                {
                    actionRef.IsUsing = true;
                    actionRef.UseTime = 0f;
                }

                if (request.ValueRO.StopUse && actionRef.IsUsing)
                {
                    actionRef.IsUsing = false;
                }
            }
        }
    }
}
