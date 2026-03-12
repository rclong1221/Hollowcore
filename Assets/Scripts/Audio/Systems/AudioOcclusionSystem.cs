using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using Audio.Config;

namespace Audio.Systems
{
    /// <summary>
    /// Performs batched raycasts from listener to audio sources for occlusion.
    /// Frame-spread scheduling: each source is raycasted once every SpreadFrames.
    /// Applies low-pass filter cutoff and volume attenuation based on occlusion factor.
    /// EPIC 15.27 Phase 3.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioTransformSyncSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AudioOcclusionSystem : SystemBase
    {
        private OcclusionProfile _profile;
        private int _frameCount;

        protected override void OnUpdate()
        {
            if (_profile == null)
            {
                _profile = Object.FindAnyObjectByType<OcclusionProfileHolder>()?.Profile;
                if (_profile == null) return;
            }

            // Get listener position
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null) return;
            Vector3 listenerPos = listener.transform.position;

            float dt = SystemAPI.Time.DeltaTime;
            int spreadFrames = _profile.SpreadFrames;
            _frameCount++;

            // Check vacuum state — if in vacuum, occlusion is irrelevant
            bool inVacuum = false;
            foreach (var listenerState in SystemAPI.Query<RefRO<AudioListenerState>>().WithAll<GhostOwnerIsLocal>())
            {
                if (listenerState.ValueRO.PressureFactor < 0.01f)
                    inVacuum = true;
            }

            foreach (var (state, emitter, ltw, entity) in
                     SystemAPI.Query<AudioSourceState, RefRO<AudioEmitter>, RefRO<LocalToWorld>>()
                     .WithEntityAccess())
            {
                if (state.Source == null) continue;
                if (!emitter.ValueRO.UseOcclusion) continue;
                if (emitter.ValueRO.Priority < _profile.MinPriorityForOcclusion) continue;

                // In vacuum, all external sounds fully occluded
                if (inVacuum)
                {
                    state.TargetOcclusionFactor = 0f;
                    ApplyOcclusion(state, dt);
                    continue;
                }

                Vector3 sourcePos = ltw.ValueRO.Position;
                float distance = Vector3.Distance(listenerPos, sourcePos);

                // Skip raycasts for distant sources
                if (distance > _profile.MaxOcclusionDistance)
                {
                    state.TargetOcclusionFactor = _profile.ClearFactor;
                    ApplyOcclusion(state, dt);
                    continue;
                }

                // Frame-spread: only raycast this source on its assigned frame slot
                if (state.OcclusionFrameSlot % spreadFrames == _frameCount % spreadFrames)
                {
                    PerformRaycast(listenerPos, sourcePos, distance, state);
                }

                ApplyOcclusion(state, dt);
            }
        }

        private void PerformRaycast(Vector3 listenerPos, Vector3 sourcePos, float distance, AudioSourceState state)
        {
            Vector3 direction = sourcePos - listenerPos;

            // Count hits through environment
            var hits = Physics.RaycastAll(listenerPos, direction.normalized, distance, _profile.OcclusionLayers,
                QueryTriggerInteraction.Ignore);

            int hitCount = hits.Length;

            if (hitCount == 0)
                state.TargetOcclusionFactor = _profile.ClearFactor;
            else if (hitCount == 1)
                state.TargetOcclusionFactor = _profile.PartialFactor;
            else
                state.TargetOcclusionFactor = _profile.HeavyFactor;
        }

        private void ApplyOcclusion(AudioSourceState state, float dt)
        {
            // Smooth lerp toward target
            float lerpSpeed = 1f / Mathf.Max(_profile.TransitionSpeed, 0.01f);
            state.OcclusionFactor = Mathf.Lerp(state.OcclusionFactor, state.TargetOcclusionFactor, dt * lerpSpeed);

            // Apply to audio source
            if (state.LowPass != null)
            {
                state.LowPass.cutoffFrequency = Mathf.Lerp(_profile.OccludedCutoff, _profile.ClearCutoff, state.OcclusionFactor);
            }

            if (state.Source != null)
            {
                float baseVolume = state.Source.volume; // preserve bus-set volume
                // Only attenuate, don't amplify
                float occlusionVolume = Mathf.Lerp(_profile.OccludedVolume, _profile.ClearVolume, state.OcclusionFactor);
                // We modulate volume multiplicatively, clamping to not exceed 1
                state.Source.volume = Mathf.Min(baseVolume, occlusionVolume);
            }
        }
    }

    /// <summary>
    /// MonoBehaviour holder for OcclusionProfile ScriptableObject reference.
    /// Place on a scene GameObject alongside AudioSourcePool.
    /// </summary>
    public class OcclusionProfileHolder : MonoBehaviour
    {
        public OcclusionProfile Profile;
    }
}
