using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Handles bow draw, aim, and release mechanics.
    /// Reads UseRequest (left-click) and WeaponAimState (right-click) to update BowState.
    /// The animation bridge reads BowState to set animator parameters.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerToItemInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct BowActionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool isServer = state.WorldUnmanaged.IsServer();

            // Get current network tick for release tracking
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;
            bool isFirstPrediction = networkTime.IsFirstTimeFullyPredictingTick;

            foreach (var (action, bowAction, bowState, request, aimState, charItem, entity) in
                     SystemAPI.Query<RefRW<UsableAction>, RefRO<BowAction>, RefRW<BowState>,
                                    RefRO<UseRequest>, RefRO<WeaponAimState>, RefRO<CharacterItem>>()
                     .WithEntityAccess())
            {
                // Client-side: Only process weapons owned by the local player
                if (!isServer)
                {
                    Entity owner = charItem.ValueRO.OwnerEntity;
                    if (owner == Entity.Null ||
                        !SystemAPI.HasComponent<GhostOwnerIsLocal>(owner) ||
                        !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(owner))
                        continue;
                }

                ref var stateRef = ref bowState.ValueRW;
                var config = bowAction.ValueRO;
                var useRequest = request.ValueRO;
                var aiming = aimState.ValueRO;

                // Track previous state for release detection
                bool wasDrawing = stateRef.IsDrawing;

                // Update aiming state from WeaponAimState (right-click)
                stateRef.IsAiming = aiming.IsAiming;

                // Handle draw state (left-click held)
                if (useRequest.StartUse)
                {
                    // Reset debounce counter when input is active
                    stateRef.ReleaseDebounceCounter = 0;

                    // Start or continue drawing
                    if (!stateRef.IsDrawing)
                    {
                        // Just started drawing - reset all state for new action
                        stateRef.IsDrawing = true;
                        stateRef.CurrentDrawTime = 0f;
                        stateRef.DrawProgress = 0f;
                        stateRef.IsFullyDrawn = false;
                        stateRef.JustReleased = false;
                        stateRef.HasReleasedThisAction = false;
                        stateRef.ReleaseTickValue = 0;
                        stateRef.ReleaseDebounceTime = 0f;
                    }
                    else
                    {
                        // Continue drawing
                        stateRef.CurrentDrawTime += deltaTime;
                        stateRef.DrawProgress = math.saturate(stateRef.CurrentDrawTime / config.DrawTime);
                        stateRef.IsFullyDrawn = stateRef.DrawProgress >= 1f;
                    }

                    stateRef.TimeSinceRelease = 0f;
                    stateRef.ReleaseDebounceTime = 0f;
                }
                else if (wasDrawing && !stateRef.HasReleasedThisAction)
                {
                    // StartUse is false but we were drawing (and haven't released yet)
                    // Use TIME-based debounce instead of tick-based (more reliable with prediction)
                    stateRef.ReleaseDebounceTime += deltaTime;
                    stateRef.ReleaseDebounceCounter++;

                    const float RELEASE_DEBOUNCE_DURATION = 0.15f;

                    if (stateRef.ReleaseDebounceTime >= RELEASE_DEBOUNCE_DURATION)
                    {
                        // Released the draw - fire arrow!
                        stateRef.IsDrawing = false;
                        stateRef.JustReleased = true;
                        stateRef.TimeSinceRelease = 0f;
                        stateRef.ReleaseDebounceCounter = 0;
                        stateRef.ReleaseDebounceTime = 0f;
                        stateRef.HasReleasedThisAction = true;
                        stateRef.ReleaseTickValue = currentTick;

                        // Reset draw progress after release
                        stateRef.DrawProgress = 0f;
                        stateRef.CurrentDrawTime = 0f;
                        stateRef.IsFullyDrawn = false;
                    }
                    else
                    {
                        // Still within debounce window - continue drawing (ignore false release)
                        stateRef.CurrentDrawTime += deltaTime;
                        stateRef.DrawProgress = math.saturate(stateRef.CurrentDrawTime / config.DrawTime);
                        stateRef.IsFullyDrawn = stateRef.DrawProgress >= 1f;
                    }
                }
                else
                {
                    // Not drawing - update release timer
                    stateRef.TimeSinceRelease += deltaTime;

                    // Clear JustReleased after a short time
                    if (stateRef.JustReleased && stateRef.TimeSinceRelease > 0.5f)
                    {
                        stateRef.JustReleased = false;
                    }
                }
            }
        }
    }
}
