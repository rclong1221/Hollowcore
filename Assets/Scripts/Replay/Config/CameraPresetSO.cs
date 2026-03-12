using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Camera preset for spectator/replay camera modes.
    /// Create via: Create > DIG > Replay > Camera Preset
    /// </summary>
    [CreateAssetMenu(fileName = "CameraPreset", menuName = "DIG/Replay/Camera Preset")]
    public class CameraPresetSO : ScriptableObject
    {
        public string PresetName = "Default";
        public SpectatorCameraMode Mode = SpectatorCameraMode.FreeCam;

        [Header("Free Cam")]
        public float MoveSpeed = 10f;
        public float FastMoveMultiplier = 3f;
        public float MouseSensitivity = 2f;

        [Header("Follow")]
        public float FollowDistance = 8f;
        public float FollowHeight = 1.6f;
        public float FollowSmoothTime = 0.15f;

        [Header("Orbit")]
        public float OrbitSpeed = 15f;
        public float OrbitRadius = 8f;
        public float OrbitHeight = 3f;

        [Header("General")]
        public float FOV = 60f;
        public float NearClip = 0.1f;
    }
}
