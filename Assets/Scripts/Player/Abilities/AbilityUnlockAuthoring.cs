using Unity.Entities;
using UnityEngine;

namespace DIG.Player.Abilities
{
    public enum AbilityType
    {
        None = 0,
        Jetpack = 1,
        Sprint = 2,
        Weapon = 3,
        // Add more as needed
    }

    public struct AbilityUnlockComponent : IComponentData
    {
        public AbilityType AbilityToUnlock;
        public bool Triggered;
    }

    public class AbilityUnlockAuthoring : MonoBehaviour
    {
        public AbilityType Ability = AbilityType.None;

        class Baker : Baker<AbilityUnlockAuthoring>
        {
            public override void Bake(AbilityUnlockAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AbilityUnlockComponent
                {
                    AbilityToUnlock = authoring.Ability,
                    Triggered = false
                });
            }
        }
    }
}
