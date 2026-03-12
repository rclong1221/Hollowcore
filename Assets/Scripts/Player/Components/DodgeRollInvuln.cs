using Unity.Entities;

namespace Player.Components
{
    // Tag component: entity is currently invulnerable due to dodge/roll
    public struct DodgeRollInvuln : IComponentData, IEnableableComponent { }
}
