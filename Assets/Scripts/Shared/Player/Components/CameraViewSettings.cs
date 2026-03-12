using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    public enum CameraViewType
    {
        Combat = 0,     // Third Person (Shoulder/Orbit)
        Adventure = 1,  // Third Person (Free Orbit)
        FirstPerson = 2, // Locked to head
        TopDown = 3,    // Fixed angle
        PointClick = 4  // RTS style
    }

    /// <summary>
    /// Configuration for the active camera view behavior
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CameraViewConfig : IComponentData
    {
        [GhostField] public CameraViewType ActiveViewType;
        
        // Combat View Settings
        [GhostField(Quantization = 1000)] public float3 CombatPivotOffset; // e.g., (0.5, 1.7, 0)
        [GhostField(Quantization = 1000)] public float3 CombatCameraOffset; // e.g., (0, 0, -2)
        [GhostField] public float CombatMinPitch;
        [GhostField] public float CombatMaxPitch;
        
        // Adventure View Settings
        [GhostField(Quantization = 1000)] public float3 AdventurePivotOffset;
        [GhostField] public float AdventureDistance;
        
        // First Person Settings
        [GhostField(Quantization = 1000)] public float3 FPSOffset; // Offset from head bone
    }
}
