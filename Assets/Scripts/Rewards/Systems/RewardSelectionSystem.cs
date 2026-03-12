using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Processes RewardSelectionRequest on RunState entity.
    /// Applies the selected reward via RewardApplicationUtility and clears the choice buffer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChoiceGenerationSystem))]
    public partial class RewardSelectionSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            _requestQuery = GetEntityQuery(
                ComponentType.ReadWrite<RunState>(),
                ComponentType.ReadOnly<RewardSelectionRequest>());
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.IsEmptyIgnoreFilter)
                return;

            if (!SystemAPI.ManagedAPI.HasSingleton<RewardRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<RewardRegistryManaged>();
            var entities = _requestQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int e = 0; e < entities.Length; e++)
            {
                var entity = entities[e];
                var request = EntityManager.GetComponentData<RewardSelectionRequest>(entity);
                int slotIndex = request.SlotIndex;

                // Remove request regardless of outcome
                EntityManager.RemoveComponent<RewardSelectionRequest>(entity);

                if (!EntityManager.HasBuffer<PendingRewardChoice>(entity))
                    continue;

                var choices = EntityManager.GetBuffer<PendingRewardChoice>(entity);
                if (slotIndex < 0 || slotIndex >= choices.Length)
                    continue;

                var choice = choices[slotIndex];
                if (choice.IsSelected)
                    continue;

                if (!registry.RewardById.TryGetValue(choice.RewardId, out var rewardDef))
                    continue;

                var runState = EntityManager.GetComponentData<RunState>(entity);
                RewardApplicationUtility.Apply(rewardDef, choice, ref runState, entity, EntityManager);
                EntityManager.SetComponentData(entity, runState);

                // Clear buffer (choice data no longer needed after application)
                choices.Clear();

                LogRewardApplied(rewardDef.DisplayName, rewardDef.Type);
            }

            entities.Dispose();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogRewardApplied(string name, RewardType type)
        {
            Debug.Log($"[RewardSelection] Applied reward '{name}' (type={type})");
        }
    }
}
