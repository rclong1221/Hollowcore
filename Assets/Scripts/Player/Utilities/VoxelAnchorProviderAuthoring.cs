using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

namespace Player.Utilities
{
    // Simple authoring helper to create a VoxelAnchorProvider blob from inspector anchors.
    // Designers or the voxel system can place this GameObject and add child Transforms
    // representing anchor candidates; the Baker will create a BlobAssetReference and
    // attach a `VoxelAnchorProvider` component to a singleton entity.
    public class VoxelAnchorProviderAuthoring : MonoBehaviour
    {
        [Tooltip("Anchor transforms (world positions) representing preferred mount anchors.")]
        public Transform[] AnchorPoints = new Transform[0];

        [Tooltip("Optional normals for each anchor. If empty, up (0,1,0) will be used.")]
        public Vector3[] AnchorNormals = new Vector3[0];
    }

    public class VoxelAnchorProviderBaker : Baker<VoxelAnchorProviderAuthoring>
    {
        public override void Bake(VoxelAnchorProviderAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VoxelAnchorBlob>();

            int count = authoring.AnchorPoints != null ? authoring.AnchorPoints.Length : 0;
            if (count > 0)
            {
                var posArr = builder.Allocate(ref root.Positions, count);
                var normArr = builder.Allocate(ref root.Normals, count);

                for (int i = 0; i < count; ++i)
                {
                    var t = authoring.AnchorPoints[i];
                    posArr[i] = t != null ? new float3(t.position.x, t.position.y, t.position.z) : float3.zero;
                    if (authoring.AnchorNormals != null && i < authoring.AnchorNormals.Length)
                        normArr[i] = new float3(authoring.AnchorNormals[i].x, authoring.AnchorNormals[i].y, authoring.AnchorNormals[i].z);
                    else
                        normArr[i] = new float3(0f, 1f, 0f);
                }
            }
            else
            {
                var posArr = builder.Allocate(ref root.Positions, 0);
                var normArr = builder.Allocate(ref root.Normals, 0);
            }

            var blob = builder.CreateBlobAssetReference<VoxelAnchorBlob>(Allocator.Persistent);
            builder.Dispose();

            var ent = GetEntity(TransformUsageFlags.None);
            AddComponent(ent, new VoxelAnchorProvider { Blob = blob });
        }
    }
}
