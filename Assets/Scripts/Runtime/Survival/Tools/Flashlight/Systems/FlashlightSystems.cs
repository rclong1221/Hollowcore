using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Handles flashlight toggle when use input is pressed.
    /// Runs on both client and server (predicted) for responsive feel.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolRaycastSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlashlightToggleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FlashlightToggleJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct FlashlightToggleJob : IJobEntity
        {
            void Execute(
                ref FlashlightTool flashlight,
                in ToolUsageState usageState,
                in ToolDurability durability)
            {
                // Toggle on use input (check for press, not hold)
                // The IsInUse is set when Use.IsSet is true in ToolRaycastSystem
                // We want to toggle on the rising edge, so we track via UseTimer
                // If UseTimer < deltaTime (approximately), this is a fresh press

                // Skip if depleted - can't turn on
                if (durability.IsDepleted && !flashlight.IsOn)
                    return;

                // Toggle on use input (when useTimer is very small = just started using)
                // This is a simple approach - a proper implementation would track previous state
                if (usageState.IsInUse && usageState.UseTimer < 0.1f)
                {
                    flashlight.IsOn = !flashlight.IsOn;
                }

                // Force off if battery depleted while on
                if (flashlight.IsOn && durability.IsDepleted)
                {
                    flashlight.IsOn = false;
                }
            }
        }
    }

    /// <summary>
    /// Drains flashlight battery while it's turned on.
    /// Runs on server (authoritative) for battery management.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(FlashlightToggleSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlashlightDrainSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            new FlashlightDrainJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct FlashlightDrainJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref FlashlightTool flashlight,
                ref ToolDurability durability)
            {
                // Only drain if on
                if (!flashlight.IsOn)
                    return;

                // Drain battery
                durability.Current -= flashlight.BatteryDrainPerSecond * DeltaTime;

                if (durability.Current <= 0f)
                {
                    durability.Current = 0f;
                    durability.IsDepleted = true;
                    flashlight.IsOn = false;
                }
            }
        }
    }

    /// <summary>
    /// Updates ToggleableLight entities to match their associated flashlight state.
    /// Presentation layer system that controls actual Unity lights.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlashlightLightSyncSystem : ISystem
    {
        private ComponentLookup<FlashlightTool> _flashlightLookup;

        public void OnCreate(ref SystemState state)
        {
            _flashlightLookup = state.GetComponentLookup<FlashlightTool>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _flashlightLookup.Update(ref state);

            // Sync flashlight tool state to associated light entities
            foreach (var (flashlight, _) in
                     SystemAPI.Query<RefRO<FlashlightTool>, RefRO<Tool>>())
            {
                var lightEntity = flashlight.ValueRO.LightEntity;
                if (lightEntity == Entity.Null)
                    continue;

                if (SystemAPI.HasComponent<ToggleableLight>(lightEntity))
                {
                    var toggleableLight = SystemAPI.GetComponentRW<ToggleableLight>(lightEntity);
                    toggleableLight.ValueRW.ShouldBeEnabled = flashlight.ValueRO.IsOn;
                }
            }
        }
    }
}
