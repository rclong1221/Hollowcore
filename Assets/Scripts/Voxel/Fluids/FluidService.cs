using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// Service for fluid management.
    /// Converts FluidDefinition ScriptableObjects to Burst-compatible data.
    /// </summary>
    public static class FluidService
    {
        // Burst-compatible fluid properties
        public struct FluidParams
        {
            public byte FluidID;
            public byte Type;               // FluidType enum
            public byte DamageType;         // FluidDamageType enum
            public byte Flags;              // Packed booleans
            
            public float Viscosity;
            public float Density;
            public float DamagePerSecond;
            public float DamageStartDepth;
            public float PressureLevel;
            
            // Flags accessors
            public bool IsPressurized => (Flags & 0x01) != 0;
            public bool IsFlammable => (Flags & 0x02) != 0;
            public bool IsToxic => (Flags & 0x04) != 0;
            public bool CoolsToSolid => (Flags & 0x08) != 0;
            
            public static byte PackFlags(bool pressurized, bool flammable, bool toxic, bool cools)
            {
                byte flags = 0;
                if (pressurized) flags |= 0x01;
                if (flammable) flags |= 0x02;
                if (toxic) flags |= 0x04;
                if (cools) flags |= 0x08;
                return flags;
            }
        }
        
        // Static data
        private static NativeArray<FluidParams> _fluidParams;
        private static bool _isInitialized;
        
        private static int _referenceCount;
        
        public static bool IsInitialized => _isInitialized;
        public static NativeArray<FluidParams> FluidParamsArray => _fluidParams;
        
        /// <summary>
        /// Initialize from FluidDefinition array.
        /// </summary>
        public static void Initialize(FluidDefinition[] definitions)
        {
            _referenceCount++;
            
            if (_isInitialized) return;
            
            if (definitions == null || definitions.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[FluidService] No fluid definitions provided");
                return;
            }
            
            // Find max ID
            int maxId = 0;
            foreach (var def in definitions)
            {
                if (def != null && def.FluidID > maxId)
                    maxId = def.FluidID;
            }
            
            // Allocate array
            _fluidParams = new NativeArray<FluidParams>(maxId + 1, Allocator.Persistent);
            
            // Convert definitions
            foreach (var def in definitions)
            {
                if (def == null) continue;
                
                _fluidParams[def.FluidID] = new FluidParams
                {
                    FluidID = def.FluidID,
                    Type = (byte)def.Type,
                    DamageType = (byte)def.DamageType,
                    Flags = FluidParams.PackFlags(def.IsPressurized, def.IsFlammable, def.IsToxic, def.CoolsToSolid),
                    Viscosity = def.Viscosity,
                    Density = def.Density,
                    DamagePerSecond = def.DamagePerSecond,
                    DamageStartDepth = def.DamageStartDepth,
                    PressureLevel = def.PressureLevel
                };
            }
            
            _isInitialized = true;
            UnityEngine.Debug.Log($"[FluidService] Initialized with {definitions.Length} fluid types");
        }
        
        /// <summary>
        /// Dispose native arrays.
        /// </summary>
        public static void Dispose()
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                if (_fluidParams.IsCreated) _fluidParams.Dispose();
                _isInitialized = false;
                _referenceCount = 0;
            }
        }
        
        /// <summary>
        /// Get fluid params by ID.
        /// </summary>
        public static FluidParams GetFluidParams(byte fluidId)
        {
            if (!_isInitialized || fluidId >= _fluidParams.Length)
                return default;
            return _fluidParams[fluidId];
        }
    }
    
    /// <summary>
    /// Burst-compatible static methods for fluid generation.
    /// Called from generation jobs.
    /// </summary>
    [BurstCompile]
    public static class FluidLookup
    {
        /// <summary>
        /// Determine if a position should contain fluid based on hollow earth profile.
        /// </summary>
        [BurstCompile]
        public static bool ShouldHaveFluid(
            in float3 worldPos,
            float floorHeight,
            float fluidElevation,
            float fluidCoverage,
            uint seed,
            out byte fluidType,
            out byte fluidLevel)
        {
            fluidType = 0;
            fluidLevel = 0;
            
            // Y must be above floor but below fluid surface
            float fluidSurfaceY = floorHeight + fluidElevation;
            if (worldPos.y < floorHeight || worldPos.y > fluidSurfaceY)
                return false;
            
            // Check coverage using noise
            float2 xz = new float2(worldPos.x, worldPos.z);
            float coverageNoise = noise.snoise(xz * 0.005f + seed);
            
            // Transform noise from [-1,1] to [0,1] and compare to coverage
            float normalizedNoise = (coverageNoise + 1f) * 0.5f;
            if (normalizedNoise > fluidCoverage)
                return false;
            
            // Calculate fill level based on distance from surface
            float distFromSurface = fluidSurfaceY - worldPos.y;
            float maxDepth = fluidElevation;
            fluidLevel = (byte)(255 * math.saturate(distFromSurface / maxDepth));
            
            // Default to water
            fluidType = (byte)FluidType.Water;
            
            return true;
        }
        
        /// <summary>
        /// Check if position is in a lava river.
        /// </summary>
        [BurstCompile]
        public static bool IsInLavaRiver(
            in float3 worldPos,
            float floorHeight,
            float riverWidth,
            uint seed,
            out byte fluidLevel)
        {
            fluidLevel = 0;
            
            // River follows a noise-based path
            float2 xz = new float2(worldPos.x, worldPos.z);
            
            // River path noise
            float riverNoise = noise.snoise(xz * 0.002f + seed + 1000);
            
            // Check if within river width
            float distFromCenter = math.abs(riverNoise) * 100f;  // Scale noise to world units
            if (distFromCenter > riverWidth)
                return false;
            
            // Must be near floor level
            if (worldPos.y < floorHeight || worldPos.y > floorHeight + 3f)
                return false;
            
            // Full fill for lava
            fluidLevel = 255;
            return true;
        }
        
        /// <summary>
        /// Calculate damage from fluid exposure.
        /// </summary>
        [BurstCompile]
        public static float CalculateFluidDamage(
            byte fluidType,
            float submersionDepth,
            float damageStartDepth,
            float damagePerSecond,
            float deltaTime)
        {
            // No damage if not deep enough
            if (submersionDepth < damageStartDepth)
                return 0f;
            
            // Damage scales with depth for water (drowning)
            if (fluidType == (byte)FluidType.Water)
            {
                float depthFactor = math.saturate((submersionDepth - damageStartDepth) / 2f);
                return damagePerSecond * depthFactor * deltaTime;
            }
            
            // Full damage for lava/toxic
            return damagePerSecond * deltaTime;
        }
    }
}
