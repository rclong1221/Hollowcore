using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Service for cave and hollow earth generation.
    /// Converts ScriptableObject data to Burst-compatible NativeArrays.
    /// </summary>
    public static class CaveGenerationService
    {
        // Burst-compatible cave parameters (all fields must be blittable - no bool!)
        public struct CaveParams
        {
            // Swiss Cheese (use byte instead of bool: 0 = disabled, 1 = enabled)
            public byte EnableSwissCheese;
            public float CheeseScale;
            public float CheeseThreshold;
            public float CheeseMinDepth;
            public float CheeseMaxDepth;
            
            // Spaghetti
            public byte EnableSpaghetti;
            public float SpaghettiScale;
            public float SpaghettiWidth;
            public float SpaghettiMinDepth;
            public float SpaghettiMaxDepth;
            
            // Noodles
            public byte EnableNoodles;
            public float NoodleScale;
            public float NoodleWidth;
            public float NoodleMinDepth;
            public float NoodleMaxDepth;
            
            // Caverns
            public byte EnableCaverns;
            public float CavernScale;
            public float CavernThreshold;
            public float CavernMinDepth;
        }
        
        // Burst-compatible hollow earth parameters (all fields must be blittable - no bool!)
        public struct HollowParams
        {
            public float TopDepth;          // Y where hollow starts (e.g., -400)
            public float BottomDepth;       // Y where hollow ends (e.g., -900)
            public float AverageHeight;     // Hollow height
            public float HeightVariation;
            
            // Floor
            public float FloorNoiseScale;
            public float FloorAmplitude;
            public byte FloorMaterialID;
            
            // Ceiling
            public float CeilingNoiseScale;
            public byte HasStalactites;     // 0 = no, 1 = yes
            public float MaxStalactiteLength;
            public float StalactiteDensity;
            
            // Pillars
            public byte GeneratePillars;    // 0 = no, 1 = yes
            public float PillarFrequency;
            public float MinPillarRadius;
            public float MaxPillarRadius;
            
            // Materials
            public byte WallMaterialID;
            
            // Fluids
            public byte FluidType;          // 0=none, 1=water, 3=lava
            public float FluidElevation;    // Height above floor
            public float FluidCoverage;     // 0-1 percentage
            public byte HasFluidRivers;     // 0 = no, 1 = yes
            public float RiverWidth;
        }
        
        // Burst-compatible layer data
        public struct LayerData
        {
            public int LayerIndex;
            public int Type;  // Cast from LayerType enum (0 = Solid, 1 = Hollow, 2 = Transition)
            public float TopDepth;
            public float BottomDepth;
            public float Thickness;
            public int CaveParamsIndex;    // Index into cave params array (-1 if none)
            public int HollowParamsIndex;  // Index into hollow params array (-1 if none)
        }
        
        // Static data arrays (Persistent allocation)
        private static NativeArray<LayerData> _layers;
        private static NativeArray<CaveParams> _caveParams;
        private static NativeArray<HollowParams> _hollowParams;
        private static bool _isInitialized;
        private static uint _seed;
        
        public static bool IsInitialized => _isInitialized;
        public static uint Seed => _seed;
        public static NativeArray<LayerData> Layers => _layers;
        public static NativeArray<CaveParams> CaveParamsArray => _caveParams;
        public static NativeArray<HollowParams> HollowParamsArray => _hollowParams;
        
        /// <summary>
        /// Initialize the cave generation service from WorldStructureConfig.
        /// </summary>
        public static void Initialize(WorldStructureConfig config)
        {
            if (config == null || config.Layers == null || config.Layers.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[CaveGenerationService] No layers configured");
                return;
            }
            
            Dispose();
            
            _seed = config.WorldSeed;
            
            // Count cave and hollow profiles needed
            int caveCount = 0;
            int hollowCount = 0;
            
            foreach (var layer in config.Layers)
            {
                if (layer == null) continue;
                if (layer.Type == LayerType.Solid && layer.CaveProfile != null) caveCount++;
                if (layer.Type == LayerType.Hollow && layer.HollowProfile != null) hollowCount++;
            }
            
            // Allocate arrays
            _layers = new NativeArray<LayerData>(config.Layers.Length, Allocator.Persistent);
            _caveParams = new NativeArray<CaveParams>(Mathf.Max(1, caveCount), Allocator.Persistent);
            _hollowParams = new NativeArray<HollowParams>(Mathf.Max(1, hollowCount), Allocator.Persistent);
            
            // Convert data
            int caveIndex = 0;
            int hollowIndex = 0;
            
            for (int i = 0; i < config.Layers.Length; i++)
            {
                var layer = config.Layers[i];
                if (layer == null) continue;
                
                var layerData = new LayerData
                {
                    LayerIndex = i,
                    Type = (int)layer.Type,
                    TopDepth = layer.TopDepth,
                    BottomDepth = layer.BottomDepth,
                    Thickness = layer.Thickness,
                    CaveParamsIndex = -1,
                    HollowParamsIndex = -1
                };
                
                // Convert cave profile
                if (layer.Type == LayerType.Solid && layer.CaveProfile != null)
                {
                    var cave = layer.CaveProfile;
                    _caveParams[caveIndex] = new CaveParams
                    {
                        EnableSwissCheese = cave.EnableSwissCheese ? (byte)1 : (byte)0,
                        CheeseScale = cave.CheeseScale,
                        CheeseThreshold = cave.CheeseThreshold,
                        CheeseMinDepth = cave.CheeseMinDepth,
                        CheeseMaxDepth = cave.CheeseMaxDepth,
                        EnableSpaghetti = cave.EnableSpaghetti ? (byte)1 : (byte)0,
                        SpaghettiScale = cave.SpaghettiScale,
                        SpaghettiWidth = cave.SpaghettiWidth,
                        SpaghettiMinDepth = cave.SpaghettiMinDepth,
                        SpaghettiMaxDepth = cave.SpaghettiMaxDepth,
                        EnableNoodles = cave.EnableNoodles ? (byte)1 : (byte)0,
                        NoodleScale = cave.NoodleScale,
                        NoodleWidth = cave.NoodleWidth,
                        NoodleMinDepth = cave.NoodleMinDepth,
                        NoodleMaxDepth = cave.NoodleMaxDepth,
                        EnableCaverns = cave.EnableCaverns ? (byte)1 : (byte)0,
                        CavernScale = cave.CavernScale,
                        CavernThreshold = cave.CavernThreshold,
                        CavernMinDepth = cave.CavernMinDepth
                    };
                    layerData.CaveParamsIndex = caveIndex;
                    caveIndex++;
                }
                
                // Convert hollow profile
                if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
                {
                    var hollow = layer.HollowProfile;
                    _hollowParams[hollowIndex] = new HollowParams
                    {
                        TopDepth = layer.TopDepth,
                        BottomDepth = layer.BottomDepth,
                        AverageHeight = hollow.AverageHeight,
                        HeightVariation = hollow.HeightVariation,
                        FloorNoiseScale = hollow.FloorNoiseScale,
                        FloorAmplitude = hollow.FloorAmplitude,
                        FloorMaterialID = (hollow.BiomeType != null && hollow.BiomeType.SurfaceMaterial != null) 
                            ? hollow.BiomeType.SurfaceMaterial.MaterialID 
                            : hollow.FloorMaterialID,
                        CeilingNoiseScale = hollow.CeilingNoiseScale,
                        HasStalactites = hollow.HasStalactites ? (byte)1 : (byte)0,
                        MaxStalactiteLength = hollow.MaxStalactiteLength,
                        StalactiteDensity = hollow.StalactiteDensity,
                        GeneratePillars = hollow.GeneratePillars ? (byte)1 : (byte)0,
                        PillarFrequency = hollow.PillarFrequency,
                        MinPillarRadius = hollow.MinPillarRadius,
                        MaxPillarRadius = hollow.MaxPillarRadius,
                        WallMaterialID = (hollow.BiomeType != null && hollow.BiomeType.WallMaterial != null) 
                            ? hollow.BiomeType.WallMaterial.MaterialID 
                            : hollow.WallMaterialID,
                        // Fluid parameters
                        FluidType = (byte)hollow.PrimaryFluidType,
                        FluidElevation = hollow.LakeElevation,
                        FluidCoverage = hollow.FluidCoverage,
                        HasFluidRivers = hollow.HasFluidRivers ? (byte)1 : (byte)0,
                        RiverWidth = hollow.RiverWidth
                    };
                    layerData.HollowParamsIndex = hollowIndex;
                    hollowIndex++;
                }
                
                _layers[i] = layerData;
            }
            
            _isInitialized = true;
            UnityEngine.Debug.Log($"[CaveGenerationService] Initialized with {config.Layers.Length} layers, {caveCount} cave profiles, {hollowCount} hollow profiles");
        }
        
        /// <summary>
        /// Dispose all native arrays.
        /// </summary>
        public static void Dispose()
        {
            if (_layers.IsCreated) _layers.Dispose();
            if (_caveParams.IsCreated) _caveParams.Dispose();
            if (_hollowParams.IsCreated) _hollowParams.Dispose();
            _isInitialized = false;
        }
        
        /// <summary>
        /// Get the layer index for a world Y position.
        /// </summary>
        public static int GetLayerIndexAt(float worldY)
        {
            if (!_isInitialized) return -1;
            
            for (int i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                if (worldY <= layer.TopDepth && worldY > layer.BottomDepth)
                    return i;
            }
            return -1;
        }
    }
    
    /// <summary>
    /// Burst-compatible static methods for cave generation.
    /// Called from GenerateVoxelDataJob.
    /// </summary>
    /// <summary>
    /// Burst-compatible static methods for cave generation.
    /// Called from GenerateVoxelDataJob.
    /// </summary>
    [BurstCompile]
    public static class CaveLookup
    {
        // Constants for layer types (matching LayerType enum)
        public const int LAYER_TYPE_SOLID = 0;
        public const int LAYER_TYPE_HOLLOW = 1;
        public const int LAYER_TYPE_TRANSITION = 2;
        
        /// <summary>
        /// Check if position should be air due to caves.
        /// Uses proper 3D worm algorithm for coherent tunnels.
        /// </summary>
        [BurstCompile]
        public static bool IsCaveAir(
            in float3 worldPos, 
            float depth,
            in CaveGenerationService.CaveParams cave,
            uint seed)
        {
            bool isAir = false;

            // Swiss Cheese - Random spherical pockets
            if (cave.EnableSwissCheese != 0 && 
                depth >= cave.CheeseMinDepth && 
                depth <= cave.CheeseMaxDepth)
            {
                float cheeseNoise = noise.snoise(worldPos * cave.CheeseScale + seed);
                isAir |= (cheeseNoise > cave.CheeseThreshold);
            }
            
            // Spaghetti Tunnels - 3D Worm Algorithm
            // Creates proper cylindrical tunnels by computing distance to a noise-driven centerline
            if (cave.EnableSpaghetti != 0 && 
                depth >= cave.SpaghettiMinDepth && 
                depth <= cave.SpaghettiMaxDepth)
            {
                // Compute worm centerline position using 3D noise
                // The worm "wiggles" through space based on low-frequency noise
                float wormFreq = cave.SpaghettiScale * 0.5f;
                float3 wormOffset = new float3(
                    noise.snoise(new float3(worldPos.y * wormFreq, worldPos.z * wormFreq, seed)) * 16f,
                    noise.snoise(new float3(worldPos.x * wormFreq, worldPos.z * wormFreq, seed + 1000)) * 8f,
                    noise.snoise(new float3(worldPos.x * wormFreq, worldPos.y * wormFreq, seed + 2000)) * 16f
                );
                
                // Distance from this point to the worm centerline
                float3 localPos = math.frac(worldPos * cave.SpaghettiScale) * 2f - 1f; // -1 to 1
                float distToWorm = math.length(localPos.xz - wormOffset.xz * 0.1f);
                
                // Tunnel radius based on Width parameter (scaled appropriately)
                float tunnelRadius = cave.SpaghettiWidth * 8f;
                isAir |= (distToWorm < tunnelRadius);
            }
            
            // Noodle Caves - Larger 3D Worm with more variation
            if (cave.EnableNoodles != 0 && 
                depth >= cave.NoodleMinDepth && 
                depth <= cave.NoodleMaxDepth)
            {
                // Slower-varying worm for larger passages
                float wormFreq = cave.NoodleScale * 0.3f;
                float3 wormOffset = new float3(
                    noise.snoise(new float3(worldPos.y * wormFreq, worldPos.z * wormFreq, seed + 10000)) * 24f,
                    noise.snoise(new float3(worldPos.x * wormFreq, worldPos.z * wormFreq, seed + 11000)) * 12f,
                    noise.snoise(new float3(worldPos.x * wormFreq, worldPos.y * wormFreq, seed + 12000)) * 24f
                );
                
                float3 localPos = math.frac(worldPos * cave.NoodleScale) * 2f - 1f;
                float distToWorm = math.length(localPos.xz - wormOffset.xz * 0.1f);
                
                // Wider tunnel radius for noodles
                float tunnelRadius = cave.NoodleWidth * 12f;
                isAir |= (distToWorm < tunnelRadius);
            }
            
            // Large Caverns - Spherical open spaces
            if (cave.EnableCaverns != 0 && depth >= cave.CavernMinDepth)
            {
                float cavernNoise = noise.snoise(worldPos * cave.CavernScale);
                float depthFactor = math.saturate((depth - cave.CavernMinDepth) / 100f) * 0.1f;
                isAir |= (cavernNoise > (cave.CavernThreshold - depthFactor));
            }
            
            return isAir;
        }
        
        /// <summary>
        /// Calculate density for hollow earth position.
        /// Returns negative for air, positive for solid.
        /// </summary>
        [BurstCompile]
        public static float GetHollowDensity(
            in float3 worldPos,
            in CaveGenerationService.HollowParams hollow,
            uint seed,
            out byte material)
        {
            // Default: Open air space
            float currentDensity = -1f;
            byte currentMaterial = 0;
            
            float worldY = worldPos.y;
            float2 xz = new float2(worldPos.x, worldPos.z);
            
            // Calculate floor height
            float floorNoise = noise.snoise(xz * hollow.FloorNoiseScale + seed);
            float floorHeight = hollow.BottomDepth + floorNoise * hollow.FloorAmplitude + hollow.FloorAmplitude;
            
            // Calculate ceiling height
            float ceilingNoise = noise.snoise(xz * hollow.CeilingNoiseScale + seed + 5000);
            float ceilingHeight = hollow.TopDepth - ceilingNoise * hollow.HeightVariation;
            
            // Branchless material selection
            // If below floor, set solid
            bool belowFloor = worldY < floorHeight;
            currentDensity = math.select(currentDensity, 1f, belowFloor);
            currentMaterial = (byte)math.select((int)currentMaterial, (int)hollow.FloorMaterialID, belowFloor); // Explicit cast for select
            
            // If above ceiling, set solid
            bool aboveCeiling = worldY > ceilingHeight;
            currentDensity = math.select(currentDensity, 1f, aboveCeiling);
            currentMaterial = (byte)math.select((int)currentMaterial, (int)hollow.WallMaterialID, aboveCeiling);
            
            // Pillars (Selective branch if disabled)
            if (hollow.GeneratePillars != 0)
            {
                float pillarDensity = GetPillarDensity(worldPos, hollow, seed);
                bool hasPillar = pillarDensity > 0;
                // Only apply if not already solid (though density > 0 overwrites air)
                // If already solid (floor/ceil), we keep it 1f. If pillar (0.5..1), we might reduce density?
                // Logic: Pillars connect floor/ceiling.
                // We want max(currentDensity, pillarDensity).
                currentDensity = math.max(currentDensity, pillarDensity);
                // If pillar contributed more, set material
                currentMaterial = (byte)math.select((int)currentMaterial, (int)hollow.WallMaterialID, hasPillar);
            }
            
            // Stalactites
            if (hollow.HasStalactites != 0)
            {
                float distFromCeiling = ceilingHeight - worldY;
                // Quick check before expensive call
                if (distFromCeiling < hollow.MaxStalactiteLength && distFromCeiling > 0)
                {
                    float stalactiteDensity = GetStalactiteDensity(worldPos, distFromCeiling, hollow, seed);
                    bool hasStal = stalactiteDensity > 0;
                    currentDensity = math.max(currentDensity, stalactiteDensity);
                    currentMaterial = (byte)math.select((int)currentMaterial, (int)hollow.WallMaterialID, hasStal);
                }
            }
            
            material = currentMaterial;
            return currentDensity;
        }
        
        [BurstCompile]
        private static float GetPillarDensity(
            in float3 worldPos,
            in CaveGenerationService.HollowParams hollow,
            uint seed)
        {
            float2 xz = new float2(worldPos.x, worldPos.z);
            
            // Grid-based pillar placement
            float2 cell = math.floor(xz * hollow.PillarFrequency);
            uint cellSeed = (uint)(cell.x * 374761 + cell.y * 668265 + seed);
            
            // Deterministic random using hashing - faster than sin?
            // Existing sin based random:
            float randX = Frac(math.sin(cellSeed * 12.9898f) * 43758.5453f);
            float randZ = Frac(math.sin(cellSeed * 78.233f) * 43758.5453f);
            float randR = Frac(math.sin(cellSeed * 39.346f) * 43758.5453f);
            
            float2 pillarCenter = (cell + new float2(randX, randZ)) / hollow.PillarFrequency;
            float distSq = math.distancesq(xz, pillarCenter);
            
            float radius = math.lerp(hollow.MinPillarRadius, hollow.MaxPillarRadius, randR);
            float radiusSq = radius * radius;
            
            // Branchless return
            // if (distSq < radiusSq) return 1f - math.sqrt(distSq) / radius; else return -1f;
            bool inside = distSq < radiusSq;
            return math.select(-1f, 1f - math.sqrt(distSq) / radius, inside);
        }
        
        [BurstCompile]
        private static float GetStalactiteDensity(
            in float3 worldPos,
            float distFromCeiling,
            in CaveGenerationService.HollowParams hollow,
            uint seed)
        {
            // Use noise to determine stalactite positions
            float stalNoise = noise.snoise(new float2(worldPos.x, worldPos.z) * 0.1f + seed);
            
            bool potentialStal = stalNoise > (1f - hollow.StalactiteDensity);
            
            // Branchless accumulation tricky due to dependent calcs. 
            // We use 'select' to carry 0 or values.
            if (!potentialStal) return -1f; // Early exit safe here as it's a sparce check? 
            // Maintaining branchless prefered:
            
            float lengthNoise = noise.snoise(new float2(worldPos.x, worldPos.z) * 0.05f + seed + 1000);
            float stalLength = (lengthNoise * 0.5f + 0.5f) * hollow.MaxStalactiteLength;
            
            if (distFromCeiling >= stalLength) return -1f;

            // Tapers toward tip
            float taper = 1f - (distFromCeiling / stalLength);
            // float radius = 1f + taper * 2f; 
            // Radius check logic from original:
            float radiusNoise = noise.snoise(new float2(worldPos.x, worldPos.z) * 0.2f + seed);
            
            bool insideRadius = radiusNoise > (1f - hollow.StalactiteDensity * 0.5f);
            
            return math.select(-1f, taper, insideRadius);
        }

        [BurstCompile]
        private static float Frac(float x)
        {
            return x - math.floor(x);
        }
    }
}
