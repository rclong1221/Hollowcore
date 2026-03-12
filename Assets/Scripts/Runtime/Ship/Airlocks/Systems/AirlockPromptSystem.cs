using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that displays interaction prompts for nearby airlocks.
    /// Runs only on client world, drives UI only (no gameplay authority).
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.1: Interaction + Prompting
    /// - Selects best airlock (closest + in view + usable)
    /// - Shows appropriate prompt based on player mode and airlock state
    /// - Avoids prompt flicker with single-best-airlock selection
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct AirlockPromptSystem : ISystem
    {
        private ComponentLookup<Airlock> _airlockLookup;
        private ComponentLookup<AirlockInteractable> _interactableLookup;
        private ComponentLookup<AirlockLocked> _lockedLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            
            _airlockLookup = state.GetComponentLookup<Airlock>(true);
            _interactableLookup = state.GetComponentLookup<AirlockInteractable>(true);
            _lockedLookup = state.GetComponentLookup<AirlockLocked>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _airlockLookup.Update(ref state);
            _interactableLookup.Update(ref state);
            _lockedLookup.Update(ref state);
            _transformLookup.Update(ref state);

            // Collect all airlock entities with their positions
            var airlockEntities = new NativeList<Entity>(Allocator.Temp);
            var airlockPositions = new NativeList<float3>(Allocator.Temp);

            foreach (var (transform, airlock, interactable, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Airlock>, RefRO<AirlockInteractable>>()
                     .WithEntityAccess())
            {
                airlockEntities.Add(entity);
                airlockPositions.Add(transform.ValueRO.Position);
            }

            // For each local player, find the best airlock to show prompt for
            foreach (var (playerState, transform, promptState) in
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<LocalTransform>, RefRW<AirlockPromptState>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                float3 playerPos = transform.ValueRO.Position;
                PlayerMode playerMode = playerState.ValueRO.Mode;

                // Find the best airlock (closest within range)
                Entity bestAirlock = Entity.Null;
                float bestDistance = float.MaxValue;
                float bestRange = 0f;

                for (int i = 0; i < airlockEntities.Length; i++)
                {
                    Entity airlockEntity = airlockEntities[i];
                    float3 airlockPos = airlockPositions[i];

                    if (!_interactableLookup.HasComponent(airlockEntity))
                        continue;

                    var interactable = _interactableLookup[airlockEntity];
                    float distance = math.distance(playerPos, airlockPos);

                    if (distance <= interactable.Range && distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAirlock = airlockEntity;
                        bestRange = interactable.Range;
                    }
                }

                ref var prompt = ref promptState.ValueRW;

                if (bestAirlock == Entity.Null)
                {
                    // No airlock in range
                    prompt.TargetAirlock = Entity.Null;
                    prompt.IsPromptVisible = false;
                    prompt.CanInteract = false;
                    prompt.PromptText = default;
                    prompt.Distance = 0f;
                }
                else
                {
                    // Found an airlock - determine prompt
                    var airlock = _airlockLookup[bestAirlock];
                    var interactable = _interactableLookup[bestAirlock];
                    bool isLocked = _lockedLookup.HasComponent(bestAirlock);

                    prompt.TargetAirlock = bestAirlock;
                    prompt.Distance = bestDistance;
                    prompt.IsPromptVisible = true;

                    // Determine prompt text and interactability
                    if (isLocked)
                    {
                        prompt.PromptText = interactable.PromptLocked;
                        prompt.CanInteract = false;
                    }
                    else if (airlock.State != AirlockState.Idle || airlock.CurrentUser != Entity.Null)
                    {
                        prompt.PromptText = interactable.PromptBusy;
                        prompt.CanInteract = false;
                    }
                    else
                    {
                        // Airlock is available - show direction-appropriate prompt
                        if (playerMode == PlayerMode.EVA)
                        {
                            prompt.PromptText = interactable.PromptEnter;
                            prompt.CanInteract = true;
                        }
                        else if (playerMode == PlayerMode.InShip)
                        {
                            prompt.PromptText = interactable.PromptExit;
                            prompt.CanInteract = true;
                        }
                        else
                        {
                            // Player is in a state that can't use airlock (Piloting, Dead, etc.)
                            prompt.IsPromptVisible = false;
                            prompt.CanInteract = false;
                        }
                    }
                }
            }

            airlockEntities.Dispose();
            airlockPositions.Dispose();
        }
    }
}
