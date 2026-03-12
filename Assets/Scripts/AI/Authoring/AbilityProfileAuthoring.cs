using UnityEngine;
using Unity.Entities;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Authoring component that bakes AbilityProfileSO into ECS buffers.
    /// Add to enemy prefabs. If absent, AIBrainAuthoring generates a fallback melee ability.
    /// </summary>
    [AddComponentMenu("DIG/AI/Ability Profile")]
    public class AbilityProfileAuthoring : MonoBehaviour
    {
        [Tooltip("The ability profile ScriptableObject containing this enemy's ability rotation.")]
        public AbilityProfileSO Profile;

        class Baker : Baker<AbilityProfileAuthoring>
        {
            public override void Bake(AbilityProfileAuthoring authoring)
            {
                if (authoring.Profile == null) return;
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var abilityBuffer = AddBuffer<AbilityDefinition>(entity);
                var cooldownBuffer = AddBuffer<AbilityCooldownState>(entity);

                foreach (var abilitySO in authoring.Profile.Abilities)
                {
                    if (abilitySO == null) continue;

                    abilityBuffer.Add(abilitySO.ToDefinition());
                    cooldownBuffer.Add(new AbilityCooldownState
                    {
                        CooldownRemaining = 0,
                        GlobalCooldownRemaining = 0,
                        CooldownGroupRemaining = 0,
                        ChargesRemaining = abilitySO.MaxCharges,
                        MaxCharges = abilitySO.MaxCharges,
                        ChargeRegenTimer = 0
                    });
                }

                // Register dependency on the SO so rebakes happen on change
                DependsOn(authoring.Profile);
                foreach (var ability in authoring.Profile.Abilities)
                {
                    if (ability != null)
                        DependsOn(ability);
                }
            }
        }
    }
}
