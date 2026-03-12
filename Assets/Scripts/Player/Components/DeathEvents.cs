using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Event added when an entity is about to die (Health <= 0). (13.16.10)
    /// Systems can process this event and set Cancelled = true to prevent death 
    /// (e.g. God Mode, Last Stand, Second Wind).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct WillDieEvent : IComponentData, IEnableableComponent
    {
        [GhostField] public bool Cancelled;
        [GhostField] public Entity Killer;
    }

    /// <summary>
    /// Event added when an entity has died (Transitioned to Dead/Downed). (13.16.10)
    /// Used for triggering death effects, logging, etc.
    /// Should be removed after processing.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DiedEvent : IComponentData, IEnableableComponent
    {
        [GhostField] public Entity Killer;
    }
}
