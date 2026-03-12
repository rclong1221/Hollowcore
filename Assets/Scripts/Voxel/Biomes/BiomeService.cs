using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Biomes
{
    /// <summary>
    /// Burst-compatible Service for biome lookups.
    /// Uses NativeArrays to store biome data for jobs.
    /// </summary>
    public static class BiomeService
    {
        // Burst-compatible biome data
        public struct BiomeParams
        {
            public byte BiomeID;
            
            // Materials
            public byte SurfaceMaterialID;
            public byte SubsurfaceMaterialID;
            public byte WallMaterialID;
            
            // Conditions
            public float MinTemp;
            public float MaxTemp;
            public float MinHum;
            public float MaxHum;
            
            // Environment
            public float OreSpawnMultiplier;
            public byte FluidType; // 0 if no override
        }
        
        private static NativeArray<BiomeParams> _biomeParams;
        private static NativeList<BiomeParams> _solidLayerBiomes; // Subset for solid layer mixing
        private static float _noiseScale;
        
        public static bool IsInitialized { get; private set; }
        
        public static NativeArray<BiomeParams> AllBiomes => _biomeParams;
        public static NativeList<BiomeParams> SolidLayerBiomes => _solidLayerBiomes;
        public static float NoiseScale => _noiseScale;
        
        private static int _referenceCount;
        
        public static void Initialize(BiomeRegistry registry)
        {
            _referenceCount++;
            if (IsInitialized) return;
            
            if (registry == null || registry.Biomes == null)
            {
                UnityEngine.Debug.LogWarning("[BiomeService] Valid registry required");
                return;
            }
            
            // Find max ID
            int maxId = 0;
            foreach (var b in registry.Biomes)
            {
                if (b != null && b.BiomeID > maxId) maxId = b.BiomeID;
            }
            
            // Safety: Ensure we don't leak if state was desynced
            if (_biomeParams.IsCreated) _biomeParams.Dispose();
            if (_solidLayerBiomes.IsCreated) _solidLayerBiomes.Dispose();

            _biomeParams = new NativeArray<BiomeParams>(maxId + 1, Allocator.Persistent);
            _solidLayerBiomes = new NativeList<BiomeParams>(Allocator.Persistent);
            _noiseScale = registry.GlobalNoiseScale;
            
            // Fill arrays
            foreach (var b in registry.Biomes)
            {
                if (b == null) continue;
                
                var p = new BiomeParams
                {
                    BiomeID = b.BiomeID,
                    SurfaceMaterialID = b.SurfaceMaterial ? b.SurfaceMaterial.MaterialID : (byte)1,
                    SubsurfaceMaterialID = b.SubsurfaceMaterial ? b.SubsurfaceMaterial.MaterialID : (byte)1,
                    WallMaterialID = b.WallMaterial ? b.WallMaterial.MaterialID : (byte)1,
                    MinTemp = b.MinTemperature,
                    MaxTemp = b.MaxTemperature,
                    MinHum = b.MinHumidity,
                    MaxHum = b.MaxHumidity,
                    OreSpawnMultiplier = b.OreSpawnMultiplier,
                    FluidType = b.FluidOverride ? (byte)b.FluidOverride.Type : (byte)0
                };
                
                _biomeParams[b.BiomeID] = p;
                
                // Add to solid list if it has valid ranges (not just explicit for hollow)
                // Assuming biomes with full -1 to 1 range might be generic defaults, 
                // but usually specific ranges indicate placement rules.
                // For now, add all that have ranges defined.
                _solidLayerBiomes.Add(p);
            }
            
            IsInitialized = true;
            UnityEngine.Debug.Log($"[BiomeService] Initialized with {_biomeParams.Length} biomes");
        }
        
        public static void Dispose()
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                if (_biomeParams.IsCreated) _biomeParams.Dispose();
                if (_solidLayerBiomes.IsCreated) _solidLayerBiomes.Dispose();
                IsInitialized = false;
                _referenceCount = 0;
            }
        }
    }
    
    /// <summary>
    /// Static lookup methods.
    /// </summary>
    public static class BiomeLookup
    {
        public static byte GetBiomeAt(
            float3 worldPos, 
            float noiseScale, 
            uint seed, 
            in NativeArray<BiomeService.BiomeParams> candidates)
        {
            float2 xz = worldPos.xz;
            
            // Sample temp and humidity
            float temp = noise.snoise(xz * noiseScale + seed);
            float hum = noise.snoise(xz * noiseScale * 1.3f + seed + 2000);
            
            // Find matching biome (simple first match or best match)
            // Iterate candidates
            for (int i = 0; i < candidates.Length; i++)
            {
                var b = candidates[i];
                if (temp >= b.MinTemp && temp <= b.MaxTemp &&
                    hum >= b.MinHum && hum <= b.MaxHum)
                {
                    return b.BiomeID;
                }
            }
            
            // Return first (fallback)
            return candidates.Length > 0 ? candidates[0].BiomeID : (byte)0;
        }
        
        public static BiomeService.BiomeParams GetBiomeData(byte id, in NativeArray<BiomeService.BiomeParams> allBiomes)
        {
            if (id < allBiomes.Length)
                return allBiomes[id];
            return default;
        }
    }
}
