using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Core.Input;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 2: Maps PlayerState to MotionState, handles state transitions,
    /// and caches paradigm weights for other systems.
    /// Runs first in the weapon pipeline so downstream systems have current state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct ProceduralMotionStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProceduralMotionState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            // Read paradigm settings (ECS singleton synced from managed ParadigmStateMachine)
            var hasParadigm = SystemAPI.HasSingleton<ParadigmSettings>();
            var paradigm = hasParadigm ? SystemAPI.GetSingleton<ParadigmSettings>() : default;

            foreach (var (motionState, config, playerState, weaponAim) in
                     SystemAPI.Query<RefRW<ProceduralMotionState>, RefRO<ProceduralMotionConfig>,
                             RefRO<PlayerState>, RefRO<DIG.Weapons.WeaponAimState>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                ref var ms = ref motionState.ValueRW;

                // Map PlayerState + WeaponAimState to MotionState
                MotionState newState = MapToMotionState(playerState.ValueRO, weaponAim.ValueRO.IsAiming);

                // State transition
                if (newState != ms.CurrentState)
                {
                    ms.PreviousState = ms.CurrentState;
                    ms.CurrentState = newState;
                    ms.StateBlendT = 0f;

                    // Get transition duration from blob
                    if (config.ValueRO.ProfileBlob.IsCreated)
                    {
                        ref var blob = ref config.ValueRO.ProfileBlob.Value;
                        int idx = (int)newState;
                        if (idx >= 0 && idx < blob.StateOverrides.Length)
                        {
                            float duration = blob.StateOverrides[idx].TransitionDuration;
                            ms.StateTransitionSpeed = duration > 0.001f ? 1f / duration : 100f;
                        }
                    }
                }

                // Advance blend
                if (ms.StateBlendT < 1f)
                {
                    ms.StateBlendT = math.saturate(ms.StateBlendT + dt * ms.StateTransitionSpeed);
                }

                // Cache paradigm weights from blob
                if (hasParadigm && config.ValueRO.ProfileBlob.IsCreated)
                {
                    ref var blob = ref config.ValueRO.ProfileBlob.Value;
                    int pIdx = (int)paradigm.ActiveParadigm;
                    if (pIdx >= 0 && pIdx < blob.ParadigmWeights.Length)
                    {
                        ref var pw = ref blob.ParadigmWeights[pIdx];
                        ms.FPMotionWeight = pw.FPMotionWeight;
                        ms.CameraMotionWeight = pw.CameraMotionWeight;
                        ms.WeaponMotionWeight = pw.WeaponMotionWeight;
                        ms.HitReactionWeight = pw.HitReactionWeight;
                        ms.BobWeight = pw.BobWeight;
                        ms.SwayWeight = pw.SwayWeight;
                    }
                }
            }
        }

        private static MotionState MapToMotionState(in PlayerState ps, bool isAiming)
        {
            // Priority: ADS > Sprint > special states > stance
            if (isAiming && ps.MovementState != PlayerMovementState.Sprinting)
                return MotionState.ADS;

            return ps.MovementState switch
            {
                PlayerMovementState.Idle => ps.Stance == PlayerStance.Crouching ? MotionState.Crouch : MotionState.Idle,
                PlayerMovementState.Walking => ps.Stance == PlayerStance.Crouching ? MotionState.Crouch : MotionState.Walk,
                PlayerMovementState.Running => MotionState.Walk,
                PlayerMovementState.Sprinting => MotionState.Sprint,
                PlayerMovementState.Jumping => MotionState.Airborne,
                PlayerMovementState.Falling => MotionState.Airborne,
                PlayerMovementState.Climbing => MotionState.Climb,
                PlayerMovementState.Swimming => MotionState.Swim,
                PlayerMovementState.Rolling => MotionState.Airborne,
                PlayerMovementState.Diving => MotionState.Airborne,
                PlayerMovementState.Sliding => MotionState.Slide,
                PlayerMovementState.Staggered => MotionState.Staggered,
                PlayerMovementState.Knockdown => MotionState.Staggered,
                _ => MotionState.Idle
            };
        }
    }
}
