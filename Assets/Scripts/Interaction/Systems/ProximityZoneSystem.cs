using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 5: Detects entities inside proximity zones and manages occupant tracking.
    ///
    /// Each frame:
    /// 1. For each ProximityZone entity, checks all CanInteract entities for distance
    /// 2. Adds new occupants entering the radius, removes those who left or were destroyed
    /// 3. Updates TimeInZone per occupant
    /// 4. Ticks EffectTimer and sets EffectTickReady when an effect should fire
    ///
    /// Game-specific systems (in DIG.Player or game assemblies) read ProximityZone.EffectTickReady
    /// and ProximityZoneOccupant buffer to apply actual effects (healing, damage, buffs).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ProximityZoneSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProximityZone>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Collect all interactable entities (players) with positions
            var interactorQuery = SystemAPI.QueryBuilder()
                .WithAll<CanInteract, LocalTransform, Simulate>()
                .Build();

            var interactorEntities = interactorQuery.ToEntityArray(Allocator.Temp);
            var interactorTransforms = interactorQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            foreach (var (zone, zoneTransform, zoneEntity) in
                     SystemAPI.Query<RefRW<ProximityZone>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                var occupants = SystemAPI.GetBuffer<ProximityZoneOccupant>(zoneEntity);
                float3 zonePos = zoneTransform.ValueRO.Position;
                float radiusSq = zone.ValueRO.Radius * zone.ValueRO.Radius;

                // --- Remove stale occupants (left radius or destroyed) ---
                for (int i = occupants.Length - 1; i >= 0; i--)
                {
                    var occupant = occupants[i];
                    bool stillValid = false;

                    for (int j = 0; j < interactorEntities.Length; j++)
                    {
                        if (interactorEntities[j] == occupant.OccupantEntity)
                        {
                            float distSq = math.distancesq(interactorTransforms[j].Position, zonePos);
                            if (distSq <= radiusSq)
                            {
                                // Still inside — update time
                                stillValid = true;
                                var updated = occupant;
                                updated.TimeInZone += deltaTime;
                                occupants[i] = updated;
                            }
                            break;
                        }
                    }

                    if (!stillValid)
                    {
                        occupants.RemoveAtSwapBack(i);
                    }
                }

                // --- Add new occupants ---
                for (int j = 0; j < interactorEntities.Length; j++)
                {
                    float distSq = math.distancesq(interactorTransforms[j].Position, zonePos);
                    if (distSq > radiusSq)
                        continue;

                    // Check max occupants
                    if (zone.ValueRO.MaxOccupants > 0 && occupants.Length >= zone.ValueRO.MaxOccupants)
                        break;

                    // Check if already tracked
                    bool alreadyTracked = false;
                    for (int k = 0; k < occupants.Length; k++)
                    {
                        if (occupants[k].OccupantEntity == interactorEntities[j])
                        {
                            alreadyTracked = true;
                            break;
                        }
                    }

                    if (!alreadyTracked)
                    {
                        occupants.Add(new ProximityZoneOccupant
                        {
                            OccupantEntity = interactorEntities[j],
                            TimeInZone = 0
                        });
                    }
                }

                // --- Update occupant count ---
                zone.ValueRW.CurrentOccupantCount = occupants.Length;

                // --- Tick effect timer ---
                zone.ValueRW.EffectTickReady = false;

                if (occupants.Length > 0 && zone.ValueRO.Effect != ProximityEffect.None)
                {
                    if (zone.ValueRO.EffectInterval <= 0)
                    {
                        // Continuous: tick every frame
                        zone.ValueRW.EffectTickReady = true;
                    }
                    else
                    {
                        zone.ValueRW.EffectTimer += deltaTime;
                        if (zone.ValueRO.EffectTimer >= zone.ValueRO.EffectInterval)
                        {
                            zone.ValueRW.EffectTickReady = true;
                            zone.ValueRW.EffectTimer -= zone.ValueRO.EffectInterval;
                        }
                    }
                }
                else
                {
                    // No occupants — reset timer so first tick fires immediately on entry
                    zone.ValueRW.EffectTimer = 0;
                }
            }

            interactorEntities.Dispose();
            interactorTransforms.Dispose();
        }
    }
}
