using UnityEngine;
using System.Collections.Generic;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Defines properties of a voxel material.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Material Definition")]
    public class VoxelMaterialDefinition : ScriptableObject
    {
        [Header("Identification")]
        public byte MaterialID;
        public string MaterialName;
        
        [Header("Mining")]
        public float Hardness = 1f;  // Time to mine
        public bool IsMineable = true;
        
        [Header("Loot")]
        public GameObject LootPrefab;  // What drops when mined
        public int MinDropCount = 1;
        public int MaxDropCount = 1;
        public float DropChance = 1f;
        
        [Tooltip("Mass multiplier for loot physics (ore = 1.5, stone = 1.0, dirt = 0.7)")]
        [Range(0.1f, 3f)]
        public float LootMassMultiplier = 1f;
        
        [Header("Visuals")]
        public VoxelVisualMaterial VisualMaterial; // Link to visual definition
        public Color DebugColor = Color.gray;
        public int TextureArrayIndex;  // For texture array shader
        
        [Header("Damage Resistance (EPIC 15.10)")]
        [Tooltip("Resistance to Mining damage (picks, drills). 0 = normal, 1 = immune, negative = weak.")]
        [Range(-1f, 1f)]
        public float ResistanceToMining = 0f;
        
        [Tooltip("Resistance to Explosive damage (grenades, dynamite). 0 = normal, 1 = immune, negative = weak.")]
        [Range(-1f, 1f)]
        public float ResistanceToExplosive = 0f;
        
        [Tooltip("Resistance to Heat damage (flamethrower, plasma). 0 = normal, 1 = immune, negative = weak.")]
        [Range(-1f, 1f)]
        public float ResistanceToHeat = 0f;
        
        [Tooltip("Resistance to Crush damage (hammers, heavy equipment). 0 = normal, 1 = immune, negative = weak.")]
        [Range(-1f, 1f)]
        public float ResistanceToCrush = 0f;
        
        [Tooltip("Resistance to Laser damage (mining laser, cutting beam). 0 = normal, 1 = immune, negative = weak.")]
        [Range(-1f, 1f)]
        public float ResistanceToLaser = 0f;
        
        /// <summary>
        /// Get resistance for a specific damage type.
        /// </summary>
        public float GetResistance(DIG.Voxel.VoxelDamageType damageType)
        {
            switch (damageType)
            {
                case DIG.Voxel.VoxelDamageType.Mining:
                    return ResistanceToMining;
                case DIG.Voxel.VoxelDamageType.Explosive:
                    return ResistanceToExplosive;
                case DIG.Voxel.VoxelDamageType.Heat:
                    return ResistanceToHeat;
                case DIG.Voxel.VoxelDamageType.Crush:
                    return ResistanceToCrush;
                case DIG.Voxel.VoxelDamageType.Laser:
                    return ResistanceToLaser;
                default:
                    return 0f;
            }
        }
    }
}
