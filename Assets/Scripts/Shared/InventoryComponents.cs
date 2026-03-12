using Unity.Entities;
using Unity.NetCode;

namespace DIG.Shared
{
    /// <summary>
    /// Buffer element for player inventory.
    /// Stores resource type and quantity.
    /// Moved to Shared to allow access from Interaction system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(8)]
    public struct InventoryItem : IBufferElementData
    {
        [GhostField] public ResourceType ResourceType;
        [GhostField] public int Quantity;
    }

    /// <summary>
    /// Tracks player's inventory capacity and weight.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InventoryCapacity : IComponentData
    {
        public float MaxWeight;
        [GhostField(Quantization = 100)] public float CurrentWeight;
        [GhostField] public bool IsOverencumbered;
        public float OverencumberedSpeedMultiplier;

        public static InventoryCapacity Default => new()
        {
            MaxWeight = 100f,
            CurrentWeight = 0f,
            IsOverencumbered = false,
            OverencumberedSpeedMultiplier = 0.5f
        };
    }

    /// <summary>
    /// Singleton containing resource weight values.
    /// </summary>
    public struct ResourceWeights : IComponentData
    {
        public float StoneWeight;
        public float MetalWeight;
        public float BioMassWeight;
        public float CrystalWeight;
        public float TitanBoneWeight;
        public float ThermalGlassWeight;
        public float IsotopeWeight;

        public readonly float GetWeight(ResourceType type) => type switch
        {
            ResourceType.Stone => StoneWeight,
            ResourceType.Metal => MetalWeight,
            ResourceType.BioMass => BioMassWeight,
            ResourceType.Crystal => CrystalWeight,
            ResourceType.TitanBone => TitanBoneWeight,
            ResourceType.ThermalGlass => ThermalGlassWeight,
            ResourceType.Isotope => IsotopeWeight,
            _ => 0f
        };

        public static ResourceWeights Default => new()
        {
            StoneWeight = 2f,
            MetalWeight = 3f,
            BioMassWeight = 0.5f,
            CrystalWeight = 1f,
            TitanBoneWeight = 5f,
            ThermalGlassWeight = 2f,
            IsotopeWeight = 4f
        };
    }
}
