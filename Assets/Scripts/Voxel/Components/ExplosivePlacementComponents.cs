using Unity.Entities;
using Unity.NetCode;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// EPIC 15.10: Configuration for tools that place explosives (Dynamite, C4).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ExplosivePlacementConfig : IComponentData
    {
        [GhostField] public Entity ExplosivePrefab;
        [GhostField] public float PlacementRange;
        [GhostField] public bool CanPlaceOnWalls;
        [GhostField] public bool SubsurfacePlacement; // For dynamite in holes
        [GhostField] public float CooldownTime;
    }

    /// <summary>
    /// Runtime state for explosive placement tools.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ExplosivePlacementState : IComponentData
    {
        [GhostField] public float CooldownTimer;
    }

    /// <summary>
    /// EPIC 15.10: Component for a tool that remotely detonates placed explosives.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RemoteDetonator : IComponentData
    {
        [GhostField] public float Range;
        [GhostField] public float Cooldown;
    }

    /// <summary>
    /// Tag for explosives that can be remotely detonated.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RemoteExplosive : IComponentData { }
    
    /// <summary>
    /// Component linking an entity to its owner (player).
    /// Used for remote detonation.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct EntityOwner : IComponentData
    {
         [GhostField] public Entity OwnerEntity;
    }
}
