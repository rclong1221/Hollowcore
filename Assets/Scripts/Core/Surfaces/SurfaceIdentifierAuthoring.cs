using Unity.Entities;
using UnityEngine;

namespace DIG.Core.Surfaces
{
    public enum SurfaceType
    {
        Default,
        Wood,
        Metal,
        Stone,
        Grass,
        Water,
        Flesh
    }

    public struct SurfaceTag : IComponentData
    {
        public SurfaceType Type;
    }

    /// <summary>
    /// Tags a GameObject with a surface type for footstep/impact FX.
    /// Replaces Opsive SurfaceIdentifier.cs.
    /// </summary>
    public class SurfaceIdentifierAuthoring : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Default;

        class Baker : Baker<SurfaceIdentifierAuthoring>
        {
            public override void Bake(SurfaceIdentifierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic); 
                AddComponent(entity, new SurfaceTag
                {
                    Type = authoring.Type
                });
            }
        }
    }
}
