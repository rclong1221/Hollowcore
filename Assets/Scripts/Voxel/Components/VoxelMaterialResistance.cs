using Unity.Entities;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Material resistance data for voxel damage types.
    /// Used to modify incoming damage based on the voxel's material.
    /// </summary>
    public struct VoxelMaterialResistance : IComponentData
    {
        /// <summary>Resistance to Mining damage (pickaxes, drills). 0 = no resistance, 1 = immune.</summary>
        public float ResistanceToMining;
        
        /// <summary>Resistance to Explosive damage (grenades, dynamite). 0 = no resistance, 1 = immune.</summary>
        public float ResistanceToExplosive;
        
        /// <summary>Resistance to Heat damage (flamethrower, plasma). 0 = no resistance, 1 = immune.</summary>
        public float ResistanceToHeat;
        
        /// <summary>Resistance to Crush damage (hammers, heavy equipment). 0 = no resistance, 1 = immune.</summary>
        public float ResistanceToCrush;
        
        /// <summary>Resistance to Laser damage (mining laser, cutting beam). 0 = no resistance, 1 = immune.</summary>
        public float ResistanceToLaser;
        
        /// <summary>Get resistance for a specific damage type.</summary>
        public float GetResistance(VoxelDamageType damageType)
        {
            switch (damageType)
            {
                case VoxelDamageType.Mining:
                    return ResistanceToMining;
                case VoxelDamageType.Explosive:
                    return ResistanceToExplosive;
                case VoxelDamageType.Heat:
                    return ResistanceToHeat;
                case VoxelDamageType.Crush:
                    return ResistanceToCrush;
                case VoxelDamageType.Laser:
                    return ResistanceToLaser;
                default:
                    return 0f;
            }
        }
        
        // ========== PRESET MATERIALS ==========
        
        /// <summary>Dirt - easy to mine, moderate explosion resistance.</summary>
        public static VoxelMaterialResistance Dirt => new VoxelMaterialResistance
        {
            ResistanceToMining = 0f,
            ResistanceToExplosive = 0.2f,
            ResistanceToHeat = 0.1f,
            ResistanceToCrush = 0f,
            ResistanceToLaser = 0.3f
        };
        
        /// <summary>Stone - harder to mine, moderate resistances.</summary>
        public static VoxelMaterialResistance Stone => new VoxelMaterialResistance
        {
            ResistanceToMining = 0.5f,
            ResistanceToExplosive = 0.1f,
            ResistanceToHeat = 0.4f,
            ResistanceToCrush = 0.3f,
            ResistanceToLaser = 0.2f
        };
        
        /// <summary>Metal ore - hard to mine, weak to explosives.</summary>
        public static VoxelMaterialResistance MetalOre => new VoxelMaterialResistance
        {
            ResistanceToMining = 0.7f,
            ResistanceToExplosive = -0.5f, // Negative = takes MORE damage
            ResistanceToHeat = 0.3f,
            ResistanceToCrush = 0.4f,
            ResistanceToLaser = 0.1f
        };
        
        /// <summary>Crystal - weak to explosives and crush, resistant to laser.</summary>
        public static VoxelMaterialResistance Crystal => new VoxelMaterialResistance
        {
            ResistanceToMining = 0.3f,
            ResistanceToExplosive = -0.3f,
            ResistanceToHeat = 0.5f,
            ResistanceToCrush = -0.2f,
            ResistanceToLaser = 0.8f
        };
        
        /// <summary>Ice - weak to heat, resistant to mining.</summary>
        public static VoxelMaterialResistance Ice => new VoxelMaterialResistance
        {
            ResistanceToMining = 0.4f,
            ResistanceToExplosive = 0.2f,
            ResistanceToHeat = -0.8f, // Very weak to heat
            ResistanceToCrush = 0.1f,
            ResistanceToLaser = -0.5f
        };
        
        /// <summary>Bedrock - nearly immune to everything.</summary>
        public static VoxelMaterialResistance Bedrock => new VoxelMaterialResistance
        {
            ResistanceToMining = 0.99f,
            ResistanceToExplosive = 0.95f,
            ResistanceToHeat = 0.99f,
            ResistanceToCrush = 0.99f,
            ResistanceToLaser = 0.99f
        };
    }
}
