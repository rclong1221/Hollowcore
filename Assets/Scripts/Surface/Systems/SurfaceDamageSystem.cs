using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using DIG.Combat.UI;
using DIG.Combat.Systems;
using DIG.Combat.Utility;
using DIG.Targeting.Theming;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 6: Applies damage-over-time to entities inside SurfaceDamageZone
    /// trigger volumes whose GroundSurfaceState matches the zone's required SurfaceID.
    /// Server-authoritative only.
    /// Supports TickInterval (throttled damage application) and RampUpDuration (gradual scaling).
    /// Optimized: iterates entities once (outer), zones inner (smaller set).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SurfaceDamageSystem : SystemBase
    {
        private ComponentLookup<PlayerTag> _playerTagLookup;
        private EntityQuery _zoneQuery;

        // Tracks (entityIndex * 10000 + zoneIndex) → (timeInZone, timeSinceLastTick)
        private NativeHashMap<long, float2> _contactTracking;

        // EPIC 16.11: Visual bridge — RPC archetype for broadcasting damage visuals to remote clients
        private bool _isServer;
        private EntityArchetype _rpcArchetype;

        protected override void OnCreate()
        {
            RequireForUpdate<SurfaceDamageZone>();
            _playerTagLookup = GetComponentLookup<PlayerTag>(true);
            _zoneQuery = GetEntityQuery(
                ComponentType.ReadOnly<SurfaceDamageZone>(),
                ComponentType.ReadOnly<LocalToWorld>());
            _contactTracking = new NativeHashMap<long, float2>(32, Allocator.Persistent);

            // EPIC 16.11: Visual bridge setup
            _isServer = World.Name == "ServerWorld";
            if (_isServer)
            {
                _rpcArchetype = EntityManager.CreateArchetype(
                    typeof(DamageVisualRpc), typeof(SendRpcCommandRequest));
            }
        }

        protected override void OnDestroy()
        {
            if (_contactTracking.IsCreated)
                _contactTracking.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_zoneQuery.IsEmpty) return;

            // EPIC 16.10 Phase 8: Respect feature toggles
            if (SystemAPI.TryGetSingleton<SurfaceGameplayToggles>(out var toggles) &&
                !toggles.EnableSurfaceDamageZones)
                return;

            _playerTagLookup.Update(this);
            float dt = SystemAPI.Time.DeltaTime;

            // EPIC 16.11: ECB for RPC entity creation — no structural changes during iteration
            EntityCommandBuffer ecb = default;
            if (_isServer)
                ecb = new EntityCommandBuffer(Allocator.Temp);

            // Copy zone data once (small array — typically 0-5 zones)
            var zones = _zoneQuery.ToComponentDataArray<SurfaceDamageZone>(Allocator.Temp);
            var zonePositions = _zoneQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var zoneEntities = _zoneQuery.ToEntityArray(Allocator.Temp);
            int zoneCount = zones.Length;

            // Track which contacts are active this frame for cleanup
            var activeContacts = new NativeHashSet<long>(32, Allocator.Temp);

            // Iterate entities once (outer), check all zones (inner)
            foreach (var (groundSurface, health, transform, entity) in
                SystemAPI.Query<RefRO<GroundSurfaceState>, RefRW<Health>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Must be grounded
                if (!groundSurface.ValueRO.IsGrounded)
                    continue;

                bool isPlayer = _playerTagLookup.HasComponent(entity);
                float3 entityPos = transform.ValueRO.Position;

                for (int z = 0; z < zoneCount; z++)
                {
                    var zone = zones[z];

                    // Check NPC filter
                    if (!zone.AffectsNPCs && !isPlayer)
                        continue;

                    // Surface match check
                    if (zone.RequiredSurfaceId != SurfaceID.Default &&
                        groundSurface.ValueRO.SurfaceId != zone.RequiredSurfaceId)
                        continue;

                    // Distance check
                    float dist = math.distance(entityPos, zonePositions[z].Position);
                    if (dist > 50f) continue;

                    if (zone.DamagePerSecond <= 0f) continue;

                    // Track contact state for ramp-up and tick interval
                    long contactKey = (long)entity.Index * 10000L + z;
                    activeContacts.Add(contactKey);

                    float2 state; // x = timeInZone, y = timeSinceLastTick
                    if (!_contactTracking.TryGetValue(contactKey, out state))
                        state = float2.zero;

                    state.x += dt; // timeInZone
                    state.y += dt; // timeSinceLastTick

                    // Tick interval: only apply damage when enough time has elapsed
                    float tickInterval = math.max(zone.TickInterval, dt); // prevent zero-division
                    if (state.y >= tickInterval)
                    {
                        // Ramp-up scaling
                        float rampScale = 1.0f;
                        if (zone.RampUpDuration > 0f)
                            rampScale = math.saturate(state.x / zone.RampUpDuration);

                        float damage = zone.DamagePerSecond * tickInterval * rampScale;
                        health.ValueRW.Current -= damage;
                        state.y = 0f; // reset tick timer

                        // EPIC 16.11: Bridge to visual pipeline for damage numbers
                        if (damage > 0f)
                        {
                            var visualData = new DamageVisualData
                            {
                                Damage = damage,
                                HitPosition = entityPos + new float3(0, 1.5f, 0),
                                HitType = HitType.Hit,
                                DamageType = DamageTypeConverter.ToTheme(zone.DamageType),
                                Flags = ResultFlags.None,
                                IsDOT = true
                            };
                            DamageVisualQueue.Enqueue(visualData);

                            if (_isServer)
                            {
                                var rpcEntity = ecb.CreateEntity(_rpcArchetype);
                                ecb.SetComponent(rpcEntity, new DamageVisualRpc
                                {
                                    Damage = visualData.Damage,
                                    HitPosition = visualData.HitPosition,
                                    HitType = (byte)visualData.HitType,
                                    DamageType = (byte)visualData.DamageType,
                                    Flags = (byte)visualData.Flags,
                                    IsDOT = 1
                                });
                            }
                        }
                    }

                    _contactTracking[contactKey] = state;
                    break; // Only apply one zone's damage per frame per entity
                }
            }

            // Remove stale contact entries (entity left the zone)
            var keys = _contactTracking.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (!activeContacts.Contains(keys[i]))
                    _contactTracking.Remove(keys[i]);
            }
            keys.Dispose();

            // EPIC 16.11: Playback RPC entity creation after iteration is complete
            if (_isServer)
            {
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }

            activeContacts.Dispose();
            zones.Dispose();
            zonePositions.Dispose();
            zoneEntities.Dispose();
        }
    }
}
