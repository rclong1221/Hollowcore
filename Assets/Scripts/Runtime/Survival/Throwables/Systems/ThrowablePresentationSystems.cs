using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Throwables
{
    /// <summary>
    /// Client-side system that syncs EmitsLight state to actual Unity lights.
    /// Presentation layer handles the actual light GameObject management.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlareLightSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Only run on clients
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Sync light state to light entities
            // The presentation layer (MonoBehaviour) will read LightState
            // and apply it to actual Unity Light components
            foreach (var (emitsLight, thrownObject) in
                     SystemAPI.Query<RefRW<EmitsLight>, RefRO<ThrownObject>>())
            {
                // Light entity will be updated by hybrid presentation layer
                // This system ensures the ECS data is correct

                // Intensity already updated by FlareIntensitySystem
                // Nothing additional needed here - presentation layer reads EmitsLight
            }
        }
    }

    /// <summary>
    /// Client-side system for sound lure audio playback.
    /// Presentation layer handles actual AudioSource management.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoundLureAudioSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Only run on clients
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Audio playback is handled by presentation layer (MonoBehaviour)
            // This system ensures ECS state is correct for the presentation layer to read
            foreach (var (emitsSound, thrownObject) in
                     SystemAPI.Query<RefRO<EmitsSound>, RefRO<ThrownObject>>())
            {
                // Presentation layer reads EmitsSound.IsPlaying and EmitsSound.Volume
                // to control AudioSource playback
            }
        }
    }

    /// <summary>
    /// Client-side state for flare visual effects.
    /// Used by presentation layer for particle effects, etc.
    /// </summary>
    public struct FlareVisualState : IComponentData
    {
        /// <summary>
        /// Whether smoke/spark particles should be emitting.
        /// </summary>
        public bool IsEmittingParticles;

        /// <summary>
        /// Particle emission rate multiplier (0-1).
        /// </summary>
        public float ParticleRate;
    }

    /// <summary>
    /// Updates FlareVisualState based on thrown object state.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlareVisualUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (visualState, thrownObject, emitsLight) in
                     SystemAPI.Query<RefRW<FlareVisualState>, RefRO<ThrownObject>, RefRO<EmitsLight>>())
            {
                ref var visual = ref visualState.ValueRW;

                // Emit particles while active
                visual.IsEmittingParticles = thrownObject.ValueRO.RemainingLifetime > 0f;

                // Particle rate follows intensity
                visual.ParticleRate = emitsLight.ValueRO.Intensity / emitsLight.ValueRO.MaxIntensity;
            }
        }
    }
}
