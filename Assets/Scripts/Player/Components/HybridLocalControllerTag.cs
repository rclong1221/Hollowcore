using Unity.Entities;

namespace Player.Components
{
    // Marker component: when present on a player entity, the DOTS CharacterController
    // will skip moving this entity so a local MonoBehaviour (KinematicCharacterController)
    // can drive the visual/kinematic motion on the client for designer iteration.
    public struct HybridLocalControllerTag : IComponentData { }
}
