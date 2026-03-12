using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Shared;

namespace DIG.Survival.Resources
{
    /// <summary>
    /// Calculates total inventory weight and sets overencumbered state.
    /// Uses change filter to only run when inventory changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct InventoryWeightSystem : ISystem
    {
        private ResourceWeights _weights;
        private bool _weightsInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get or cache resource weights
            if (!_weightsInitialized)
            {
                if (SystemAPI.TryGetSingleton<ResourceWeights>(out var weights))
                {
                    _weights = weights;
                    _weightsInitialized = true;
                }
                else
                {
                    _weights = ResourceWeights.Default;
                }
            }

            foreach (var (inventoryBuffer, capacity) in
                     SystemAPI.Query<DynamicBuffer<InventoryItem>, RefRW<InventoryCapacity>>()
                     .WithAll<Simulate>())
            {
                float totalWeight = 0f;

                for (int i = 0; i < inventoryBuffer.Length; i++)
                {
                    var item = inventoryBuffer[i];
                    float weightPerUnit = _weights.GetWeight(item.ResourceType);
                    totalWeight += weightPerUnit * item.Quantity;
                }

                ref var cap = ref capacity.ValueRW;
                cap.CurrentWeight = totalWeight;
                cap.IsOverencumbered = totalWeight > cap.MaxWeight;
            }
        }
    }

    /// <summary>
    /// Applies movement penalty when player is overencumbered.
    /// Modifies movement speed multiplier based on overencumbered state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InventoryWeightSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OverencumberedMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system modifies a movement modifier component
            // The actual movement system reads this modifier
            foreach (var (capacity, overencumberedModifier) in
                     SystemAPI.Query<RefRO<InventoryCapacity>, RefRW<OverencumberedModifier>>()
                     .WithAll<Simulate>())
            {
                ref var modifier = ref overencumberedModifier.ValueRW;

                if (capacity.ValueRO.IsOverencumbered)
                {
                    modifier.SpeedMultiplier = capacity.ValueRO.OverencumberedSpeedMultiplier;
                    modifier.DisableSprint = true;
                }
                else
                {
                    modifier.SpeedMultiplier = 1f;
                    modifier.DisableSprint = false;
                }
            }
        }
    }

    /// <summary>
    /// Movement modifier applied when overencumbered.
    /// Read by the player movement system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct OverencumberedModifier : IComponentData
    {
        /// <summary>
        /// Speed multiplier (1.0 = normal, 0.5 = half speed).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float SpeedMultiplier;

        /// <summary>
        /// If true, sprinting is disabled.
        /// </summary>
        [GhostField]
        public bool DisableSprint;

        /// <summary>
        /// Default (no penalty).
        /// </summary>
        public static OverencumberedModifier Default => new()
        {
            SpeedMultiplier = 1f,
            DisableSprint = false
        };
    }
}
