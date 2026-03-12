using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    // Input authored per-player entity. Systems can read this to drive movement.
    public struct PlayerInputComponent : IComponentData
    {
        public float2 Move;    // x = horizontal, y = vertical (local space)
        public float2 LookDelta;
        public float ZoomDelta;
        public byte Jump;      // 0/1
        public byte Crouch;    // 0/1
        public byte Sprint;    // 0/1
        public byte Slide;     // 0/1 - trigger a slide
        public byte DodgeRoll; // 0/1 - trigger a dodge/roll move
        public byte DodgeDive; // 0/1 - trigger a forward dive ending in prone
        public byte Prone; // 0/1 - toggle prone
        // Lean inputs (press to peek). Use mutually-exclusive input handling in systems.
        public byte LeanLeft;  // 0/1
        public byte LeanRight; // 0/1
    }

    // Note: NetCode/Predicted `PlayerInput` and `InputEvent` live in the global namespace
    // in `PlayerInput_Global.cs` to satisfy generated code expectations. This file
    // only contains the hybrid `PlayerInputComponent` used by MonoBehaviour systems
    // and `PlayerInputSettings`.

    /// <summary>
    /// Component to store player input settings (sensitivity, dead zones, etc.)
    /// </summary>
    public struct PlayerInputSettings : IComponentData
    {
        public float MouseSensitivity;
        public float MouseSensitivityADS; // Aim down sights sensitivity
        public float DeadZone;
        public bool InvertY;

        public static PlayerInputSettings Default => new PlayerInputSettings
        {
            MouseSensitivity = 1.0f,
            MouseSensitivityADS = 0.5f,
            DeadZone = 0.1f,
            InvertY = false
        };
    }
}

