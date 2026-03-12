using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Audio.Components;
using Audio.Config;

namespace Audio.Systems
{
    /// <summary>
    /// Consumes PlayAudioRequest buffer and manages entity-linked AudioSources.
    /// For entity-linked requests, adds AudioSourceState managed component.
    /// For fire-and-forget, acquires source, plays at position, pool handles return.
    /// Runs in PresentationSystemGroup, Client/Local only.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AudioSourcePoolSystem : SystemBase
    {
        private AudioSourcePool _pool;
        private AudioClipBank _clipBank;
        private Entity _singletonEntity;
        private bool _singletonCreated;

        protected override void OnCreate()
        {
            RequireForUpdate<AudioRequestSingleton>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _pool = AudioSourcePool.Instance;
            _clipBank = Object.FindAnyObjectByType<AudioClipBankHolder>()?.Bank;

            if (!_singletonCreated)
            {
                // Create singleton entity with PlayAudioRequest buffer if it doesn't exist
                var query = GetEntityQuery(ComponentType.ReadOnly<AudioRequestSingleton>());
                if (query.IsEmpty)
                {
                    _singletonEntity = EntityManager.CreateEntity(
                        ComponentType.ReadOnly<AudioRequestSingleton>(),
                        ComponentType.ReadWrite<PlayAudioRequest>()
                    );
                    EntityManager.SetName(_singletonEntity, "AudioRequestSingleton");
                }
                else
                {
                    _singletonEntity = query.GetSingletonEntity();
                }
                _singletonCreated = true;
            }
        }

        protected override void OnUpdate()
        {
            if (_pool == null)
            {
                _pool = AudioSourcePool.Instance;
                if (_pool == null) return;
            }

            // Clean up finished one-shots
            _pool.CleanupFinished();

            // Update listener distance for priority scoring
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener != null)
                _pool.UpdateDistances(listener.transform.position);

            // Process requests
            if (!SystemAPI.HasSingleton<AudioRequestSingleton>()) return;
            var singletonEntity = SystemAPI.GetSingletonEntity<AudioRequestSingleton>();
            var requests = EntityManager.GetBuffer<PlayAudioRequest>(singletonEntity);

            for (int i = 0; i < requests.Length; i++)
            {
                ProcessRequest(requests[i]);
            }
            requests.Clear();

            // Handle dead entities with AudioSourceState
            CleanupDeadEntitySources();
        }

        private void ProcessRequest(PlayAudioRequest req)
        {
            // Resolve clip
            AudioClip clip = null;
            if (req.ClipId >= 0 && _clipBank != null)
            {
                _clipBank.TryGetClip(req.ClipId, out clip);
            }

            if (clip == null)
            {
                AudioTelemetry.LogPlaybackFailure($"No clip for ClipId={req.ClipId}");
                return;
            }

            var pooled = _pool.Acquire(req.Bus, req.Priority,
                new Vector3(req.Position.x, req.Position.y, req.Position.z));

            pooled.Source.clip = clip;
            pooled.Source.volume = req.Volume;
            pooled.Source.pitch = req.Pitch;
            pooled.Source.loop = req.Loop;
            if (req.MaxDistance > 0) pooled.Source.maxDistance = req.MaxDistance;

            if (req.TargetEntity != Entity.Null && EntityManager.Exists(req.TargetEntity))
            {
                // Entity-linked: add managed AudioSourceState so AudioTransformSyncSystem tracks it
                if (!EntityManager.HasComponent<AudioSourceState>(req.TargetEntity))
                {
                    EntityManager.AddComponentObject(req.TargetEntity, new AudioSourceState
                    {
                        Source = pooled.Source,
                        LowPass = pooled.LowPass,
                        OcclusionFactor = 1f,
                        TargetOcclusionFactor = 1f,
                        OcclusionFrameSlot = req.TargetEntity.Index % 6
                    });
                }

                // Set initial position from entity
                if (EntityManager.HasComponent<LocalToWorld>(req.TargetEntity))
                {
                    var ltw = EntityManager.GetComponentData<LocalToWorld>(req.TargetEntity);
                    pooled.Source.transform.position = ltw.Position;
                }
            }
            else
            {
                // Fire-and-forget at position
                pooled.Source.transform.position = new Vector3(req.Position.x, req.Position.y, req.Position.z);
            }

            pooled.Source.Play();
        }

        private void CleanupDeadEntitySources()
        {
            // Find entities with AudioSourceState whose source is no longer playing
            // or whose entity has been destroyed
            foreach (var (state, entity) in SystemAPI.Query<AudioSourceState>().WithEntityAccess())
            {
                if (state.Source == null) continue;

                bool shouldRelease = false;

                // Check if entity is being destroyed / disabled
                if (EntityManager.HasComponent<Unity.Entities.Disabled>(entity))
                    shouldRelease = true;

                // Check if source finished playing (non-looping)
                if (!state.Source.isPlaying && !state.Source.loop)
                    shouldRelease = true;

                if (shouldRelease)
                {
                    state.Source.Stop();
                    // AudioTransformSyncSystem or AudioSourcePool will reclaim
                }
            }
        }

        /// <summary>
        /// Static helper to ensure the singleton entity exists. Call from OnCreate of systems
        /// that need to append PlayAudioRequests.
        /// </summary>
        public static Entity GetOrCreateSingleton(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<AudioRequestSingleton>());
            if (!query.IsEmpty)
                return query.GetSingletonEntity();

            var entity = em.CreateEntity(
                ComponentType.ReadOnly<AudioRequestSingleton>(),
                ComponentType.ReadWrite<PlayAudioRequest>()
            );
            em.SetName(entity, "AudioRequestSingleton");
            return entity;
        }
    }

    /// <summary>
    /// Optional MonoBehaviour holder for AudioClipBank reference.
    /// Place on a scene GameObject alongside AudioSourcePool.
    /// </summary>
    public class AudioClipBankHolder : MonoBehaviour
    {
        public AudioClipBank Bank;
    }
}
