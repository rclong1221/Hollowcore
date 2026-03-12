using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// ScriptableObject defining a loadout of player abilities.
    /// Contains a flat array of ability definitions that gets baked into a BlobAsset.
    /// Place in Resources/ folder as "AbilityLoadout" for auto-loading.
    ///
    /// Create via: Assets > Create > DIG/Combat/Ability Loadout
    ///
    /// EPIC 18.19 - Phase 6
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityLoadout", menuName = "DIG/Combat/Ability Loadout", order = 2)]
    public class AbilityLoadoutSO : ScriptableObject
    {
        [Header("Ability Definitions")]
        [Tooltip("All ability definitions available to players. Order determines database index.")]
        public AbilityDefinitionSO[] abilities;

        [Header("Default Slot Assignments")]
        [Tooltip("Default ability IDs for slots 0-5. Use -1 for empty slots.")]
        public int[] defaultSlotAbilityIds = new int[6] { -1, -1, -1, -1, -1, -1 };

        /// <summary>
        /// Bakes all ability definitions into a BlobAsset.
        /// </summary>
        /// <summary>
        /// Bakes all ability definitions into a BlobAsset.
        /// IMPORTANT: The blob array index IS the runtime AbilityId.
        /// User-facing IDs from AbilityDefinitionSO are stored as DesignerAbilityId
        /// for editor/debug, but all runtime lookups use the blob index.
        /// </summary>
        public BlobAssetReference<AbilityDatabaseBlob> BakeToBlob()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AbilityDatabaseBlob>();

            int count = abilities != null ? abilities.Length : 0;
            var blobArray = builder.Allocate(ref root.Abilities, count);

            for (int i = 0; i < count; i++)
            {
                if (abilities[i] != null)
                {
                    var def = abilities[i].ToBlobDef();
                    def.AbilityId = i; // Runtime AbilityId = blob array index
                    blobArray[i] = def;
                }
                else
                {
                    blobArray[i] = default;
                }
            }

            var result = builder.CreateBlobAssetReference<AbilityDatabaseBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        /// <summary>
        /// Resolves a designer-facing ability ID to the blob array index.
        /// Returns -1 if not found.
        /// </summary>
        public int ResolveDesignerIdToBlobIndex(int designerAbilityId)
        {
            if (abilities == null) return -1;
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] != null && abilities[i].abilityId == designerAbilityId)
                    return i;
            }
            return -1;
        }

        private void OnValidate()
        {
            // Ensure default slot array is always 6 elements
            if (defaultSlotAbilityIds == null || defaultSlotAbilityIds.Length != 6)
            {
                var old = defaultSlotAbilityIds;
                defaultSlotAbilityIds = new int[6] { -1, -1, -1, -1, -1, -1 };
                if (old != null)
                {
                    for (int i = 0; i < Mathf.Min(old.Length, 6); i++)
                        defaultSlotAbilityIds[i] = old[i];
                }
            }

            // Validate ability IDs are unique
            if (abilities != null)
            {
                for (int i = 0; i < abilities.Length; i++)
                {
                    if (abilities[i] == null) continue;
                    for (int j = i + 1; j < abilities.Length; j++)
                    {
                        if (abilities[j] == null) continue;
                        if (abilities[i].abilityId == abilities[j].abilityId)
                        {
                            Debug.LogWarning($"[AbilityLoadoutSO] {name}: Duplicate ability ID {abilities[i].abilityId} " +
                                $"at indices {i} ({abilities[i].name}) and {j} ({abilities[j].name}).");
                        }
                    }
                }
            }
        }
    }
}
