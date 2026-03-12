using Unity.Entities;
using Unity.Mathematics;
using DIG.Survival.Environment;

namespace Audio.Components
{
    // Epic 5.1: Immersive Audio Components

    /// <summary>
    /// Tracks the current environment state for the local listener (Player).
    /// Used by AudioEnvironmentSystem to drive mixer parameters.
    /// </summary>
    public struct AudioListenerState : IComponentData
    {
        public EnvironmentZoneType CurrentZoneType;
        
        /// <summary>
        /// 0.0 (Vacuum) to 1.0 (1 ATM)
        /// </summary>
        public float PressureFactor;
        
        /// <summary>
        /// True if recent loud explosion caused temporary deafness.
        /// </summary>
        public bool IsDeafened;
        
        /// <summary>
        /// Timer for recovery from deafness.
        /// </summary>
        public float DeafenTimer;

        // EPIC 15.27 Phase 4: Reverb zone tracking
        /// <summary>ID of the current reverb zone (-1 = none/outdoor fallback).</summary>
        public int ReverbZoneId;

        /// <summary>0.0 (exterior) to 1.0 (fully interior). Drives reverb wet/dry and ambient crossfade.</summary>
        public float IndoorFactor;
    }

    /// <summary>
    /// Tracks state for generating vital feedback audio (breathing, heartbeat).
    /// </summary>
    public struct VitalAudioSource : IComponentData
    {
        /// <summary>
        /// 0-1 intensity factor, derived from Low Stamina or Sprinting.
        /// </summary>
        public float BreathIntensity;
        
        /// <summary>
        /// 0-1 intensity factor, derived from Low Health (Health < 30%).
        /// </summary>
        public float HeartbeatIntensity;
        
        /// <summary>
        /// Accumulator for breath loop timing.
        /// </summary>
        public float TimeSinceLastBreath;

        // EPIC 15.27 Phase 8: Separate heartbeat timer (was reusing TimeSinceLastBreath)
        /// <summary>
        /// Accumulator for heartbeat timing. Separate from breath to avoid timing conflicts.
        /// </summary>
        public float TimeSinceLastHeartbeat;

        /// <summary>
        /// Timestamp of last pain grunt to prevent spam.
        /// </summary>
        public double LastGruntTime;
    }

    /// <summary>
    /// Tag/Data for physics objects that should make sound on impact.
    /// </summary>
    public struct ImpactAudioData : IComponentData
    {
        /// <summary>
        /// ID matching SurfaceMaterialRegistry (0=Default, 1=Metal, etc)
        /// </summary>
        public int MaterialId;
        
        /// <summary>
        /// Multiplier for impact volume. Heavy objects -> Louder.
        /// </summary>
        public float MassFactor;
        
        /// <summary>
        /// Minimum relative velocity to trigger sound.
        /// </summary>
        public float VelocityThreshold;
    }

    /// <summary>
    /// Managed component to hold references to Unity AudioSources for looping sounds.
    /// </summary>
    public class AudioSourceReference : IComponentData
    {
        public UnityEngine.AudioSource BreathSource;
        public UnityEngine.AudioSource HeartbeatSource;
    }
}
