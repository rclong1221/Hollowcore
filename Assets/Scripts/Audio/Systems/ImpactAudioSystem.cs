using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using DIG.Player.Components; // CollisionEvent

namespace Audio.Systems
{
    /// <summary>
    /// Physics collision impact sounds with distance culling.
    /// EPIC 5.1, EPIC 15.27 Phase 8 quality upgrades:
    ///   - Added distance culling (skip impacts beyond 40m from listener)
    ///   - Added WorldSystemFilter (Client/Local only)
    ///   - Cached AudioManager + AudioListener references
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ImpactAudioSystem : SystemBase
    {
        private AudioManager _audioManager;
        private bool _audioManagerCached;
        private Transform _listenerTransform;

        private const float kMaxImpactDistance = 40f;
        private const float kMaxImpactDistanceSq = kMaxImpactDistance * kMaxImpactDistance;

        protected override void OnUpdate()
        {
            // EPIC 15.27 Phase 8: Cache AudioManager reference
            if (!_audioManagerCached)
            {
                _audioManager = Object.FindFirstObjectByType<AudioManager>();
                if (_audioManager == null) return;
                _audioManagerCached = true;
            }

            // Cache listener transform for distance check
            if (_listenerTransform == null)
            {
                var listener = Object.FindAnyObjectByType<AudioListener>();
                if (listener != null) _listenerTransform = listener.transform;
            }

            float3 listenerPos = _listenerTransform != null ? (float3)_listenerTransform.position : float3.zero;

            foreach (var (buffer, localTransform) in
                     SystemAPI.Query<DynamicBuffer<CollisionEvent>, RefRO<Unity.Transforms.LocalTransform>>())
            {
                if (buffer.IsEmpty) continue;

                foreach (var evt in buffer)
                {
                    // Filter small impacts
                    if (evt.ImpactForce < 2.0f) continue;

                    // EPIC 15.27 Phase 8: Distance culling — skip impacts far from listener
                    float distSq = math.distancesq(listenerPos, (float3)evt.ContactPoint);
                    if (distSq > kMaxImpactDistanceSq) continue;

                    int materialId = 0;
                    float intensity = math.clamp(evt.ImpactForce / 10f, 0.1f, 1.0f);

                    if (SystemAPI.HasComponent<ImpactAudioData>(evt.OtherEntity))
                    {
                        var data = SystemAPI.GetComponent<ImpactAudioData>(evt.OtherEntity);
                        if (evt.ImpactSpeed < data.VelocityThreshold) continue;

                        materialId = data.MaterialId;
                        intensity *= data.MassFactor;
                    }

                    _audioManager.PlayImpact(materialId, evt.ContactPoint, intensity);
                }
            }
        }
    }
}
