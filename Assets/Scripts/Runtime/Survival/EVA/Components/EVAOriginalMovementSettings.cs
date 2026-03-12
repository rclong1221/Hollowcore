using Unity.Entities;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Stores original PlayerMovementSettings values before EVA modification.
    /// Used to restore settings when exiting EVA mode.
    /// </summary>
    public struct EVAOriginalMovementSettings : IComponentData
    {
        /// <summary>
        /// True if original settings have been captured.
        /// </summary>
        public bool HasStoredSettings;

        /// <summary>
        /// Original walk speed before EVA modification.
        /// </summary>
        public float WalkSpeed;

        /// <summary>
        /// Original run speed before EVA modification.
        /// </summary>
        public float RunSpeed;

        /// <summary>
        /// Original sprint speed before EVA modification.
        /// </summary>
        public float SprintSpeed;

        /// <summary>
        /// Original crouch speed before EVA modification.
        /// </summary>
        public float CrouchSpeed;

        /// <summary>
        /// Original prone speed before EVA modification.
        /// </summary>
        public float ProneSpeed;

        /// <summary>
        /// Original jump force before EVA modification.
        /// </summary>
        public float JumpForce;

        /// <summary>
        /// Original air acceleration before EVA modification.
        /// </summary>
        public float AirAcceleration;

        /// <summary>
        /// Original gravity before EVA modification.
        /// </summary>
        public float Gravity;

        public static EVAOriginalMovementSettings Default => new EVAOriginalMovementSettings
        {
            HasStoredSettings = false,
            WalkSpeed = 0f,
            RunSpeed = 0f,
            SprintSpeed = 0f,
            CrouchSpeed = 0f,
            ProneSpeed = 0f,
            JumpForce = 0f,
            AirAcceleration = 0f,
            Gravity = 0f
        };
    }
}
