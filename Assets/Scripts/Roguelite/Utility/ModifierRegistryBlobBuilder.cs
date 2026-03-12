using Unity.Collections;
using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Burst-readable modifier definition. Indexed by position in BlobArray.
    /// </summary>
    public struct ModifierBlobEntry
    {
        public int ModifierId;
        public ModifierPolarity Polarity;
        public ModifierTarget Target;
        public int StatId;
        public float FloatValue;
        public bool IsMultiplicative;
        public bool Stackable;
        public int MaxStacks;
        public int RequiredAscensionLevel;
        public int HeatCost;
    }

    /// <summary>
    /// EPIC 23.4: BlobAsset containing all modifier definitions for Burst-compatible lookup.
    /// </summary>
    public struct ModifierRegistryBlob
    {
        public BlobArray<ModifierBlobEntry> Modifiers;
    }

    /// <summary>
    /// EPIC 23.4: Singleton holding the baked modifier registry blob.
    /// </summary>
    public struct ModifierRegistrySingleton : IComponentData
    {
        public BlobAssetReference<ModifierRegistryBlob> Registry;
    }

    /// <summary>
    /// EPIC 23.4: Burst-readable ascension tier data.
    /// </summary>
    public struct AscensionTierBlob
    {
        public byte Level;
        public float RewardMultiplier;
        public int BonusHeatBudget;
        public BlobArray<int> ForcedModifierIds;
    }

    /// <summary>
    /// EPIC 23.4: BlobAsset containing all ascension tiers.
    /// </summary>
    public struct AscensionBlob
    {
        public BlobArray<AscensionTierBlob> Tiers;
    }

    /// <summary>
    /// EPIC 23.4: Singleton holding the baked ascension definition blob.
    /// </summary>
    public struct AscensionSingleton : IComponentData
    {
        public BlobAssetReference<AscensionBlob> Ascension;
    }

    /// <summary>
    /// EPIC 23.4: Builds ModifierRegistryBlob and AscensionBlob from ScriptableObjects.
    /// Follows RunConfigBlobBuilder pattern.
    /// </summary>
    public static class ModifierRegistryBlobBuilder
    {
        public static BlobAssetReference<ModifierRegistryBlob> Build(RunModifierPoolSO pool)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModifierRegistryBlob>();

            var mods = pool.Modifiers ?? new System.Collections.Generic.List<RunModifierDefinition>();
            var array = builder.Allocate(ref root.Modifiers, mods.Count);
            for (int i = 0; i < mods.Count; i++)
            {
                var def = mods[i];
                array[i] = new ModifierBlobEntry
                {
                    ModifierId = def.ModifierId,
                    Polarity = def.Polarity,
                    Target = def.Target,
                    StatId = def.StatId,
                    FloatValue = def.FloatValue,
                    IsMultiplicative = def.IsMultiplicative,
                    Stackable = def.Stackable,
                    MaxStacks = def.MaxStacks,
                    RequiredAscensionLevel = def.RequiredAscensionLevel,
                    HeatCost = def.HeatCost
                };
            }

            var result = builder.CreateBlobAssetReference<ModifierRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        public static BlobAssetReference<AscensionBlob> BuildAscension(AscensionDefinitionSO def)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AscensionBlob>();

            var tiers = def.Tiers;
            var tierArray = builder.Allocate(ref root.Tiers, tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                tierArray[i].Level = tier.Level;
                tierArray[i].RewardMultiplier = tier.RewardMultiplier;
                tierArray[i].BonusHeatBudget = tier.BonusHeatBudget;

                var forcedIds = tier.ForcedModifierIds ?? System.Array.Empty<int>();
                var idArray = builder.Allocate(ref tierArray[i].ForcedModifierIds, forcedIds.Length);
                for (int j = 0; j < forcedIds.Length; j++)
                    idArray[j] = forcedIds[j];
            }

            var result = builder.CreateBlobAssetReference<AscensionBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
