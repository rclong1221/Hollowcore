using UnityEngine;
using Unity.Entities;

namespace Audio.Systems
{
    /// <summary>
    /// Attach this authoring component to scene geometry to mark it with a SurfaceMaterial.
    /// The Baker will write a small `SurfaceMaterialId` component to the converted entity.
    /// </summary>
    public class SurfaceMaterialAuthoring : MonoBehaviour
    {
        public SurfaceMaterial Material;
    }

#if UNITY_EDITOR
    // Baker for conversion workflow (Entities 1.0+ Baker API)
    public class SurfaceMaterialAuthoringBaker : Baker<SurfaceMaterialAuthoring>
    {
        public override void Bake(SurfaceMaterialAuthoring authoring)
        {
            var mat = authoring.Material;
            if (mat == null) return;
            var ent = GetEntity(TransformUsageFlags.None);
            AddComponent(ent, new SurfaceMaterialId { Id = mat.Id });
        }
    }
#endif
}
