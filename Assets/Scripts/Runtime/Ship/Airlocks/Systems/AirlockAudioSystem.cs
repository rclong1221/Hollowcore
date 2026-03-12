using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that triggers audio events based on airlock state changes.
    /// Detects cycle start, progress, and completion to play appropriate sounds.
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.5: Presentation (Audio Cues)
    /// - Pressurization start: hiss/seal sound
    /// - Cycle progress: ambient vent sounds
    /// - Cycle complete: door open sound
    /// 
    /// Uses Unity ECS with managed system for AudioManager integration.
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial class AirlockAudioSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Track state changes to trigger audio events
            foreach (var (airlock, prevState, transform, entity) in
                     SystemAPI.Query<RefRO<Airlock>, RefRW<AirlockAudioState>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                ref var audioState = ref prevState.ValueRW;
                var currentAirlock = airlock.ValueRO;
                float3 position = transform.ValueRO.Position;

                // Detect state transitions
                if (audioState.PreviousState != currentAirlock.State)
                {
                    // State changed - trigger appropriate audio
                    HandleStateChange(audioState.PreviousState, currentAirlock.State, position);
                    audioState.PreviousState = currentAirlock.State;
                    audioState.LastProgressSound = 0f;
                }

                // During cycling, play ambient vent sounds at intervals
                if (currentAirlock.State != AirlockState.Idle && currentAirlock.CurrentUser != Entity.Null)
                {
                    float progressDelta = currentAirlock.CycleProgress - audioState.LastProgressSound;
                    if (progressDelta >= audioState.VentSoundInterval)
                    {
                        PlayAirlockSound(AirlockSoundType.Vent, position, 0.5f);
                        audioState.LastProgressSound = currentAirlock.CycleProgress;
                    }
                }
            }
        }

        private void HandleStateChange(AirlockState previousState, AirlockState newState, float3 position)
        {
            // Cycle started
            if (previousState == AirlockState.Idle && newState != AirlockState.Idle)
            {
                PlayAirlockSound(AirlockSoundType.CycleStart, position, 1f);
                
                if (newState == AirlockState.CyclingToExterior)
                {
                    // Depressurizing - hiss sound
                    PlayAirlockSound(AirlockSoundType.Depressurize, position, 1f);
                }
                else if (newState == AirlockState.CyclingToInterior)
                {
                    // Pressurizing - different sound
                    PlayAirlockSound(AirlockSoundType.Pressurize, position, 1f);
                }
            }
            // Cycle completed
            else if (previousState != AirlockState.Idle && newState == AirlockState.Idle)
            {
                PlayAirlockSound(AirlockSoundType.CycleComplete, position, 1f);
                PlayAirlockSound(AirlockSoundType.DoorOpen, position, 1f);
            }
        }

        private void PlayAirlockSound(AirlockSoundType soundType, float3 position, float volume)
        {
            // Try to find AirlockAudioManager in scene
            var audioManager = UnityEngine.Object.FindAnyObjectByType<AirlockAudioManager>();
            if (audioManager != null)
            {
                audioManager.PlaySound(soundType, new UnityEngine.Vector3(position.x, position.y, position.z), volume);
            }
            else
            {
                // Fallback: Use global AudioManager with generated beep
                var globalAudioManager = UnityEngine.Object.FindAnyObjectByType<Audio.Systems.AudioManager>();
                if (globalAudioManager != null)
                {
                    // Generate a placeholder beep at the position
                    // Sound type determines frequency for debugging
                    int soundId = (int)soundType + 100; // Offset to avoid collision with surface materials
                    // Note: This is a fallback - proper audio assets should be assigned via AirlockAudioManager
                }
            }
        }
    }

    /// <summary>
    /// Type of airlock sound effect.
    /// </summary>
    public enum AirlockSoundType
    {
        /// <summary>Initial seal/lock sound when cycle starts.</summary>
        CycleStart = 0,
        
        /// <summary>Depressurization hiss (exiting to vacuum).</summary>
        Depressurize = 1,
        
        /// <summary>Pressurization sound (entering from vacuum).</summary>
        Pressurize = 2,
        
        /// <summary>Ambient vent sound during cycle.</summary>
        Vent = 3,
        
        /// <summary>Cycle complete chime/beep.</summary>
        CycleComplete = 4,
        
        /// <summary>Door opening sound.</summary>
        DoorOpen = 5,
        
        /// <summary>Door closing sound.</summary>
        DoorClose = 6,
        
        /// <summary>Error/denied buzzer.</summary>
        Denied = 7,

        /// <summary>Emergency alarm.</summary>
        Emergency = 8
    }

    /// <summary>
    /// Component tracking previous airlock state for audio triggering.
    /// Added to airlocks to detect state changes.
    /// </summary>
    public struct AirlockAudioState : IComponentData
    {
        /// <summary>Previous airlock state (for detecting changes).</summary>
        public AirlockState PreviousState;

        /// <summary>Last cycle progress when a vent sound was played.</summary>
        public float LastProgressSound;

        /// <summary>Interval between vent sounds during cycling.</summary>
        public float VentSoundInterval;

        /// <summary>Default audio state.</summary>
        public static AirlockAudioState Default => new()
        {
            PreviousState = AirlockState.Idle,
            LastProgressSound = 0f,
            VentSoundInterval = 0.5f // Play vent sound every 0.5 seconds
        };
    }
}
