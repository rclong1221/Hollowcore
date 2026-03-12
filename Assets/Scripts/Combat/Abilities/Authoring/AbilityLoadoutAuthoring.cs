using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// MonoBehaviour authoring component for player entities.
    /// References an AbilityLoadoutSO to bake PlayerAbilitySlot buffer
    /// and PlayerAbilityState onto the player entity.
    ///
    /// Add to the player prefab alongside other player authoring components.
    ///
    /// EPIC 18.19 - Phase 6
    /// </summary>
    public class AbilityLoadoutAuthoring : MonoBehaviour
    {
        [Tooltip("Ability loadout to bake onto this entity.")]
        public AbilityLoadoutSO loadout;

        public class Baker : Baker<AbilityLoadoutAuthoring>
        {
            public override void Bake(AbilityLoadoutAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add ability state
                AddComponent(entity, PlayerAbilityState.Default);

                // Add ability slot buffer
                var slotBuffer = AddBuffer<PlayerAbilitySlot>(entity);

                if (authoring.loadout != null)
                {
                    // Pre-fill slots from loadout defaults
                    // Designer IDs are resolved to blob indices (runtime AbilityId = blob index)
                    var defaults = authoring.loadout.defaultSlotAbilityIds;
                    int slotCount = defaults != null ? Mathf.Min(defaults.Length, 6) : 6;

                    for (byte i = 0; i < slotCount; i++)
                    {
                        int designerId = (defaults != null && i < defaults.Length) ? defaults[i] : -1;
                        int blobIndex = designerId >= 0
                            ? authoring.loadout.ResolveDesignerIdToBlobIndex(designerId)
                            : -1;

                        slotBuffer.Add(new PlayerAbilitySlot
                        {
                            AbilityId = blobIndex,
                            SlotIndex = i,
                            CooldownRemaining = 0f,
                            ChargesRemaining = 0,
                            ChargeRechargeElapsed = 0f
                        });
                    }

                    // Initialize charge counts from ability definitions
                    if (authoring.loadout.abilities != null)
                    {
                        for (int i = 0; i < slotBuffer.Length; i++)
                        {
                            var slot = slotBuffer[i];
                            if (slot.AbilityId < 0 || slot.AbilityId >= authoring.loadout.abilities.Length)
                                continue;

                            var abDef = authoring.loadout.abilities[slot.AbilityId];
                            if (abDef != null && abDef.maxCharges > 0)
                            {
                                slot.ChargesRemaining = (byte)Mathf.Clamp(abDef.maxCharges, 0, 255);
                                slotBuffer[i] = slot;
                            }
                        }
                    }
                }
                else
                {
                    // No loadout — create 6 empty slots
                    for (byte i = 0; i < 6; i++)
                    {
                        slotBuffer.Add(PlayerAbilitySlot.Empty(i));
                    }
                }
            }
        }
    }
}
