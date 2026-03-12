using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Reads `NetworkedAudioBufferElement` buffers on entities (populated by network adapter
    /// or the local publisher) and plays them through the AudioManager. Dedupes quick
    /// repeat events by hashing event contents and enforcing a short cooldown window.
    /// EPIC 15.27 Phase 8: Configurable dedup window (was hardcoded 0.18s).
    /// Default 8 frames (~0.27s at 30Hz) for footsteps.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NetworkedAudioPlaybackSystem : SystemBase
    {
        AudioManager _audioManager;
        Dictionary<int, double> _lastPlayed = new Dictionary<int, double>();

        // EPIC 15.27 Phase 8: Configurable dedup window (was hardcoded 0.18s)
        public static double FootstepDedupeWindow = 0.27; // ~8 frames at 30Hz
        public static double CombatDedupeWindow = 0.1;    // ~3 frames at 30Hz

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _audioManager = Object.FindAnyObjectByType<AudioManager>();
        }

        protected override void OnUpdate()
        {
            if (_audioManager == null)
            {
                _audioManager = Object.FindAnyObjectByType<AudioManager>();
                if (_audioManager == null) return;
            }

            double now = SystemAPI.Time.ElapsedTime;

            // Iterate all entities that have a NetworkedAudioBufferElement buffer using a main-thread
            // EntityQuery to avoid the now-deprecated Entities.ForEach API.
            var query = GetEntityQuery(ComponentType.ReadWrite<NetworkedAudioBufferElement>());
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (int ei = 0; ei < entities.Length; ++ei)
                {
                    var e = entities[ei];
                    var buf = EntityManager.GetBuffer<NetworkedAudioBufferElement>(e);
                    for (int i = 0; i < buf.Length; ++i)
                    {
                        var el = buf[i];
                        // Hash event content: material + quantized position + stance + foot
                        int hx = el.MaterialId;
                        int px = (int)math.round(el.Position.x * 10);
                        int py = (int)math.round(el.Position.y * 10);
                        int pz = (int)math.round(el.Position.z * 10);
                        int key = hx * 73856093 ^ (px << 7) ^ (py << 13) ^ (pz << 19) ^ (el.Stance << 3) ^ el.FootIndex;

                        if (_lastPlayed.TryGetValue(key, out var t))
                        {
                            // EPIC 15.27 Phase 8: Use configurable dedup window
                            if (now - t < FootstepDedupeWindow) continue;
                        }

                        // Play through AudioManager on main thread
                        _audioManager.PlayFootstep(el.MaterialId, new Vector3(el.Position.x, el.Position.y, el.Position.z), el.Stance);
                        _lastPlayed[key] = now;
                    }

                    // Clear buffer after processing
                    buf.Clear();
                }
            }

            // Prune old entries periodically
            var keysToRemove = new List<int>();
            foreach (var kv in _lastPlayed)
            {
                if (now - kv.Value > 2.0) keysToRemove.Add(kv.Key);
            }
            foreach (var k in keysToRemove) _lastPlayed.Remove(k);
        }
    }
}
