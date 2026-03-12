using Unity.Burst;
using Unity.Entities;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Destroys AchievementUnlockEvent transient entities after reward distribution.
    /// Follows CombatEventCleanupSystem lifecycle pattern.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(AchievementRewardSystem))]
    public partial struct AchievementCleanupSystem : ISystem
    {
        private EntityQuery _unlockEventQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _unlockEventQuery = state.GetEntityQuery(ComponentType.ReadOnly<AchievementUnlockEvent>());
            state.RequireForUpdate(_unlockEventQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Single call — no temp array or ECB needed
            state.EntityManager.DestroyEntity(_unlockEventQuery);
        }
    }
}
