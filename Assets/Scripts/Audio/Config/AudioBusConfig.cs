using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Config
{
    /// <summary>
    /// Per-bus audio settings. Configurable defaults for volume, spatial blend,
    /// max distance, rolloff, and sidechain ducking.
    /// EPIC 15.27 Phase 1.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioBusConfig", menuName = "DIG/Audio/Bus Config")]
    public class AudioBusConfig : ScriptableObject
    {
        [System.Serializable]
        public struct BusSettings
        {
            [Tooltip("AudioMixer group for this bus (assign from mixer)")]
            public AudioMixerGroup MixerGroup;

            [Tooltip("Default volume (0-1)")]
            [Range(0f, 1f)]
            public float DefaultVolume;

            [Tooltip("Default spatial blend (0=2D, 1=3D)")]
            [Range(0f, 1f)]
            public float DefaultSpatialBlend;

            [Tooltip("Default max audible distance in meters")]
            public float DefaultMaxDistance;

            [Tooltip("Rolloff mode (0=Logarithmic, 1=Linear, 2=Custom)")]
            [Range(0, 2)]
            public int RolloffMode;
        }

        [System.Serializable]
        public struct SidechainDuck
        {
            [Tooltip("Source bus that triggers ducking")]
            public AudioBusType SourceBus;

            [Tooltip("Target bus that gets ducked")]
            public AudioBusType TargetBus;

            [Tooltip("Duck amount in dB (negative value, e.g. -6)")]
            [Range(-20f, 0f)]
            public float DuckAmountDB;

            [Tooltip("Attack time in seconds")]
            [Range(0.01f, 1f)]
            public float AttackTime;

            [Tooltip("Release time in seconds")]
            [Range(0.1f, 5f)]
            public float ReleaseTime;
        }

        [Header("Bus Settings")]
        public BusSettings CombatBus = new BusSettings
        {
            DefaultVolume = 1f, DefaultSpatialBlend = 1f, DefaultMaxDistance = 100f, RolloffMode = 0
        };

        public BusSettings AmbientBus = new BusSettings
        {
            DefaultVolume = 0.8f, DefaultSpatialBlend = 0.6f, DefaultMaxDistance = 80f, RolloffMode = 0
        };

        public BusSettings MusicBus = new BusSettings
        {
            DefaultVolume = 0.7f, DefaultSpatialBlend = 0f, DefaultMaxDistance = 0f, RolloffMode = 0
        };

        public BusSettings DialogueBus = new BusSettings
        {
            DefaultVolume = 1f, DefaultSpatialBlend = 0.8f, DefaultMaxDistance = 60f, RolloffMode = 0
        };

        public BusSettings UIBus = new BusSettings
        {
            DefaultVolume = 1f, DefaultSpatialBlend = 0f, DefaultMaxDistance = 0f, RolloffMode = 0
        };

        public BusSettings FootstepBus = new BusSettings
        {
            DefaultVolume = 0.9f, DefaultSpatialBlend = 1f, DefaultMaxDistance = 60f, RolloffMode = 0
        };

        [Header("Sidechain Ducking")]
        public SidechainDuck[] SidechainRules = new SidechainDuck[]
        {
            new SidechainDuck
            {
                SourceBus = AudioBusType.Combat, TargetBus = AudioBusType.Ambient,
                DuckAmountDB = -6f, AttackTime = 0.3f, ReleaseTime = 1f
            },
            new SidechainDuck
            {
                SourceBus = AudioBusType.Dialogue, TargetBus = AudioBusType.Music,
                DuckAmountDB = -9f, AttackTime = 0.2f, ReleaseTime = 1.5f
            }
        };

        public BusSettings GetSettings(AudioBusType bus)
        {
            switch (bus)
            {
                case AudioBusType.Combat: return CombatBus;
                case AudioBusType.Ambient: return AmbientBus;
                case AudioBusType.Music: return MusicBus;
                case AudioBusType.Dialogue: return DialogueBus;
                case AudioBusType.UI: return UIBus;
                case AudioBusType.Footstep: return FootstepBus;
                default: return CombatBus;
            }
        }
    }
}
