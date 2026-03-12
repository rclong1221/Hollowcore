using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Audio.Components;

namespace Audio.Systems
{
    /// <summary>
    /// Updates AudioSource transform.position from entity LocalToWorld each frame.
    /// Handles cleanup when sources finish playing or entities are destroyed.
    /// Runs after AudioSourcePoolSystem in PresentationSystemGroup.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioSourcePoolSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AudioTransformSyncSystem : SystemBase
    {
        private AudioSourcePool _pool;

        protected override void OnUpdate()
        {
            if (_pool == null)
            {
                _pool = AudioSourcePool.Instance;
                if (_pool == null) return;
            }

            foreach (var (state, ltw, emitter, entity) in
                     SystemAPI.Query<AudioSourceState, RefRO<LocalToWorld>, RefRO<AudioEmitter>>()
                     .WithEntityAccess())
            {
                if (state.Source == null) continue;

                // Update position if tracking enabled
                if (emitter.ValueRO.TrackPosition)
                {
                    state.Source.transform.position = ltw.ValueRO.Position;
                }

                // Check if playback finished (non-looping)
                if (!state.Source.isPlaying && !state.Source.loop)
                {
                    // Return source to pool and remove managed component
                    state.Source.Stop();
                    state.Source.clip = null;
                    EntityManager.RemoveComponent<AudioSourceState>(entity);
                }
            }
        }
    }
}
