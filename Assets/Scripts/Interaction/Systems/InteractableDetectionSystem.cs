using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 1: Detects nearby interactables for players using spatial hash grid.
    ///
    /// Instead of iterating ALL interactable entities (O(N*M)), queries only the player's
    /// grid cell + 8 neighbors (~50 candidates max). Preserves all existing filtering
    /// (ID, type mask, distance, cone, LOS) and scoring logic.
    ///
    /// Adds sticky target bonus to prevent flickering when camera jitters between close targets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractableSpatialMapSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InteractableDetectionSystem : ISystem
    {
        private ComponentLookup<Interactable> _interactableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        private const float StickyTargetBonus = 0.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<InteractableSpatialGrid>();

            _interactableLookup = state.GetComponentLookup<Interactable>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        // Not Burst-compiled: accesses InteractableSpatialGridData static fields.
        // The spatial grid query logic is simple enough that Burst is not critical here.
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var grid = SystemAPI.GetSingleton<InteractableSpatialGrid>();

            if (!grid.IsPopulated || !InteractableSpatialGridData.IsInitialized)
                return;

            _interactableLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var cellToEntities = InteractableSpatialGridData.CellToEntities;

            foreach (var (ability, transform, entity) in
                     SystemAPI.Query<RefRW<InteractAbility>, RefRO<LocalTransform>>()
                     .WithAll<CanInteract>()
                     .WithEntityAccess())
            {
                ref var abilityRef = ref ability.ValueRW;

                // Skip if currently interacting
                if (abilityRef.IsInteracting)
                    continue;

                float3 position = transform.ValueRO.Position + math.up() * 1.0f; // Eye level
                float3 forward = math.forward(transform.ValueRO.Rotation);
                Entity previousTarget = abilityRef.TargetEntity;

                Entity bestTarget = Entity.Null;
                float bestScore = float.MaxValue;

                // Get player's grid cell
                int playerCell = grid.GetCellIndex(transform.ValueRO.Position);
                if (playerCell < 0)
                {
                    abilityRef.TargetEntity = Entity.Null;
                    continue;
                }

                // Query player's cell + 8 neighbors (9 cells total)
                var neighbors = new InteractableNeighborCells();
                int neighborCount = grid.GetNeighborCellsFixed(playerCell, ref neighbors);

                for (int n = 0; n < neighborCount; n++)
                {
                    int cellIndex = neighbors[n];

                    // Iterate all interactables in this cell
                    if (!cellToEntities.TryGetFirstValue(cellIndex, out var interactableEntity, out var iterator))
                        continue;

                    do
                    {
                        // Validate entity still has required components
                        if (!_interactableLookup.HasComponent(interactableEntity) ||
                            !_transformLookup.HasComponent(interactableEntity))
                            continue;

                        var interactable = _interactableLookup[interactableEntity];
                        var interactableTransform = _transformLookup[interactableEntity];

                        if (!interactable.CanInteract)
                            continue;

                        // EPIC 13.17.4: ID Filtering
                        if (abilityRef.RequiredInteractableID != 0 &&
                            interactable.InteractableID != abilityRef.RequiredInteractableID)
                            continue;

                        // EPIC 13.17.4: Type mask filtering
                        if (abilityRef.InteractableTypeMask != 0)
                        {
                            int typeBit = 1 << (int)interactable.Type;
                            if ((abilityRef.InteractableTypeMask & typeBit) == 0)
                                continue;
                        }

                        float3 toTarget = interactableTransform.Position - position;
                        float distance = math.length(toTarget);

                        // Check range
                        if (distance > abilityRef.DetectionRange ||
                            distance > interactable.InteractionRadius)
                            continue;

                        // Check angle (cone detection)
                        float3 toTargetNorm = math.normalize(toTarget);
                        float dot = math.dot(forward, toTargetNorm);
                        float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));

                        if (angle > abilityRef.DetectionAngle * 0.5f)
                            continue;

                        // Line of sight check
                        var rayInput = new RaycastInput
                        {
                            Start = position,
                            End = interactableTransform.Position + math.up() * 0.5f,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = ~0u,
                                CollidesWith = ~0u,
                                GroupIndex = 0
                            }
                        };

                        if (physicsWorld.CastRay(rayInput, out var hit))
                        {
                            if (hit.Entity != interactableEntity && hit.Fraction < 0.95f)
                                continue; // Blocked
                        }

                        // Score: lower is better (closer + more centered + higher priority)
                        float score = distance + angle * 0.1f - interactable.Priority;

                        // Sticky target: bonus for current target to prevent flickering
                        if (interactableEntity == previousTarget)
                            score -= StickyTargetBonus;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestTarget = interactableEntity;
                        }

                    } while (cellToEntities.TryGetNextValue(out interactableEntity, ref iterator));
                }

                abilityRef.TargetEntity = bestTarget;
            }
        }
    }
}
