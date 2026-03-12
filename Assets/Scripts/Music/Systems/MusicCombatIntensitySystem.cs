using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Collections;
using DIG.Aggro.Components;
using DIG.Combat.Components;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Reads AlertState from nearby AI to compute combat intensity (0-1).
    /// Also centralizes IsInCombat flag and processes MusicBossOverride transient entities.
    /// Falls back to binary CombatState intensity when AlertState is unavailable (remote client).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MusicZoneSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicCombatIntensitySystem : SystemBase
    {
        private EntityQuery _alertQuery;
        private EntityQuery _bossOverrideQuery;

        // Reusable list — persistent allocation avoids per-frame alloc overhead
        private NativeList<DistanceWeight> _candidates;

        private struct DistanceWeight
        {
            public float DistSq;
            public float Weight;
            public bool IsCombat;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            RequireForUpdate<MusicConfig>();
            _alertQuery = GetEntityQuery(
                ComponentType.ReadOnly<AlertState>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            _bossOverrideQuery = GetEntityQuery(ComponentType.ReadOnly<MusicBossOverride>());
            _candidates = new NativeList<DistanceWeight>(32, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_candidates.IsCreated) _candidates.Dispose();
        }

        protected override void OnUpdate()
        {
            // Find local player
            float3 playerPos = float3.zero;
            bool foundPlayer = false;
            bool localPlayerInCombat = false;

            foreach (var (transform, combatState) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<CombatState>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = transform.ValueRO.Position;
                localPlayerInCombat = combatState.ValueRO.IsInCombat;
                foundPlayer = true;
                break;
            }

            // Fallback: try without CombatState
            if (!foundPlayer)
            {
                foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<GhostOwnerIsLocal>())
                {
                    playerPos = transform.ValueRO.Position;
                    foundPlayer = true;
                    break;
                }
            }
            if (!foundPlayer) return;

            var musicState = SystemAPI.GetSingleton<MusicState>();
            var config = SystemAPI.GetSingleton<MusicConfig>();
            float dt = SystemAPI.Time.DeltaTime;

            // Process boss overrides first
            ProcessBossOverrides(ref musicState);

            // Compute combat intensity from AlertState
            float targetIntensity;
            bool anyCombat = false;
            int alertCount = _alertQuery.CalculateEntityCount();

            if (alertCount > 0)
            {
                float rangeSq = config.MaxCombatIntensityRange * config.MaxCombatIntensityRange;

                // Batched reads (Allocator.Temp is stack-based, very cheap)
                var alertStates = _alertQuery.ToComponentDataArray<AlertState>(Allocator.Temp);
                var alertTransforms = _alertQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                // Collect in-range candidates with distance + weight
                _candidates.Clear();
                for (int i = 0; i < alertStates.Length; i++)
                {
                    float distSq = math.distancesq(playerPos, alertTransforms[i].Position);
                    if (distSq > rangeSq) continue;

                    int level = alertStates[i].AlertLevel;
                    float weight = level switch
                    {
                        AlertState.COMBAT => config.IntensityWeightCombat,
                        AlertState.SEARCHING => config.IntensityWeightSearching,
                        AlertState.SUSPICIOUS => config.IntensityWeightSuspicious,
                        AlertState.CURIOUS => config.IntensityWeightCurious,
                        _ => 0f
                    };

                    if (weight > 0f)
                    {
                        _candidates.Add(new DistanceWeight
                        {
                            DistSq = distSq,
                            Weight = weight,
                            IsCombat = level == AlertState.COMBAT
                        });
                    }

                    if (level == AlertState.COMBAT) anyCombat = true;
                }

                alertStates.Dispose();
                alertTransforms.Dispose();

                // Sort by distance ascending — nearest enemies contribute first
                if (_candidates.Length > 1)
                    _candidates.Sort(new DistWeightComparer());

                // Accumulate top N contributors
                float rawIntensity = 0f;
                int cap = math.min(_candidates.Length, config.MaxIntensityContributors);
                for (int i = 0; i < cap; i++)
                {
                    rawIntensity += _candidates[i].Weight;
                    if (_candidates[i].IsCombat) anyCombat = true;
                }

                targetIntensity = math.saturate(rawIntensity / config.MaxIntensityContributors);
            }
            else
            {
                // Fallback: no AlertState data — use binary combat flag
                targetIntensity = localPlayerInCombat ? 0.8f : 0f;
                anyCombat = localPlayerInCombat;
            }

            // Boss override forces max intensity
            if (musicState.BossOverrideTrackId != 0)
            {
                targetIntensity = 1f;
                anyCombat = true;
            }

            // Smooth intensity
            musicState.CombatIntensity = targetIntensity;
            musicState.SmoothedIntensity = math.lerp(musicState.SmoothedIntensity, targetIntensity, dt * config.CombatFadeSpeed);
            musicState.IsInCombat = anyCombat || localPlayerInCombat;

            SystemAPI.SetSingleton(musicState);
        }

        private void ProcessBossOverrides(ref MusicState musicState)
        {
            if (_bossOverrideQuery.IsEmpty) return;

            var overrides = _bossOverrideQuery.ToComponentDataArray<MusicBossOverride>(Allocator.Temp);
            var entities = _bossOverrideQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].Activate)
                {
                    musicState.BossOverrideTrackId = overrides[i].TrackId;
                    musicState.TargetTrackId = overrides[i].TrackId;
                }
                else
                {
                    musicState.BossOverrideTrackId = 0;
                }

                EntityManager.DestroyEntity(entities[i]);
            }

            overrides.Dispose();
            entities.Dispose();
        }

        private struct DistWeightComparer : IComparer<DistanceWeight>
        {
            public int Compare(DistanceWeight a, DistanceWeight b)
            {
                return a.DistSq.CompareTo(b.DistSq);
            }
        }
    }
}
