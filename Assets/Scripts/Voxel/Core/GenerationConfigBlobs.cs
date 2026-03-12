using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace DIG.Voxel.Core
{
    // Strata blob
    public struct StrataBlob
    {
        public BlobArray<StrataLayerData> Layers;
        public uint NoiseSeed;
        public float NoiseScale;
    }

    public struct StrataLayerData
    {
        public byte MaterialID;
        public float MinDepth;
        public float MaxDepth;
        public float BlendWidth;
        public float NoiseInfluence;
    }

    // Cave parameters blob
    public struct CaveParamsBlob
    {
        // Swiss Cheese
        public bool EnableSwissCheese;
        public float CheeseScale;
        public float CheeseThreshold;
        
        // Spaghetti
        public bool EnableSpaghetti;
        public float SpaghettiScale;
        public float SpaghettiWidth;
        
        // Noodles
        public bool EnableNoodles;
        public float NoodleScale;
        public float NoodleWidth;
        
        // Caverns
        public bool EnableCaverns;
        public float CavernScale;
        public float CavernThreshold;
    }

    // Hollow Earth blob
    public struct HollowEarthBlob
    {
        public float TopDepth;
        public float BottomDepth;
        public float AverageHeight;
        
        // Floor
        public float FloorNoiseScale;
        public float FloorAmplitude;
        public byte FloorMaterialID;
        
        // Ceiling
        public float CeilingNoiseScale;
        public float CeilingVariation;
        public bool HasStalactites;
        public float MaxStalactiteLength;
        
        // Pillars
        public bool GeneratePillars;
        public float PillarFrequency;
        public float MinPillarRadius;
        public float MaxPillarRadius;
        
        // Wall material
        public byte WallMaterialID;
        
        // Features
        public bool HasVisitableFeatures; // Lakes, etc
    }
}
