using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Core
{
    public static class BlobAssetBuilder
    {
        public static BlobAssetReference<CaveParamsBlob> CreateCaveBlob(CaveProfile profile)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CaveParamsBlob>();
            
            root.EnableSwissCheese = profile.EnableSwissCheese;
            root.CheeseScale = profile.CheeseScale;
            root.CheeseThreshold = profile.CheeseThreshold;
            
            root.EnableSpaghetti = profile.EnableSpaghetti;
            root.SpaghettiScale = profile.SpaghettiScale;
            root.SpaghettiWidth = profile.SpaghettiWidth;
            
            root.EnableNoodles = profile.EnableNoodles;
            root.NoodleScale = profile.NoodleScale;
            root.NoodleWidth = profile.NoodleWidth;
            
            root.EnableCaverns = profile.EnableCaverns;
            root.CavernScale = profile.CavernScale;
            root.CavernThreshold = profile.CavernThreshold;
            
            var result = builder.CreateBlobAssetReference<CaveParamsBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
        
        public static BlobAssetReference<HollowEarthBlob> CreateHollowBlob(
            HollowEarthProfile profile, float topDepth, float bottomDepth)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<HollowEarthBlob>();
            
            root.TopDepth = topDepth;
            root.BottomDepth = bottomDepth;
            root.AverageHeight = profile.AverageHeight;
            
            root.FloorNoiseScale = profile.FloorNoiseScale;
            root.FloorAmplitude = profile.FloorAmplitude;
            root.FloorMaterialID = profile.FloorMaterialID;
            
            root.CeilingNoiseScale = profile.CeilingNoiseScale;
            root.CeilingVariation = profile.HeightVariation;
            root.HasStalactites = profile.HasStalactites;
            root.MaxStalactiteLength = profile.MaxStalactiteLength;
            
            root.GeneratePillars = profile.GeneratePillars;
            root.PillarFrequency = profile.PillarFrequency;
            root.MinPillarRadius = profile.MinPillarRadius;
            root.MaxPillarRadius = profile.MaxPillarRadius;
            
            root.WallMaterialID = profile.WallMaterialID;
            root.HasVisitableFeatures = profile.HasUndergroundLakes; // Simplify generic flag for now
            
            var result = builder.CreateBlobAssetReference<HollowEarthBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
        
        public static BlobAssetReference<StrataBlob> CreateStrataBlob(StrataProfile profile)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<StrataBlob>();
            
            root.NoiseSeed = 12345; // TODO: Get from config
            root.NoiseScale = 0.01f;
            
            var layers = builder.Allocate(ref root.Layers, profile.Layers.Length);
            
            for (int i = 0; i < profile.Layers.Length; i++)
            {
                var src = profile.Layers[i];
                layers[i] = new StrataLayerData
                {
                    MaterialID = src.MaterialID,
                    MinDepth = src.MinDepth,
                    MaxDepth = src.MaxDepth,
                    BlendWidth = src.BlendWidth,
                    NoiseInfluence = src.NoiseInfluence
                };
            }
            
            var result = builder.CreateBlobAssetReference<StrataBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
