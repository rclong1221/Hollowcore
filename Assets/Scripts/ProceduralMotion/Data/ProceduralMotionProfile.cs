using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 2: Designer-facing ScriptableObject for procedural motion tuning.
    /// Baked to BlobAssetReference&lt;ProceduralMotionBlob&gt; for Burst-safe runtime access.
    /// Create via: Assets > Create > DIG/Procedural Motion/Motion Profile
    /// </summary>
    [CreateAssetMenu(fileName = "MotionProfile_Default", menuName = "DIG/Procedural Motion/Motion Profile")]
    public class ProceduralMotionProfile : ScriptableObject
    {
        // ─── Sway ──────────────────────────────────────────────
        [Header("Sway (Mouse Input to Weapon Drag)")]
        [Range(0f, 0.1f)] public float SwayPositionScale = 0.02f;
        [Range(0f, 5f)] public float SwayRotationScale = 1.5f;
        [Range(0.05f, 0.5f)] public float SwayEMASmoothing = 0.15f;
        [Range(1f, 15f)] public float SwayMaxAngle = 5f;

        // ─── Bob ───────────────────────────────────────────────
        [Header("Bob (Movement to Weapon Oscillation)")]
        [Range(0f, 0.05f)] public float BobAmplitudeX = 0.01f;
        [Range(0f, 0.08f)] public float BobAmplitudeY = 0.025f;
        [Range(0.5f, 4f)] public float BobFrequency = 1.8f;
        [Range(1f, 3f)] public float BobSprintMultiplier = 1.6f;
        [Range(0f, 2f)] public float BobRotationScale = 0.5f;

        // ─── Inertia ──────────────────────────────────────────
        [Header("Inertia (Acceleration to Weapon Lag)")]
        [Range(0f, 0.02f)] public float InertiaPositionScale = 0.005f;
        [Range(0f, 2f)] public float InertiaRotationScale = 0.8f;
        [Range(0.01f, 0.5f)] public float InertiaMaxForce = 0.1f;

        // ─── Landing ──────────────────────────────────────────
        [Header("Landing Impact")]
        [Range(0f, 0.1f)] public float LandingPositionImpulse = 0.04f;
        [Range(0f, 5f)] public float LandingRotationImpulse = 2f;
        [Range(0f, 10f)] public float LandingSpeedThreshold = 2f;
        [Range(0f, 0.2f)] public float LandingMaxImpulse = 0.1f;

        // ─── Idle Noise ───────────────────────────────────────
        [Header("Idle Noise (Breathing/Micro-Movements)")]
        [Range(0f, 0.005f)] public float IdleNoiseAmplitude = 0.001f;
        [Range(0.1f, 2f)] public float IdleNoiseFrequency = 0.8f;
        [Range(0f, 1f)] public float IdleNoiseRotationScale = 0.5f;

        // ─── Wall Probe ───────────────────────────────────────
        [Header("Wall Probe (Weapon Tuck)")]
        [Range(0.3f, 2f)] public float WallProbeDistance = 0.8f;
        [Range(-0.3f, 0f)] public float WallTuckPositionZ = -0.15f;
        [Range(-30f, 0f)] public float WallTuckRotationPitch = -15f;
        [Range(1f, 20f)] public float WallTuckBlendSpeed = 8f;
        [Range(0.01f, 0.2f)] public float WallProbeRadius = 0.05f;

        // ─── Hit Reaction ─────────────────────────────────────
        [Header("Hit Reaction (Damage Direction to Flinch)")]
        [Range(0f, 0.05f)] public float HitReactionPositionScale = 0.02f;
        [Range(0f, 5f)] public float HitReactionRotationScale = 3f;
        [Range(0f, 1f)] public float HitReactionCameraScale = 0.3f;

        // ─── Visual Recoil ────────────────────────────────────
        [Header("Visual Recoil (Weapon Kick on Fire)")]
        [Range(-0.1f, 0f)] public float VisualRecoilKickZ = -0.03f;
        [Range(0f, 5f)] public float VisualRecoilPitchUp = 2f;
        [Range(0f, 3f)] public float VisualRecoilRollRange = 1f;
        [Range(1f, 10f)] public float VisualRecoilPositionSnap = 5f;

        // ─── Spring Defaults ──────────────────────────────────
        [Header("Default Spring Parameters")]
        public Vector3 DefaultPositionFrequency = new Vector3(8f, 8f, 8f);
        public Vector3 DefaultPositionDampingRatio = new Vector3(0.7f, 0.7f, 0.7f);
        public Vector3 DefaultRotationFrequency = new Vector3(8f, 8f, 8f);
        public Vector3 DefaultRotationDampingRatio = new Vector3(0.7f, 0.7f, 0.7f);

        // ─── State Overrides ──────────────────────────────────
        [Header("Per-State Overrides")]
        public MotionStateOverrideData[] StateOverrides = CreateDefaultStateOverrides();

        // ─── Paradigm Weights ─────────────────────────────────
        [Header("Per-Paradigm Weights")]
        public ParadigmMotionWeightsData[] ParadigmWeights = CreateDefaultParadigmWeights();

        // ═══════════════════════════════════════════════════════
        // BakeToBlob
        // ═══════════════════════════════════════════════════════

        public BlobAssetReference<ProceduralMotionBlob> BakeToBlob(IBaker baker)
        {
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var blob = ref builder.ConstructRoot<ProceduralMotionBlob>();

            // Sway
            blob.SwayPositionScale = SwayPositionScale;
            blob.SwayRotationScale = SwayRotationScale;
            blob.SwayEMASmoothing = SwayEMASmoothing;
            blob.SwayMaxAngle = SwayMaxAngle;

            // Bob
            blob.BobAmplitudeX = BobAmplitudeX;
            blob.BobAmplitudeY = BobAmplitudeY;
            blob.BobFrequency = BobFrequency;
            blob.BobSprintMultiplier = BobSprintMultiplier;
            blob.BobRotationScale = BobRotationScale;

            // Inertia
            blob.InertiaPositionScale = InertiaPositionScale;
            blob.InertiaRotationScale = InertiaRotationScale;
            blob.InertiaMaxForce = InertiaMaxForce;

            // Landing
            blob.LandingPositionImpulse = LandingPositionImpulse;
            blob.LandingRotationImpulse = LandingRotationImpulse;
            blob.LandingSpeedThreshold = LandingSpeedThreshold;
            blob.LandingMaxImpulse = LandingMaxImpulse;

            // Idle Noise
            blob.IdleNoiseAmplitude = IdleNoiseAmplitude;
            blob.IdleNoiseFrequency = IdleNoiseFrequency;
            blob.IdleNoiseRotationScale = IdleNoiseRotationScale;

            // Wall Probe
            blob.WallProbeDistance = WallProbeDistance;
            blob.WallTuckPositionZ = WallTuckPositionZ;
            blob.WallTuckRotationPitch = WallTuckRotationPitch;
            blob.WallTuckBlendSpeed = WallTuckBlendSpeed;
            blob.WallProbeRadius = WallProbeRadius;

            // Hit Reaction
            blob.HitReactionPositionScale = HitReactionPositionScale;
            blob.HitReactionRotationScale = HitReactionRotationScale;
            blob.HitReactionCameraScale = HitReactionCameraScale;

            // Visual Recoil
            blob.VisualRecoilKickZ = VisualRecoilKickZ;
            blob.VisualRecoilPitchUp = VisualRecoilPitchUp;
            blob.VisualRecoilRollRange = VisualRecoilRollRange;
            blob.VisualRecoilPositionSnap = VisualRecoilPositionSnap;

            // Default spring parameters
            blob.DefaultPositionFrequency = (float3)DefaultPositionFrequency;
            blob.DefaultPositionDampingRatio = (float3)DefaultPositionDampingRatio;
            blob.DefaultRotationFrequency = (float3)DefaultRotationFrequency;
            blob.DefaultRotationDampingRatio = (float3)DefaultRotationDampingRatio;

            // State overrides (11 states)
            int stateCount = 11; // MotionState.Staggered + 1
            var stateArray = builder.Allocate(ref blob.StateOverrides, stateCount);
            for (int i = 0; i < stateCount; i++)
            {
                if (StateOverrides != null && i < StateOverrides.Length)
                {
                    var src = StateOverrides[i];
                    stateArray[i] = new MotionStateOverride
                    {
                        PositionFrequency = (float3)src.PositionFrequency,
                        PositionDampingRatio = (float3)src.PositionDampingRatio,
                        RotationFrequency = (float3)src.RotationFrequency,
                        RotationDampingRatio = (float3)src.RotationDampingRatio,
                        FrequencyIsMultiplier = src.FrequencyIsMultiplier,
                        BobScale = src.BobScale,
                        SwayScale = src.SwayScale,
                        InertiaScale = src.InertiaScale,
                        IdleNoiseScale = src.IdleNoiseScale,
                        TransitionDuration = src.TransitionDuration,
                        PositionOffset = (float3)src.PositionOffset,
                        RotationOffset = (float3)src.RotationOffset,
                        IsFrozen = src.IsFrozen
                    };
                }
                else
                {
                    stateArray[i] = new MotionStateOverride
                    {
                        BobScale = 1f,
                        SwayScale = 1f,
                        InertiaScale = 1f,
                        IdleNoiseScale = 1f,
                        TransitionDuration = 0.15f
                    };
                }
            }

            // Paradigm weights (6 paradigms)
            int paradigmCount = 6; // InputParadigm.SideScroller2D + 1
            var paradigmArray = builder.Allocate(ref blob.ParadigmWeights, paradigmCount);
            for (int i = 0; i < paradigmCount; i++)
            {
                if (ParadigmWeights != null && i < ParadigmWeights.Length)
                {
                    var src = ParadigmWeights[i];
                    paradigmArray[i] = new ParadigmMotionWeights
                    {
                        FPMotionWeight = src.FPMotionWeight,
                        CameraMotionWeight = src.CameraMotionWeight,
                        WeaponMotionWeight = src.WeaponMotionWeight,
                        HitReactionWeight = src.HitReactionWeight,
                        BobWeight = src.BobWeight,
                        SwayWeight = src.SwayWeight
                    };
                }
                else
                {
                    paradigmArray[i] = new ParadigmMotionWeights
                    {
                        FPMotionWeight = 1f,
                        CameraMotionWeight = 1f,
                        WeaponMotionWeight = 1f,
                        HitReactionWeight = 1f,
                        BobWeight = 1f,
                        SwayWeight = 1f
                    };
                }
            }

            var blobRef = builder.CreateBlobAssetReference<ProceduralMotionBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return blobRef;
        }

        // ═══════════════════════════════════════════════════════
        // Default Data Factories
        // ═══════════════════════════════════════════════════════

        private static MotionStateOverrideData[] CreateDefaultStateOverrides()
        {
            var overrides = new MotionStateOverrideData[11];

            // Idle
            overrides[0] = new MotionStateOverrideData
            {
                BobScale = 0f, SwayScale = 1f, InertiaScale = 0.5f, IdleNoiseScale = 1f,
                TransitionDuration = 0.15f
            };
            // Walk
            overrides[1] = new MotionStateOverrideData
            {
                BobScale = 1f, SwayScale = 1f, InertiaScale = 1f, IdleNoiseScale = 0f,
                TransitionDuration = 0.12f
            };
            // Sprint
            overrides[2] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(1.2f, 1.2f, 1.2f),
                PositionDampingRatio = new Vector3(0.9f, 0.9f, 0.9f),
                FrequencyIsMultiplier = true,
                BobScale = 1.6f, SwayScale = 0.5f, InertiaScale = 1.5f, IdleNoiseScale = 0f,
                TransitionDuration = 0.1f,
                PositionOffset = new Vector3(0f, -0.03f, 0f),
                RotationOffset = new Vector3(5f, 0f, 3f)
            };
            // ADS
            overrides[3] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(15f, 15f, 15f),
                PositionDampingRatio = new Vector3(1f, 1f, 1f),
                RotationFrequency = new Vector3(15f, 15f, 15f),
                RotationDampingRatio = new Vector3(1f, 1f, 1f),
                BobScale = 0f, SwayScale = 0.2f, InertiaScale = 0.3f, IdleNoiseScale = 0.3f,
                TransitionDuration = 0.15f
            };
            // Slide
            overrides[4] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(8f, 8f, 8f),
                PositionDampingRatio = new Vector3(0.6f, 0.6f, 0.6f),
                BobScale = 0f, SwayScale = 0.3f, InertiaScale = 2f, IdleNoiseScale = 0f,
                TransitionDuration = 0.08f,
                PositionOffset = new Vector3(0f, -0.05f, 0f),
                RotationOffset = new Vector3(0f, 0f, 15f)
            };
            // Vault
            overrides[5] = new MotionStateOverrideData
            {
                IsFrozen = true,
                BobScale = 0f, SwayScale = 0f, InertiaScale = 0f, IdleNoiseScale = 0f,
                TransitionDuration = 0.05f,
                PositionOffset = new Vector3(0f, -0.1f, -0.1f),
                RotationOffset = new Vector3(-20f, 0f, 0f)
            };
            // Swim
            overrides[6] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(4f, 4f, 4f),
                PositionDampingRatio = new Vector3(0.9f, 0.9f, 0.9f),
                BobScale = 0.3f, SwayScale = 0.5f, InertiaScale = 0.5f, IdleNoiseScale = 0.8f,
                TransitionDuration = 0.2f
            };
            // Airborne
            overrides[7] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(0.8f, 0.8f, 0.8f),
                FrequencyIsMultiplier = true,
                BobScale = 0f, SwayScale = 0.8f, InertiaScale = 0.5f, IdleNoiseScale = 0f,
                TransitionDuration = 0.05f
            };
            // Crouch
            overrides[8] = new MotionStateOverrideData
            {
                PositionDampingRatio = new Vector3(1.1f, 1.1f, 1.1f),
                FrequencyIsMultiplier = true,
                BobScale = 0.6f, SwayScale = 0.8f, InertiaScale = 0.8f, IdleNoiseScale = 0.5f,
                TransitionDuration = 0.12f,
                PositionOffset = new Vector3(0f, -0.02f, 0f)
            };
            // Climb
            overrides[9] = new MotionStateOverrideData
            {
                IsFrozen = true,
                BobScale = 0f, SwayScale = 0f, InertiaScale = 0f, IdleNoiseScale = 0f,
                TransitionDuration = 0.05f,
                PositionOffset = new Vector3(-0.1f, 0f, -0.15f),
                RotationOffset = new Vector3(-10f, 20f, 0f)
            };
            // Staggered
            overrides[10] = new MotionStateOverrideData
            {
                PositionFrequency = new Vector3(4f, 4f, 4f),
                PositionDampingRatio = new Vector3(0.3f, 0.3f, 0.3f),
                BobScale = 0f, SwayScale = 0f, InertiaScale = 3f, IdleNoiseScale = 0f,
                TransitionDuration = 0.03f
            };

            return overrides;
        }

        private static ParadigmMotionWeightsData[] CreateDefaultParadigmWeights()
        {
            return new[]
            {
                // Shooter
                new ParadigmMotionWeightsData { FPMotionWeight = 1f, CameraMotionWeight = 1f, WeaponMotionWeight = 1f, HitReactionWeight = 1f, BobWeight = 1f, SwayWeight = 1f },
                // MMO
                new ParadigmMotionWeightsData { FPMotionWeight = 0.6f, CameraMotionWeight = 0.7f, WeaponMotionWeight = 0.5f, HitReactionWeight = 1f, BobWeight = 0.8f, SwayWeight = 0.7f },
                // ARPG
                new ParadigmMotionWeightsData { FPMotionWeight = 0f, CameraMotionWeight = 0.3f, WeaponMotionWeight = 0f, HitReactionWeight = 0.7f, BobWeight = 0f, SwayWeight = 0f },
                // MOBA
                new ParadigmMotionWeightsData { FPMotionWeight = 0f, CameraMotionWeight = 0.2f, WeaponMotionWeight = 0f, HitReactionWeight = 0.5f, BobWeight = 0f, SwayWeight = 0f },
                // TwinStick
                new ParadigmMotionWeightsData { FPMotionWeight = 0f, CameraMotionWeight = 0.4f, WeaponMotionWeight = 0f, HitReactionWeight = 0.8f, BobWeight = 0.3f, SwayWeight = 0f },
                // SideScroller2D
                new ParadigmMotionWeightsData { FPMotionWeight = 0f, CameraMotionWeight = 0.3f, WeaponMotionWeight = 0f, HitReactionWeight = 0.8f, BobWeight = 0f, SwayWeight = 0f },
            };
        }
    }

    /// <summary>
    /// Serializable per-state override data for the inspector.
    /// </summary>
    [System.Serializable]
    public struct MotionStateOverrideData
    {
        [Header("Spring Overrides (0 = use default)")]
        public Vector3 PositionFrequency;
        public Vector3 PositionDampingRatio;
        public Vector3 RotationFrequency;
        public Vector3 RotationDampingRatio;

        [Tooltip("If true, frequency values are multipliers applied to profile defaults.")]
        public bool FrequencyIsMultiplier;

        [Header("Force Scales")]
        [Range(0f, 3f)] public float BobScale;
        [Range(0f, 3f)] public float SwayScale;
        [Range(0f, 3f)] public float InertiaScale;
        [Range(0f, 3f)] public float IdleNoiseScale;

        [Header("Transition")]
        [Range(0.01f, 1f)] public float TransitionDuration;

        [Header("Static Offsets")]
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;

        [Header("Freeze")]
        [Tooltip("Spring solver is frozen (vault/climb states).")]
        public bool IsFrozen;
    }

    /// <summary>
    /// Serializable per-paradigm weight data for the inspector.
    /// </summary>
    [System.Serializable]
    public struct ParadigmMotionWeightsData
    {
        [Range(0f, 1f)] public float FPMotionWeight;
        [Range(0f, 1f)] public float CameraMotionWeight;
        [Range(0f, 1f)] public float WeaponMotionWeight;
        [Range(0f, 1f)] public float HitReactionWeight;
        [Range(0f, 1f)] public float BobWeight;
        [Range(0f, 1f)] public float SwayWeight;
    }
}
