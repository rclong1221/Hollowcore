using DIG.CameraSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Phase types in the death camera sequence.
    /// </summary>
    public enum DeathCameraPhaseType : byte
    {
        KillCam = 0,
        DeathRecap = 1,
        Spectator = 2,
        RespawnTransition = 3
    }

    /// <summary>
    /// EPIC 18.13: Camera styles available during spectator phase.
    /// Each follow-style has an unlocked and locked variant.
    /// Grouped by style for TAB cycling.
    /// </summary>
    public enum DeathSpectatorMode : byte
    {
        TPSOrbit = 0,            // Third-person orbit (mouse controls angle)
        TPSLocked = 1,           // Third-person locked (follows player's camera)
        IsometricFixed = 2,      // Fixed isometric angle
        IsometricLocked = 3,     // Isometric locked
        TopDown = 4,             // Top-down fixed angle
        TopDownLocked = 5,       // Top-down locked
        IsometricRotatable = 6,  // Isometric with Q/E rotation
        IsometricRotLocked = 7,  // Isometric rotatable locked
        FreeCam = 8              // Free fly camera
    }

    /// <summary>
    /// Extension methods for DeathSpectatorMode.
    /// </summary>
    public static class DeathSpectatorModeExtensions
    {
        public static bool IsLocked(this DeathSpectatorMode m)
            => m == DeathSpectatorMode.TPSLocked
            || m == DeathSpectatorMode.IsometricLocked
            || m == DeathSpectatorMode.TopDownLocked
            || m == DeathSpectatorMode.IsometricRotLocked;

        public static bool IsFreeCam(this DeathSpectatorMode m)
            => m == DeathSpectatorMode.FreeCam;

        /// <summary>
        /// Maps spectator mode to the CameraMode used by DeathFollowCam.SetCameraStyle().
        /// </summary>
        public static CameraMode ToCameraMode(this DeathSpectatorMode m) => m switch
        {
            DeathSpectatorMode.TPSOrbit or DeathSpectatorMode.TPSLocked
                => CameraMode.ThirdPersonFollow,
            DeathSpectatorMode.IsometricFixed or DeathSpectatorMode.IsometricLocked
                => CameraMode.IsometricFixed,
            DeathSpectatorMode.TopDown or DeathSpectatorMode.TopDownLocked
                => CameraMode.TopDownFixed,
            DeathSpectatorMode.IsometricRotatable or DeathSpectatorMode.IsometricRotLocked
                => CameraMode.IsometricRotatable,
            _ => CameraMode.ThirdPersonFollow,
        };

        /// <summary>
        /// Maps gameplay CameraMode to the default spectator mode at death time.
        /// </summary>
        public static DeathSpectatorMode FromGameplayMode(CameraMode gameplay) => gameplay switch
        {
            CameraMode.ThirdPersonFollow  => DeathSpectatorMode.TPSOrbit,
            CameraMode.IsometricFixed     => DeathSpectatorMode.IsometricFixed,
            CameraMode.TopDownFixed       => DeathSpectatorMode.TopDown,
            CameraMode.IsometricRotatable => DeathSpectatorMode.IsometricRotatable,
            CameraMode.FirstPerson        => DeathSpectatorMode.TPSOrbit,
            _ => DeathSpectatorMode.TPSOrbit,
        };

        public static string DisplayName(this DeathSpectatorMode m) => m switch
        {
            DeathSpectatorMode.TPSOrbit           => "TPS ORBIT",
            DeathSpectatorMode.TPSLocked          => "TPS LOCKED",
            DeathSpectatorMode.IsometricFixed     => "ISOMETRIC",
            DeathSpectatorMode.IsometricLocked    => "ISO LOCKED",
            DeathSpectatorMode.TopDown            => "TOP DOWN",
            DeathSpectatorMode.TopDownLocked      => "TD LOCKED",
            DeathSpectatorMode.IsometricRotatable => "ISO ROTATE",
            DeathSpectatorMode.IsometricRotLocked => "ISO ROT LOCKED",
            DeathSpectatorMode.FreeCam            => "FREE CAM",
            _ => "SPECTATING",
        };

        public static string ControlsHint(this DeathSpectatorMode m) => m switch
        {
            DeathSpectatorMode.TPSOrbit
                => "[TAB] Cycle  [1-9] Player  [Scroll] Zoom  [Mouse] Orbit",
            DeathSpectatorMode.IsometricRotatable or DeathSpectatorMode.IsometricRotLocked
                => "[TAB] Cycle  [1-9] Player  [Scroll] Zoom  [Q/E] Rotate",
            DeathSpectatorMode.FreeCam
                => "[TAB] Cycle  [WASD] Move  [Shift] Fast  [Mouse] Look",
            _ => "[TAB] Cycle  [1-9] Player  [Scroll] Zoom",
        };
    }

    /// <summary>
    /// EPIC 18.13: Current state of the death camera orchestrator.
    /// </summary>
    public enum DeathCameraState : byte
    {
        Inactive = 0,
        RunningPhases = 1,
        WaitingForRespawn = 2
    }

    /// <summary>
    /// EPIC 18.13: Entry in the spectator player list.
    /// </summary>
    public struct PlayerListEntry
    {
        public ushort GhostId;
        public string Name;
        public bool IsAlive;
    }

    /// <summary>
    /// Conditional debug logging for the death camera system.
    /// Calls are stripped entirely from release builds, eliminating
    /// string formatting GC allocations and I/O overhead.
    /// </summary>
    public static class DCamLog
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }
    }
}
