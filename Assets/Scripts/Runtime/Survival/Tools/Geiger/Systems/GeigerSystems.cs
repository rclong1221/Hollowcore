using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Survival.Radiation;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Updates Geiger counter radiation readings based on player's radiation exposure.
    /// Runs on both client and server (predicted).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolRaycastSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct GeigerUpdateSystem : ISystem
    {
        private ComponentLookup<RadiationExposure> _radiationLookup;
        private ComponentLookup<ToolOwner> _toolOwnerLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _radiationLookup = state.GetComponentLookup<RadiationExposure>(true);
            _toolOwnerLookup = state.GetComponentLookup<ToolOwner>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _radiationLookup.Update(ref state);
            _toolOwnerLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Update Geiger readings
            foreach (var (geiger, entity) in
                     SystemAPI.Query<RefRW<GeigerTool>>()
                     .WithAll<Simulate, Tool>()
                     .WithEntityAccess())
            {
                ref var geigerRef = ref geiger.ValueRW;

                // Update timer
                geigerRef.TimeSinceUpdate += deltaTime;

                // Only update at specified interval
                if (geigerRef.TimeSinceUpdate < geigerRef.UpdateInterval)
                    continue;

                geigerRef.TimeSinceUpdate = 0f;

                // Get owner's radiation exposure
                if (!_toolOwnerLookup.HasComponent(entity))
                    continue;

                var ownerEntity = _toolOwnerLookup[entity].OwnerEntity;
                if (!_radiationLookup.HasComponent(ownerEntity))
                {
                    geigerRef.CurrentRadiationLevel = 0f;
                    continue;
                }

                var radiation = _radiationLookup[ownerEntity];

                // Display the current accumulation rate (how much radiation per second)
                // This gives players useful information about their current danger level
                geigerRef.CurrentRadiationLevel = radiation.CurrentAccumulationRate;
            }
        }
    }

    /// <summary>
    /// Client-side system that updates Geiger display state for UI.
    /// Shows/hides Geiger HUD based on whether it's the active tool.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct GeigerDisplaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Only run on client
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Find local player's active tool and check if it's a Geiger
            foreach (var (activeTool, displayState, _) in
                     SystemAPI.Query<RefRO<ActiveTool>, RefRW<GeigerDisplayState>, RefRO<GhostOwnerIsLocal>>())
            {
                var toolEntity = activeTool.ValueRO.ToolEntity;

                // Check if active tool is a Geiger
                bool isGeigerActive = false;
                float radiationLevel = 0f;

                if (toolEntity != Entity.Null &&
                    SystemAPI.HasComponent<GeigerTool>(toolEntity))
                {
                    isGeigerActive = true;
                    var geiger = SystemAPI.GetComponent<GeigerTool>(toolEntity);
                    radiationLevel = geiger.CurrentRadiationLevel;
                }

                ref var display = ref displayState.ValueRW;
                display.IsVisible = isGeigerActive;

                // Smooth interpolation for display
                if (isGeigerActive)
                {
                    float lerpSpeed = 5f * SystemAPI.Time.DeltaTime;
                    display.DisplayLevel = math.lerp(display.DisplayLevel, radiationLevel, lerpSpeed);
                }
            }
        }
    }
}
