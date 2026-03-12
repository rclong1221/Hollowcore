using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Server-side movement validation.
    /// Checks position delta vs max speed per tick. Detects teleport hacks and speed hacks.
    /// Runs after PlayerMovementSystem in PredictedFixedStepSimulationSystemGroup.
    /// Burst-compiled ISystem — zero managed overhead on the hot path.
    /// Budget: &lt;0.1ms (one check per player per tick, ~64 players max).
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    public partial struct MovementValidationSystem : ISystem
    {
        private ComponentLookup<MovementValidationState> _moveLookup;
        private ComponentLookup<ValidationChildTag> _childTagLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ValidationConfig>();
            state.RequireForUpdate<NetworkTime>();
            _moveLookup = state.GetComponentLookup<MovementValidationState>(false);
            _childTagLookup = state.GetComponentLookup<ValidationChildTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ValidationConfig>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.ServerTick.IsValid) return;
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            _moveLookup.Update(ref state);
            _childTagLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, link, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<ValidationLink>>()
                    .WithAll<GhostOwner>()
                    .WithEntityAccess())
            {
                var childEntity = link.ValueRO.ValidationChild;
                if (childEntity == Entity.Null) continue;
                if (!_childTagLookup.HasComponent(childEntity)) continue;
                if (!_moveLookup.HasComponent(childEntity)) continue;

                var moveState = _moveLookup[childEntity];
                float3 currentPos = transform.ValueRO.Position;

                // Skip first tick (no previous position)
                if (moveState.LastValidatedTick == 0)
                {
                    moveState.LastValidatedPosition = currentPos;
                    moveState.LastValidatedTick = currentTick;
                    _moveLookup[childEntity] = moveState;
                    continue;
                }

                // Skip during teleport immunity
                if (moveState.TeleportCooldownTick > currentTick)
                {
                    moveState.LastValidatedPosition = currentPos;
                    moveState.LastValidatedTick = currentTick;
                    _moveLookup[childEntity] = moveState;
                    continue;
                }

                float delta = math.distance(currentPos, moveState.LastValidatedPosition);

                // Teleport detection (instant large movement)
                if (delta > config.TeleportThreshold)
                {
                    RateLimitHelper.CreateViolationDeferred(
                        ecb, entity,
                        ViolationType.Movement, 1.0f, 0, currentTick);

                    moveState.AccumulatedError = 0f;
                    moveState.LastValidatedPosition = currentPos;
                    moveState.LastValidatedTick = currentTick;
                    _moveLookup[childEntity] = moveState;
                    continue;
                }

                // Speed check — use most generous speed limit (sprint) + tolerance
                float maxAllowedDelta = config.MaxSpeedSprinting * config.SpeedToleranceMultiplier * dt;

                if (delta > maxAllowedDelta)
                {
                    // Accumulate error
                    float excess = delta - maxAllowedDelta;
                    moveState.AccumulatedError += excess;

                    if (moveState.AccumulatedError > config.MaxAccumulatedError)
                    {
                        RateLimitHelper.CreateViolationDeferred(
                            ecb, entity,
                            ViolationType.Movement, 0.6f, 1, currentTick);

                        moveState.AccumulatedError = 0f;
                    }
                }
                else
                {
                    // Decay error when moving normally
                    moveState.AccumulatedError = math.max(0f,
                        moveState.AccumulatedError - config.ErrorDecayRate * dt);
                }

                moveState.LastValidatedPosition = currentPos;
                moveState.LastValidatedTick = currentTick;
                _moveLookup[childEntity] = moveState;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
