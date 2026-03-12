using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Targeting;
using DIG.Combat.UI;
using Player.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19 + 15.33: Selects target from threat table using configurable selection mode.
    /// Supports 7 modes: HighestThreat, WeightedScore, Nearest, LastAttacker, LowestHealth, Random, Defender.
    /// Also manages HasAggroOn component for UI visibility.
    ///
    /// Features: ThreatFixate override, hysteresis, target switch cooldown, random switch chance.
    /// Runs after ThreatDecaySystem in LateSimulationSystemGroup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatDecaySystem))]
    [BurstCompile]
    public partial struct AggroTargetSelectorSystem : ISystem
    {
        ComponentLookup<Health> _healthLookup;
        ComponentLookup<ThreatFixate> _fixateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
            _healthLookup = state.GetComponentLookup<Health>(true);
            _fixateLookup = state.GetComponentLookup<ThreatFixate>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _fixateLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (config, aggroState, threatBuffer, transform, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, RefRW<AggroState>, DynamicBuffer<ThreatEntry>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                var threats = threatBuffer;
                var cfg = config.ValueRO;
                float3 myPos = transform.ValueRO.Position;

                aggroState.ValueRW.TimeSinceLastSwitch += dt;

                Entity selectedEntity = Entity.Null;
                float selectedScore = 0f;
                float3 selectedPos = float3.zero;
                bool forceTarget = false;

                // === ThreatFixate Override ===
                if (_fixateLookup.HasComponent(entity) &&
                    _fixateLookup.IsComponentEnabled(entity))
                {
                    var fixate = _fixateLookup[entity];
                    if (fixate.FixatedTarget != Entity.Null && state.EntityManager.Exists(fixate.FixatedTarget))
                    {
                        selectedEntity = fixate.FixatedTarget;
                        forceTarget = true;
                        for (int t = 0; t < threats.Length; t++)
                        {
                            if (threats[t].SourceEntity == fixate.FixatedTarget)
                            {
                                selectedPos = threats[t].LastKnownPosition;
                                selectedScore = threats[t].ThreatValue;
                                break;
                            }
                        }
                    }
                }

                // === Normal selection (no fixate) ===
                if (!forceTarget)
                {
                    if (threats.Length == 0)
                    {
                        // selectedEntity stays Entity.Null → will clear aggro below
                    }
                    else
                    {
                        Entity currentLeader = aggroState.ValueRO.CurrentThreatLeader;

                        // === Random switch chance (per-second probability) ===
                        bool randomSwitched = false;
                        if (cfg.RandomSwitchChance > 0f && threats.Length > 1 && currentLeader != Entity.Null)
                        {
                            uint seed = (uint)(entity.Index + 1) * 747796405u + (uint)(elapsedTime * 1000f);
                            var rng = new Unity.Mathematics.Random(math.max(seed, 1u));
                            if (rng.NextFloat() < cfg.RandomSwitchChance * dt)
                            {
                                int idx = rng.NextInt(0, threats.Length);
                                if (threats[idx].SourceEntity == currentLeader)
                                    idx = (idx + 1) % threats.Length;

                                selectedEntity = threats[idx].SourceEntity;
                                selectedScore = threats[idx].ThreatValue;
                                selectedPos = threats[idx].LastKnownPosition;
                                forceTarget = true;
                                randomSwitched = true;
                            }
                        }

                        if (!randomSwitched)
                        {
                            // === Mode dispatch ===
                            switch (cfg.SelectionMode)
                            {
                                case TargetSelectionMode.HighestThreat:
                                case TargetSelectionMode.Defender:
                                    SelectHighestThreat(threats, out selectedEntity, out selectedScore, out selectedPos);
                                    break;

                                case TargetSelectionMode.Nearest:
                                    SelectNearest(threats, myPos, out selectedEntity, out selectedScore, out selectedPos);
                                    break;

                                case TargetSelectionMode.LastAttacker:
                                    SelectLastAttacker(threats, out selectedEntity, out selectedScore, out selectedPos);
                                    break;

                                case TargetSelectionMode.LowestHealth:
                                    SelectLowestHealth(threats, ref _healthLookup, out selectedEntity, out selectedScore, out selectedPos);
                                    break;

                                case TargetSelectionMode.Random:
                                {
                                    uint rseed = (uint)(entity.Index + 1) * 2654435761u + (uint)(elapsedTime * 100f);
                                    SelectRandom(threats, rseed, out selectedEntity, out selectedScore, out selectedPos);
                                    break;
                                }

                                case TargetSelectionMode.WeightedScore:
                                    SelectWeightedScore(threats, myPos, cfg, ref _healthLookup,
                                        out selectedEntity, out selectedScore, out selectedPos);
                                    break;

                                default:
                                    SelectHighestThreat(threats, out selectedEntity, out selectedScore, out selectedPos);
                                    break;
                            }

                            // === Hysteresis (HighestThreat, WeightedScore, Defender only) ===
                            bool useHysteresis = cfg.SelectionMode == TargetSelectionMode.HighestThreat ||
                                                 cfg.SelectionMode == TargetSelectionMode.WeightedScore ||
                                                 cfg.SelectionMode == TargetSelectionMode.Defender;

                            if (useHysteresis && currentLeader != Entity.Null &&
                                selectedEntity != currentLeader)
                            {
                                float currentScore = 0f;
                                float3 currentPos = float3.zero;
                                bool foundCurrent = false;

                                for (int t = 0; t < threats.Length; t++)
                                {
                                    if (threats[t].SourceEntity == currentLeader)
                                    {
                                        if (cfg.SelectionMode == TargetSelectionMode.WeightedScore)
                                            currentScore = ScoreWeighted(threats[t], myPos, cfg, ref _healthLookup);
                                        else
                                            currentScore = threats[t].ThreatValue;
                                        currentPos = threats[t].LastKnownPosition;
                                        foundCurrent = true;
                                        break;
                                    }
                                }

                                if (foundCurrent && selectedScore <= currentScore * cfg.HysteresisRatio)
                                {
                                    // Keep current target — new threat doesn't exceed threshold
                                    selectedEntity = currentLeader;
                                    selectedScore = currentScore;
                                    selectedPos = currentPos;
                                }
                            }

                            // === Target Switch Cooldown ===
                            if (cfg.TargetSwitchCooldown > 0f &&
                                selectedEntity != currentLeader &&
                                currentLeader != Entity.Null &&
                                aggroState.ValueRO.TimeSinceLastSwitch < cfg.TargetSwitchCooldown)
                            {
                                for (int t = 0; t < threats.Length; t++)
                                {
                                    if (threats[t].SourceEntity == currentLeader)
                                    {
                                        selectedEntity = currentLeader;
                                        selectedScore = threats[t].ThreatValue;
                                        selectedPos = threats[t].LastKnownPosition;
                                        break;
                                    }
                                }
                                // If current leader gone from table, allow switch despite cooldown
                            }
                        }
                    }
                }

                // === Update AggroState ===
                bool isNowAggroed = selectedEntity != Entity.Null;
                bool switched = selectedEntity != aggroState.ValueRO.CurrentThreatLeader;

                aggroState.ValueRW.CurrentThreatLeader = selectedEntity;
                aggroState.ValueRW.CurrentLeaderThreat = selectedScore;
                aggroState.ValueRW.IsAggroed = isNowAggroed;

                if (switched && isNowAggroed)
                    aggroState.ValueRW.TimeSinceLastSwitch = 0f;

                if (!isNowAggroed)
                    aggroState.ValueRW.TimeSinceLastValidTarget += dt;
                else
                    aggroState.ValueRW.TimeSinceLastValidTarget = 0f;

                // === Update TargetData ===
                if (SystemAPI.HasComponent<TargetData>(entity))
                {
                    var targetData = SystemAPI.GetComponentRW<TargetData>(entity);
                    targetData.ValueRW.TargetEntity = selectedEntity;
                    targetData.ValueRW.TargetPoint = selectedPos;
                    targetData.ValueRW.HasValidTarget = isNowAggroed;
                    targetData.ValueRW.TargetDistance = isNowAggroed
                        ? math.length(selectedPos - myPos)
                        : 0f;
                }

                // === Update HasAggroOn (UI) ===
                if (isNowAggroed)
                {
                    if (SystemAPI.HasComponent<HasAggroOn>(entity))
                    {
                        var hasAggro = SystemAPI.GetComponentRW<HasAggroOn>(entity);
                        hasAggro.ValueRW.TargetPlayer = selectedEntity;
                    }
                    else
                    {
                        ecb.AddComponent(entity, new HasAggroOn { TargetPlayer = selectedEntity });
                    }
                }
                else
                {
                    if (SystemAPI.HasComponent<HasAggroOn>(entity))
                    {
                        ecb.RemoveComponent<HasAggroOn>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // === Selection Methods ===

        static void SelectHighestThreat(DynamicBuffer<ThreatEntry> threats,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = float.MinValue;
            bestPos = float3.zero;

            for (int t = 0; t < threats.Length; t++)
            {
                if (threats[t].ThreatValue > bestScore)
                {
                    bestScore = threats[t].ThreatValue;
                    bestEntity = threats[t].SourceEntity;
                    bestPos = threats[t].LastKnownPosition;
                }
            }
        }

        static void SelectNearest(DynamicBuffer<ThreatEntry> threats, float3 myPos,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = float.MinValue;
            bestPos = float3.zero;
            float bestDist = float.MaxValue;

            for (int t = 0; t < threats.Length; t++)
            {
                float dist = math.distance(myPos, threats[t].LastKnownPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestScore = -dist;
                    bestEntity = threats[t].SourceEntity;
                    bestPos = threats[t].LastKnownPosition;
                }
            }
        }

        static void SelectLastAttacker(DynamicBuffer<ThreatEntry> threats,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = float.MinValue;
            bestPos = float3.zero;

            for (int t = 0; t < threats.Length; t++)
            {
                float score = -threats[t].TimeSinceVisible;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEntity = threats[t].SourceEntity;
                    bestPos = threats[t].LastKnownPosition;
                }
            }
        }

        static void SelectLowestHealth(DynamicBuffer<ThreatEntry> threats,
            ref ComponentLookup<Health> healthLookup,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = float.MinValue;
            bestPos = float3.zero;
            float lowestHp = float.MaxValue;

            for (int t = 0; t < threats.Length; t++)
            {
                float hpNorm = 1f;
                if (healthLookup.HasComponent(threats[t].SourceEntity))
                {
                    var hp = healthLookup[threats[t].SourceEntity];
                    hpNorm = hp.Max > 0f ? hp.Current / hp.Max : 1f;
                }

                if (hpNorm < lowestHp)
                {
                    lowestHp = hpNorm;
                    bestScore = -hpNorm;
                    bestEntity = threats[t].SourceEntity;
                    bestPos = threats[t].LastKnownPosition;
                }
            }
        }

        static void SelectRandom(DynamicBuffer<ThreatEntry> threats, uint seed,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = 0f;
            bestPos = float3.zero;

            if (threats.Length == 0) return;

            var rng = new Unity.Mathematics.Random(math.max(seed, 1u));
            int idx = rng.NextInt(0, threats.Length);
            bestEntity = threats[idx].SourceEntity;
            bestScore = threats[idx].ThreatValue;
            bestPos = threats[idx].LastKnownPosition;
        }

        static void SelectWeightedScore(DynamicBuffer<ThreatEntry> threats, float3 myPos,
            AggroConfig cfg, ref ComponentLookup<Health> healthLookup,
            out Entity bestEntity, out float bestScore, out float3 bestPos)
        {
            bestEntity = Entity.Null;
            bestScore = float.MinValue;
            bestPos = float3.zero;

            for (int t = 0; t < threats.Length; t++)
            {
                float score = ScoreWeighted(threats[t], myPos, cfg, ref healthLookup);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEntity = threats[t].SourceEntity;
                    bestPos = threats[t].LastKnownPosition;
                }
            }
        }

        static float ScoreWeighted(ThreatEntry entry, float3 myPos, AggroConfig cfg,
            ref ComponentLookup<Health> healthLookup)
        {
            float threatW = math.max(1f - cfg.DistanceWeight - cfg.HealthWeight - cfg.RecencyWeight, 0f);

            float dist = math.max(math.distance(myPos, entry.LastKnownPosition), 0.1f);
            float distScore = 1f / dist;

            float hpScore = 0f;
            if (healthLookup.HasComponent(entry.SourceEntity))
            {
                var hp = healthLookup[entry.SourceEntity];
                hpScore = hp.Max > 0f ? 1f - (hp.Current / hp.Max) : 0f;
            }

            float recencyScore = 1f / math.max(entry.TimeSinceVisible + 0.1f, 0.1f);

            return entry.ThreatValue * threatW +
                   distScore * cfg.DistanceWeight * 100f +
                   hpScore * cfg.HealthWeight * 100f +
                   recencyScore * cfg.RecencyWeight * 100f;
        }
    }
}
