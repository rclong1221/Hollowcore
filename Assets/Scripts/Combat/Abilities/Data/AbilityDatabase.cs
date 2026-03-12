using Unity.Entities;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Blob data containing all player ability definitions.
    /// Loaded once at startup and shared across all player entities.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    public struct AbilityDatabaseBlob
    {
        public BlobArray<AbilityDef> Abilities;
    }

    /// <summary>
    /// ECS singleton holding the blob asset reference to the ability database.
    /// Created by AbilityDatabaseBootstrapSystem at startup.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    public struct AbilityDatabaseRef : IComponentData
    {
        public BlobAssetReference<AbilityDatabaseBlob> Value;

        /// <summary>
        /// Look up an ability by ID (== blob array index). O(1) direct index.
        /// </summary>
        public bool TryGetAbility(int abilityId, out AbilityDef def)
        {
            ref var abilities = ref Value.Value.Abilities;
            if (abilityId >= 0 && abilityId < abilities.Length)
            {
                def = abilities[abilityId];
                return true;
            }
            def = default;
            return false;
        }
    }
}
