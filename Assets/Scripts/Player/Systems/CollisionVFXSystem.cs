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
    /// Consumes CollisionEvent buffers and spawns impact VFX via CollisionVFXBridge (Epic 7.4.4).
    /// Runs client-side only in PresentationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CollisionEventClearSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class CollisionVFXSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentation;
        private readonly Dictionary<Entity, CollisionVFXBridge> _bridgeCache = new Dictionary<Entity, CollisionVFXBridge>();

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
            // Epic 7.7.1: Profile VFX system
            using (CollisionProfilerMarkers.VFX.Auto())
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

            foreach (var (events, playerState, entity) in SystemAPI
                         .Query<DynamicBuffer<CollisionEvent>, RefRO<PlayerState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                if (events.Length == 0)
                    continue;

                var go = _presentation.GetGameObjectForEntity(EntityManager, entity);
                if (go == null)
                {
                    _bridgeCache.Remove(entity);
                    continue;
                }

                if (!_bridgeCache.TryGetValue(entity, out var bridge) || bridge == null)
                {
                    bridge = go.GetComponentInChildren<CollisionVFXBridge>();
                    _bridgeCache[entity] = bridge;
                }
                if (bridge == null)
                    continue;

                bool isEVA = playerState.ValueRO.Mode == PlayerMode.EVA;

                // Coalesce: pick the strongest non-evaded event for this entity this frame.
                float bestIntensity = 0f;
                Vector3 bestContactPoint = default;

                for (int i = 0; i < events.Length; i++)
                {
                    var ev = events[i];

                    if (ev.HitDirection == 3)
                        continue;

                    float intensity = Mathf.InverseLerp(settings.CollisionAudioMinForce, settings.CollisionAudioMaxForce, ev.ImpactForce);
                    intensity = Mathf.Clamp01(intensity);
                    if (intensity <= bestIntensity)
                        continue;

                    bestIntensity = intensity;
                    bestContactPoint = ev.ContactPoint;
                }

                if (bestIntensity <= 0f)
                    continue;

                bridge.PlayImpactVFX(bestContactPoint, bestIntensity, isEVA);
            }
            } // End VFX profiler marker
        }
    }
}
