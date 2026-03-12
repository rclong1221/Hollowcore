using Unity.Entities;
using Unity.NetCode;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// 12.3.9: Swimming Event Callbacks
    /// Detects swimming state transitions and sets one-frame event flags.
    /// Other systems can query SwimmingEvents to react to:
    /// - OnEnterWater / OnExitWater
    /// - OnSurface / OnSubmerge
    /// - OnStartSwimming / OnStopSwimming
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WaterDetectionSystem))]
    [UpdateBefore(typeof(SwimmingMovementSystem))]
    public partial struct SwimmingEventSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (swimState, events, entity) in
                SystemAPI.Query<
                    RefRO<SwimmingState>,
                    RefRW<SwimmingEvents>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                ref var evt = ref events.ValueRW;
                var swim = swimState.ValueRO;

                // Clear previous frame's events
                evt.ClearEvents();

                // Detect water zone enter/exit
                bool wasInWater = evt.PrevWaterZone != Entity.Null;
                bool isInWater = swim.WaterZoneEntity != Entity.Null;

                if (isInWater && !wasInWater)
                {
                    evt.OnEnterWater = true;
                }
                else if (!isInWater && wasInWater)
                {
                    evt.OnExitWater = true;
                }

                // Detect swim start/stop
                if (swim.IsSwimming && !evt.PrevIsSwimming)
                {
                    evt.OnStartSwimming = true;
                }
                else if (!swim.IsSwimming && evt.PrevIsSwimming)
                {
                    evt.OnStopSwimming = true;
                }

                // Detect surface/submerge transitions
                if (swim.IsSubmerged && !evt.PrevIsSubmerged)
                {
                    evt.OnSubmerge = true;
                }
                else if (!swim.IsSubmerged && evt.PrevIsSubmerged)
                {
                    evt.OnSurface = true;
                }

                // Update previous state for next frame
                evt.PrevWaterZone = swim.WaterZoneEntity;
                evt.PrevIsSwimming = swim.IsSwimming;
                evt.PrevIsSubmerged = swim.IsSubmerged;
            }
        }
    }
}
