using System.Collections.Generic;
using DIG.Roguelite;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// On Preparation phase: reads ZoneSequenceSO, uses seed to resolve the zone order.
    /// Handles looping, conditional entries, weighted random, and player choice.
    /// Stores resolved list in a managed singleton for ZoneTransitionSystem to consume.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunLifecycleSystem))]
    public partial class ZoneSequenceResolverSystem : SystemBase
    {
        private ZoneSequenceSO _sequence;
        private readonly List<ResolvedZone> _resolvedZones = new();
        private bool _resolved;
        private byte _loopCount;

        /// <summary>Resolved zone at the given index. Returns null if out of range.</summary>
        public ZoneDefinitionSO GetZoneAtIndex(int index)
        {
            if (index < 0 || index >= _resolvedZones.Count) return null;
            return _resolvedZones[index].Definition;
        }

        /// <summary>Total resolved zone count (may grow if looping).</summary>
        public int ResolvedZoneCount => _resolvedZones.Count;

        /// <summary>Current loop count (0 = first pass).</summary>
        public byte LoopCount => _loopCount;

        /// <summary>Loop difficulty multiplier from the ZoneSequenceSO. 1.0 if no sequence or no looping.</summary>
        public float LoopDifficultyMultiplier => _sequence != null ? _sequence.LoopDifficultyMultiplier : 1f;

        /// <summary>Whether zones have been resolved for the current run.</summary>
        public bool IsResolved => _resolved;

        /// <summary>Register the zone sequence for this run. Call before Preparation phase.</summary>
        public void SetSequence(ZoneSequenceSO sequence) => _sequence = sequence;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            if (!EntityManager.IsComponentEnabled<RunPhaseChangedTag>(runEntity))
                return;

            var run = SystemAPI.GetSingleton<RunState>();

            if (run.Phase == RunPhase.Preparation)
            {
                ResolveSequence(run.Seed, run.AscensionLevel);
            }
            else if (run.Phase == RunPhase.None || run.Phase == RunPhase.Lobby)
            {
                _resolvedZones.Clear();
                _resolved = false;
                _loopCount = 0;
            }
        }

        private void ResolveSequence(uint masterSeed, byte ascensionLevel)
        {
            _resolvedZones.Clear();
            _loopCount = 0;

            if (_sequence == null || _sequence.Layers == null || _sequence.Layers.Count == 0)
            {
                _resolved = true;
                return;
            }

            for (int i = 0; i < _sequence.Layers.Count; i++)
            {
                var layer = _sequence.Layers[i];
                var zoneSeed = RunSeedUtility.DeriveZoneSeed(masterSeed, (byte)i);
                var zone = ResolveLayer(layer, zoneSeed, ascensionLevel, _loopCount);
                if (zone != null)
                {
                    _resolvedZones.Add(new ResolvedZone
                    {
                        Definition = zone,
                        LayerIndex = i,
                        LoopCount = _loopCount
                    });
                }
            }

            _resolved = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ZoneSequence] Resolved {_resolvedZones.Count} zones from '{_sequence.SequenceName}'");
#endif
        }

        /// <summary>
        /// Extend the resolved list by one loop iteration. Called by ZoneTransitionSystem
        /// when the current zone index exceeds the resolved count and looping is enabled.
        /// </summary>
        public void ExtendLoop(uint masterSeed, byte ascensionLevel)
        {
            if (_sequence == null || !_sequence.EnableLooping) return;

            _loopCount++;
            int start = _sequence.LoopStartIndex;
            int layerCount = _sequence.Layers.Count;

            for (int i = start; i < layerCount; i++)
            {
                var layer = _sequence.Layers[i];
                byte seedIndex = (byte)(_resolvedZones.Count & 0xFF);
                var zoneSeed = RunSeedUtility.DeriveZoneSeed(masterSeed, seedIndex);
                var zone = ResolveLayer(layer, zoneSeed, ascensionLevel, _loopCount);
                if (zone != null)
                {
                    _resolvedZones.Add(new ResolvedZone
                    {
                        Definition = zone,
                        LayerIndex = i,
                        LoopCount = _loopCount
                    });
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ZoneSequence] Extended loop {_loopCount}, total zones={_resolvedZones.Count}");
#endif
        }

        private static ZoneDefinitionSO ResolveLayer(ZoneSequenceLayer layer, uint seed, byte ascensionLevel, byte loopCount)
        {
            if (layer.Entries == null || layer.Entries.Count == 0)
                return null;

            switch (layer.Mode)
            {
                case ZoneSelectionMode.Fixed:
                    return layer.Entries[0].Zone;

                case ZoneSelectionMode.WeightedRandom:
                    return ResolveWeightedRandom(layer.Entries, seed, ascensionLevel, loopCount);

                case ZoneSelectionMode.Conditional:
                    return ResolveConditional(layer.Entries, ascensionLevel, loopCount);

                case ZoneSelectionMode.PlayerChoice:
                    // For player choice, resolve all valid candidates. The first is the default
                    // if the player doesn't choose. ZoneTransitionSystem presents the choices.
                    return ResolveWeightedRandom(layer.Entries, seed, ascensionLevel, loopCount);

                default:
                    return layer.Entries[0].Zone;
            }
        }

        private static ZoneDefinitionSO ResolveWeightedRandom(
            List<ZoneSequenceEntry> entries, uint seed, byte ascensionLevel, byte loopCount)
        {
            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.MinAscensionLevel > ascensionLevel) continue;
                if (e.MinLoopCount > loopCount) continue;
                if (e.Zone == null) continue;
                totalWeight += e.Weight;
            }

            if (totalWeight <= 0f)
                return entries[0].Zone;

            var rng = new Random(seed | 1);
            float roll = rng.NextFloat() * totalWeight;
            float acc = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.MinAscensionLevel > ascensionLevel) continue;
                if (e.MinLoopCount > loopCount) continue;
                if (e.Zone == null) continue;

                acc += e.Weight;
                if (roll <= acc)
                    return e.Zone;
            }

            return entries[0].Zone;
        }

        private static ZoneDefinitionSO ResolveConditional(
            List<ZoneSequenceEntry> entries, byte ascensionLevel, byte loopCount)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.MinAscensionLevel > ascensionLevel) continue;
                if (e.MinLoopCount > loopCount) continue;
                if (e.Zone != null)
                    return e.Zone;
            }
            return entries[0].Zone;
        }

        public struct ResolvedZone
        {
            public ZoneDefinitionSO Definition;
            public int LayerIndex;
            public byte LoopCount;
        }
    }
}
