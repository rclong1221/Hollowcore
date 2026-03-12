using UnityEngine;
using System.Collections.Generic;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Registry of all voxel materials.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Material Registry")]
    public class VoxelMaterialRegistry : ScriptableObject
    {
        public List<VoxelMaterialDefinition> Materials = new List<VoxelMaterialDefinition>();
        
        private VoxelMaterialDefinition[] _lookupTable;
        
        public void Initialize()
        {
            _lookupTable = new VoxelMaterialDefinition[256];
            
            foreach (var mat in Materials)
            {
                if (mat != null)
                {
                    _lookupTable[mat.MaterialID] = mat;
                }
            }
        }
        
        public VoxelMaterialDefinition GetMaterial(byte id)
        {
            // Auto-initialize if needed (e.g. at runtime first access)
            if (_lookupTable == null) Initialize();
            
            if (id >= _lookupTable.Length) return null;
            return _lookupTable[id];
        }
        
        private void OnEnable()
        {
            if (Materials != null && Materials.Count > 0)
            {
                Initialize();
            }
        }
    }
}
