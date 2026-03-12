using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using Audio.Config;

namespace Audio.Systems
{
    /// <summary>
    /// Scores all active AudioSourceState entities by priority, distance, and occlusion.
    /// Enforces voice budget by muting lowest-scored sources.
    /// Implements audio LOD tiers for distance-based quality reduction.
    /// EPIC 15.27 Phase 5.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioOcclusionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AudioPrioritySystem : SystemBase
    {
        private AudioLODConfig _lodConfig;
        private AudioSourcePool _pool;

        private struct ScoredSource
        {
            public Entity Entity;
            public AudioSourceState State;
            public AudioEmitter Emitter;
            public float Score;
            public float Distance;
        }

        // Reusable list to avoid allocation
        private readonly System.Collections.Generic.List<ScoredSource> _scored =
            new System.Collections.Generic.List<ScoredSource>(64);

        protected override void OnUpdate()
        {
            if (_lodConfig == null)
            {
                _lodConfig = Object.FindAnyObjectByType<AudioLODConfigHolder>()?.Config;
                if (_lodConfig == null) return;
            }

            if (_pool == null)
            {
                _pool = AudioSourcePool.Instance;
                if (_pool == null) return;
            }

            // Get listener position
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null) return;
            Vector3 listenerPos = listener.transform.position;

            _scored.Clear();

            // Score all active sources
            foreach (var (state, emitter, ltw, entity) in
                     SystemAPI.Query<AudioSourceState, RefRO<AudioEmitter>, RefRO<LocalToWorld>>()
                     .WithEntityAccess())
            {
                if (state.Source == null) continue;

                float distance = Vector3.Distance(listenerPos, (Vector3)ltw.ValueRO.Position);
                float occlusionBonus = state.OcclusionFactor > 0.5f ? 1.2f : 0.8f;
                float score = emitter.ValueRO.Priority * (1f / (1f + distance * _lodConfig.DistanceFalloff)) * occlusionBonus;

                _scored.Add(new ScoredSource
                {
                    Entity = entity,
                    State = state,
                    Emitter = emitter.ValueRO,
                    Score = score,
                    Distance = distance
                });
            }

            int voiceBudget = _lodConfig.GetVoiceBudget();

            // Sort by score descending (highest score = keep)
            _scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            int activeVoices = 0;
            int culledVoices = 0;

            for (int i = 0; i < _scored.Count; i++)
            {
                var s = _scored[i];

                // Exempt high-priority sources
                bool isExempt = s.Emitter.Priority >= _lodConfig.ExemptPriorityThreshold;

                // Apply LOD tier
                var tier = _lodConfig.GetTier(s.Distance);

                if (tier == AudioLODTier.Culled && !isExempt)
                {
                    // Beyond max distance — mute and return to pool
                    if (s.State.Source.volume > 0f)
                        s.State.Source.volume = 0f;
                    culledVoices++;
                    continue;
                }

                // Voice budget enforcement
                if (!isExempt && activeVoices >= voiceBudget)
                {
                    s.State.Source.volume = 0f;
                    culledVoices++;
                    continue;
                }

                // Source is active
                activeVoices++;

                // Apply LOD quality adjustments
                switch (tier)
                {
                    case AudioLODTier.Reduced:
                        // Mono downmix at reduced tier
                        if (_lodConfig.DownmixAtReduced)
                            s.State.Source.spread = 0f; // Collapse to mono
                        break;

                    case AudioLODTier.Minimal:
                        s.State.Source.spread = 0f;
                        // Skip reverb send at minimal
                        s.State.Source.reverbZoneMix = 0f;
                        break;

                    case AudioLODTier.Full:
                    default:
                        // Full quality — restore defaults
                        s.State.Source.spread = 1f;
                        s.State.Source.reverbZoneMix = 1f;
                        break;
                }
            }

            // Update telemetry
            AudioTelemetry.ActiveVoiceCount = activeVoices;
            AudioTelemetry.CulledVoiceCount = culledVoices;
        }
    }

    /// <summary>
    /// MonoBehaviour holder for AudioLODConfig ScriptableObject reference.
    /// </summary>
    public class AudioLODConfigHolder : MonoBehaviour
    {
        public AudioLODConfig Config;
    }
}
