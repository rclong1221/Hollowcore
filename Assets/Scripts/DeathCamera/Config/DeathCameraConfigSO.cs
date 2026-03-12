using System.Collections.Generic;
using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Master configuration for the death camera system.
    /// Create via: Create > DIG > Death Camera > Config
    /// </summary>
    [CreateAssetMenu(fileName = "DeathCameraConfig", menuName = "DIG/Death Camera/Config")]
    public class DeathCameraConfigSO : ScriptableObject
    {
        [Header("General")]
        public string ConfigName = "Default";
        public DeathCameraPhaseType[] PhaseSequence = { DeathCameraPhaseType.KillCam, DeathCameraPhaseType.DeathRecap, DeathCameraPhaseType.Spectator };
        [Tooltip("Key to skip current skippable phase")]
        public KeyCode SkipAllInput = KeyCode.Space;

        [Header("Kill Cam")]
        public bool KillCamEnabled = true;
        [Tooltip("Duration in seconds")]
        public float KillCamDuration = 3f;
        public float KillCamOrbitRadius = 5f;
        public float KillCamOrbitHeight = 3f;
        [Tooltip("Degrees per second")]
        public float KillCamOrbitSpeed = 30f;
        [Tooltip("Final orbit radius at end of kill cam (zoom-in)")]
        public float KillCamEndRadius = 2f;
        [Tooltip("Final orbit height at end of kill cam")]
        public float KillCamEndHeight = 1.5f;
        public bool KillCamSlowMotion = true;
        [Tooltip("Time.timeScale during kill cam")]
        public float KillCamTimeScale = 0.25f;
        [Tooltip("Camera blend-in duration (seconds) — ease from gameplay camera to orbit")]
        public float KillCamTransitionIn = 1.5f;

        [Header("Death Recap")]
        public bool DeathRecapEnabled = true;
        [Tooltip("Max display time (0 = manual skip only)")]
        public float DeathRecapDuration = 5f;
        public bool ShowDamageBreakdown = true;
        public bool ShowRespawnTimer = true;

        [Header("Spectator")]
        public bool SpectatorEnabled = true;
        [Tooltip("Allow TPS Orbit camera style (+ locked variant) in spectator")]
        public bool AllowTPSOrbit = true;
        [Tooltip("Allow Isometric Fixed camera style (+ locked variant) in spectator")]
        public bool AllowIsometric = true;
        [Tooltip("Allow Top-Down camera style (+ locked variant) in spectator")]
        public bool AllowTopDown = true;
        [Tooltip("Allow Isometric Rotatable camera style (+ locked variant) in spectator")]
        public bool AllowIsometricRotatable = true;
        [Tooltip("Allow free cam mode (disable for anti-cheat)")]
        public bool AllowFreeCam = true;
        public bool ShowSpectatorHUD = true;
        [Tooltip("Blend duration when switching followed player")]
        public float TransitionBetweenPlayers = 0.5f;
        [Tooltip("Blend-in duration from previous phase")]
        public float SpectatorTransitionIn = 0.5f;

        [Header("Follow Cam")]
        public float FollowDistance = 8f;
        public float FollowHeight = 1.6f;
        public float FollowSmoothTime = 0.15f;
        [Tooltip("Vertical offset for LookAt target (chest level)")]
        public float LookAtHeight = 1.6f;
        [Tooltip("Default pitch angle (degrees from horizontal)")]
        public float DefaultPitch = 25f;
        [Tooltip("Mouse orbit sensitivity")]
        [Range(0.01f, 0.5f)]
        public float OrbitSensitivity = 0.15f;

        [Header("Follow Cam Zoom")]
        [Tooltip("Minimum follow distance (closest zoom)")]
        public float ZoomDistanceMin = 2f;
        [Tooltip("Maximum follow distance (farthest zoom)")]
        public float ZoomDistanceMax = 15f;
        [Tooltip("Scroll wheel sensitivity for spectator zoom")]
        [Range(0.01f, 1f)]
        public float ZoomScrollSensitivity = 0.08f;

        [Header("Follow Cam Collision")]
        [Tooltip("Prevent camera from clipping through walls")]
        public bool EnableCollision = true;
        [Tooltip("Layer mask for collision detection")]
        public LayerMask CollisionLayers = ~0;
        [Tooltip("SphereCast radius for collision")]
        [Range(0.1f, 0.5f)]
        public float CollisionRadius = 0.25f;

        [Header("Isometric Fallback (used when gameplay CameraConfig unavailable)")]
        [Tooltip("Pitch angle for isometric death camera view")]
        [Range(30f, 75f)]
        public float IsometricAngle = 50f;
        [Tooltip("Camera Y-rotation (0=faces north, 90=faces east, 180=faces south, 270=faces west)")]
        [Range(0f, 360f)]
        public float IsometricRotation = 0f;
        [Tooltip("Height above target for isometric view")]
        public float IsometricHeight = 15f;

        [Header("Top-Down Fallback")]
        [Tooltip("Near-vertical angle for top-down death camera view")]
        [Range(60f, 90f)]
        public float TopDownAngle = 85f;
        [Tooltip("Height above target for top-down view")]
        public float TopDownHeight = 20f;

        [Header("Free Cam")]
        public float FreeCamSpeed = 10f;
        public float FreeCamFastMultiplier = 3f;
        public float FreeCamSensitivity = 2f;

        [Header("Respawn Transition")]
        public float RespawnTransitionDuration = 0.5f;

        [Header("General Camera")]
        public float FOV = 60f;
        public float NearClip = 0.1f;

        /// <summary>
        /// Build the ordered list of available spectator modes based on per-style toggles.
        /// Grouped by style: each enabled style adds its unlocked + locked variant.
        /// </summary>
        public List<DeathSpectatorMode> GetAvailableModes()
        {
            var modes = new List<DeathSpectatorMode>();
            if (AllowTPSOrbit)           { modes.Add(DeathSpectatorMode.TPSOrbit);           modes.Add(DeathSpectatorMode.TPSLocked); }
            if (AllowIsometric)          { modes.Add(DeathSpectatorMode.IsometricFixed);     modes.Add(DeathSpectatorMode.IsometricLocked); }
            if (AllowTopDown)            { modes.Add(DeathSpectatorMode.TopDown);            modes.Add(DeathSpectatorMode.TopDownLocked); }
            if (AllowIsometricRotatable) { modes.Add(DeathSpectatorMode.IsometricRotatable); modes.Add(DeathSpectatorMode.IsometricRotLocked); }
            if (AllowFreeCam)              modes.Add(DeathSpectatorMode.FreeCam);
            return modes;
        }

        /// <summary>
        /// Check if a given phase type is enabled in this config.
        /// </summary>
        public bool IsPhaseEnabled(DeathCameraPhaseType phase)
        {
            switch (phase)
            {
                case DeathCameraPhaseType.KillCam: return KillCamEnabled;
                case DeathCameraPhaseType.DeathRecap: return DeathRecapEnabled;
                case DeathCameraPhaseType.Spectator: return SpectatorEnabled;
                case DeathCameraPhaseType.RespawnTransition: return true; // Always enabled
                default: return false;
            }
        }
    }
}
