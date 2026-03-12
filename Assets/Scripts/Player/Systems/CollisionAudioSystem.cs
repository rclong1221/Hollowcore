using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using DIG.Player.Components;
using DIG.Performance;
using Player.Bridges;
using System.Collections.Generic;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Consumes CollisionEvent buffers and plays collision audio via CollisionAudioBridge (Epic 7.4.4).
    /// Runs client-side only in PresentationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CollisionEventClearSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class CollisionAudioSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentation;
        private readonly Dictionary<Entity, CollisionAudioBridge> _bridgeCache = new Dictionary<Entity, CollisionAudioBridge>();

        protected override void OnCreate()
        {
            _presentation = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate<PlayerCollisionSettings>();
        }

        protected override void OnDestroy()
        {
            _bridgeCache.Clear();
        }

        protected override void OnUpdate()
        {
            // Epic 7.7.1: Profile audio system
            using (CollisionProfilerMarkers.Audio.Auto())
            {
            // Epic 7.7.4: Check quality settings - disable audio/VFX at Low quality
            if (SystemAPI.HasSingleton<CollisionQualitySettings>())
            {
                var qualitySettings = SystemAPI.GetSingleton<CollisionQualitySettings>();
                if (!qualitySettings.ShouldPlayAudioVFX)
                    return;
            }
            
            if (_presentation == null)
            {
                _presentation = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentation == null)
                    return;
            }

            var settings = SystemAPI.GetSingleton<PlayerCollisionSettings>();
            int maxSounds = settings.MaxCollisionSoundsPerFrame;
            if (maxSounds <= 0)
                return;

            int soundsPlayed = 0;

            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<CollisionEvent>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                if (events.Length == 0)
                    continue;

                if (soundsPlayed >= maxSounds)
                    return;

                var go = _presentation.GetGameObjectForEntity(EntityManager, entity);
                if (go == null)
                {
                    _bridgeCache.Remove(entity);
                    continue;
                }

                if (!_bridgeCache.TryGetValue(entity, out var bridge) || bridge == null)
                {
                    bridge = go.GetComponentInChildren<CollisionAudioBridge>();
                    _bridgeCache[entity] = bridge;
                }
                if (bridge == null)
                    continue;

                // Coalesce: pick the strongest event for this entity this frame.
                float bestIntensity = 0f;
                int bestHitDirection = 0;
                Vector3 bestContactPoint = default;

                for (int i = 0; i < events.Length; i++)
                {
                    var ev = events[i];

                    float intensity = Mathf.InverseLerp(settings.CollisionAudioMinForce, settings.CollisionAudioMaxForce, ev.ImpactForce);
                    intensity = Mathf.Clamp01(intensity);
                    if (intensity <= bestIntensity)
                        continue;

                    bestIntensity = intensity;
                    bestHitDirection = ev.HitDirection;
                    bestContactPoint = ev.ContactPoint;
                }

                if (bestIntensity <= 0f)
                    continue;

                if (bridge.CollisionAudioSource != null)
                {
                    bridge.CollisionAudioSource.transform.position = bestContactPoint;
                }

                bridge.PlayCollisionSound(bestIntensity, bestHitDirection);
                soundsPlayed++;
            }
            } // End Audio profiler marker
        }
    }
}
