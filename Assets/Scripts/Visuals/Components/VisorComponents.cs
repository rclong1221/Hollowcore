using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Visuals.Components
{
    /// <summary>
    /// Tracks the state of the player's helmet visor (physical damage, dirt, ice).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct HelmetVisor : IComponentData
    {
        [GhostField] public float CrackLevel; // 0-1
        [GhostField] public float DirtLevel;  // 0-1
        [GhostField] public float IceLevel;   // 0-1
        public float WipeTimer;
    }

    /// <summary>
    /// Configuration for the Diegetic HUD (sway, opacity).
    /// </summary>
    public struct DiegeticHUD : IComponentData
    {
        public float MasterOpacity;
        public float GlitchAmount;
        public float2 SwayOffset;
    }

    /// <summary>
    /// State of the player's flashlight.
    /// Needs to exist on ALL ghost types so clients can see remote player flashlights.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FlashlightData : IComponentData
    {
        [GhostField] public bool IsOn;
        [GhostField] public float BatteryCurrent; // Seconds
        [GhostField] public float BatteryMax;     // Seconds

        // Settings (replicated so spawned ghosts have correct values)
        [GhostField] public float Intensity;
        [GhostField] public float Range;
        [GhostField] public float DrainRate;
        [GhostField] public float RechargeRate;
        [GhostField] public bool RechargeEnabled;

        // Input Tracking (local only - not replicated)
        public uint LastInputFrame;
    }

    /// <summary>
    /// Managed component to hold reference to the Unity Light component.
    /// </summary>
    public class FlashlightReference : IComponentData
    {
        public Light LightSource;
    }

    /// <summary>
    /// Managed component to hold reference to the HUD/Visor Material or Transform for sway.
    /// </summary>
    public class VisorReference : IComponentData
    {
        public Material VisorMaterial; // For setting cracks
        public Transform HudRoot;      // For sway
    }
}
