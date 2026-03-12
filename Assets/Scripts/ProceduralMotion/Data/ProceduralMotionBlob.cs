using Unity.Entities;
using Unity.Mathematics;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Burst-safe BlobAsset containing all procedural motion profile data.
    /// Baked from ProceduralMotionProfile ScriptableObject during subscene baking.
    /// </summary>
    public struct ProceduralMotionBlob
    {
        // Sway (mouse input to weapon drag)
        public float SwayPositionScale;
        public float SwayRotationScale;
        public float SwayEMASmoothing;
        public float SwayMaxAngle;

        // Bob (movement to weapon oscillation)
        public float BobAmplitudeX;
        public float BobAmplitudeY;
        public float BobFrequency;
        public float BobSprintMultiplier;
        public float BobRotationScale;

        // Inertia (acceleration to weapon lag)
        public float InertiaPositionScale;
        public float InertiaRotationScale;
        public float InertiaMaxForce;

        // Landing impact
        public float LandingPositionImpulse;
        public float LandingRotationImpulse;
        public float LandingSpeedThreshold;
        public float LandingMaxImpulse;

        // Idle noise (breathing/micro-movements)
        public float IdleNoiseAmplitude;
        public float IdleNoiseFrequency;
        public float IdleNoiseRotationScale;

        // Wall probe
        public float WallProbeDistance;
        public float WallTuckPositionZ;
        public float WallTuckRotationPitch;
        public float WallTuckBlendSpeed;
        public float WallProbeRadius;

        // Hit reaction
        public float HitReactionPositionScale;
        public float HitReactionRotationScale;
        public float HitReactionCameraScale;

        // Visual recoil (weapon kick on fire)
        public float VisualRecoilKickZ;
        public float VisualRecoilPitchUp;
        public float VisualRecoilRollRange;
        public float VisualRecoilPositionSnap;

        // Default spring parameters
        public float3 DefaultPositionFrequency;
        public float3 DefaultPositionDampingRatio;
        public float3 DefaultRotationFrequency;
        public float3 DefaultRotationDampingRatio;

        // Per-state overrides indexed by (byte)MotionState
        public BlobArray<MotionStateOverride> StateOverrides;

        // Per-paradigm weights indexed by (byte)InputParadigm
        public BlobArray<ParadigmMotionWeights> ParadigmWeights;
    }

    /// <summary>
    /// Per-MotionState spring parameter overrides and force scales.
    /// </summary>
    public struct MotionStateOverride
    {
        // Spring overrides (0 = use profile default, >0 = absolute Hz)
        public float3 PositionFrequency;
        public float3 PositionDampingRatio;
        public float3 RotationFrequency;
        public float3 RotationDampingRatio;

        // Whether frequency values are multipliers (true) or absolute (false)
        public bool FrequencyIsMultiplier;

        // Force scales (1.0 = use profile default)
        public float BobScale;
        public float SwayScale;
        public float InertiaScale;
        public float IdleNoiseScale;

        // Transition duration in seconds
        public float TransitionDuration;

        // Static offsets (e.g., ADS position, sprint tilt)
        public float3 PositionOffset;
        public float3 RotationOffset;

        // Whether spring solver is frozen (vault/climb)
        public bool IsFrozen;
    }

    /// <summary>
    /// Per-InputParadigm force weight multipliers.
    /// </summary>
    public struct ParadigmMotionWeights
    {
        public float FPMotionWeight;
        public float CameraMotionWeight;
        public float WeaponMotionWeight;
        public float HitReactionWeight;
        public float BobWeight;
        public float SwayWeight;
    }
}
