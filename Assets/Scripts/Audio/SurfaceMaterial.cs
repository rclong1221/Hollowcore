using System.Collections.Generic;
using UnityEngine;
using DIG.Surface;

namespace Audio.Systems
{
    [CreateAssetMenu(menuName = "DIG/SurfaceMaterial", fileName = "SurfaceMaterial")]
    public class SurfaceMaterial : ScriptableObject
    {
        [Tooltip("Unique numeric id for fast runtime lookup (assign manually or via registry)")]
        public int Id;

        [Tooltip("Human readable name")]
        public string DisplayName;

        [Header("Footstep clips per stance")]
        public List<AudioClip> WalkClips = new List<AudioClip>();
        public List<AudioClip> RunClips = new List<AudioClip>();
        public List<AudioClip> CrouchClips = new List<AudioClip>();

        [Header("Landing / Impact clips")]
        public List<AudioClip> LandingClips = new List<AudioClip>();
        public List<AudioClip> ImpactClips = new List<AudioClip>();

        [Header("Action clips")]
        [Tooltip("Jump takeoff sounds")]
        public List<AudioClip> JumpClips = new List<AudioClip>();
        
        [Tooltip("Dodge roll sounds")]
        public List<AudioClip> RollClips = new List<AudioClip>();
        
        [Tooltip("Dive sounds")]
        public List<AudioClip> DiveClips = new List<AudioClip>();
        
        [Tooltip("Slide start sounds")]
        public List<AudioClip> SlideClips = new List<AudioClip>();
        
        [Tooltip("Climb grab/start sounds")]
        public List<AudioClip> ClimbClips = new List<AudioClip>();

        [Header("VFX and gameplay")]
        public GameObject VFXPrefab;
        public float FootstepVolume = 1.0f;
        public float FrictionModifier = 1.0f;

        [Header("Decals (EPIC 13.18.2)")]
        [Tooltip("Decal spawned on bullet impacts, explosions, etc.")]
        public DecalData ImpactDecal;
        
        [Tooltip("Decal spawned for footprints (optional).")]
        public DecalData FootprintDecal;
        
        [Tooltip("Whether this surface allows footprint decals.")]
        public bool AllowFootprints = true;

        [Header("EPIC 15.24 — Surface Identity")]
        [Tooltip("Surface type for impact behavior (ricochet, penetration, VFX selection). Default uses name-based heuristic.")]
        public SurfaceID SurfaceId = SurfaceID.Default;

        [Header("EPIC 15.24 — Physical Properties")]
        [Tooltip("Surface hardness (0=soft, 255=hardest). Controls ricochet threshold angle.")]
        [Range(0, 255)]
        public byte Hardness = 128;

        [Tooltip("Surface density (0=light, 255=dense). Controls penetration resistance.")]
        [Range(0, 255)]
        public byte Density = 128;

        [Tooltip("Whether bullets can pass through this surface.")]
        public bool AllowsPenetration = false;

        [Tooltip("Whether bullets at shallow angles will ricochet off this surface.")]
        public bool AllowsRicochet = true;

        [Tooltip("Whether this surface is a liquid (water, lava, etc.). Changes impact VFX to splashes.")]
        public bool IsLiquid = false;

        [Header("EPIC 15.24 Phase 8 — Continuous Audio")]
        [Tooltip("Looping audio clip played while the player moves on this surface (e.g. ice crackle, gravel crunch).")]
        public AudioClip ContinuousLoopClip;

        [Tooltip("Minimum player speed to start the continuous loop.")]
        [Range(0.1f, 10f)]
        public float LoopSpeedThreshold = 1f;

        [Tooltip("Volume when player is at maximum speed.")]
        [Range(0f, 1f)]
        public float LoopVolumeAtMaxSpeed = 0.6f;

        [Header("EPIC 15.24 Phase 11 — Haptic Feedback")]
        [Tooltip("Haptic intensity when impacts hit this surface (0=none, 1=max).")]
        [Range(0f, 1f)]
        public float HapticIntensity = 0.5f;

        [Tooltip("Haptic duration in seconds for impacts on this surface.")]
        [Range(0f, 0.5f)]
        public float HapticDuration = 0.1f;

        [Header("Slide properties")]
        [Tooltip("Is this surface slippery? (auto-triggers slide when player moves on it)")]
        public bool IsSlippery = false;
        
        [Tooltip("Friction multiplier when sliding on this surface (0-1, lower = more slippery)")]
        [Range(0f, 1f)]
        public float SlideFrictionMultiplier = 1.0f;
    }
}
