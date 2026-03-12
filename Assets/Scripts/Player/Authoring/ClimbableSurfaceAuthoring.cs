using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// EPIC 13.20.1: Authoring component to mark GameObjects as climbable surfaces.
    /// 
    /// Add this to any object you want players to be able to climb.
    /// Works with both static geometry and voxel terrain chunks.
    /// </summary>
    public class ClimbableSurfaceAuthoring : MonoBehaviour
    {
        [Header("Climbing Behavior")]
        [Tooltip("Can player hang without footholds on this surface?")]
        public bool AllowFreeHang = true;

        [Tooltip("Can player gap-jump to nearby climbable surfaces?")]
        public bool AllowGapCrossing = true;

        [Tooltip("Stamina drain multiplier (1.0 = normal, 2.0 = harder)")]
        [Range(0.1f, 3f)]
        public float GripStrength = 1.0f;

        [Tooltip("Surface type for audio/VFX (0 = default)")]
        public int SurfaceType = 0;

        public class Baker : Baker<ClimbableSurfaceAuthoring>
        {
            public override void Bake(ClimbableSurfaceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ClimbableSurface
                {
                    AllowFreeHang = authoring.AllowFreeHang,
                    AllowGapCrossing = authoring.AllowGapCrossing,
                    GripStrength = authoring.GripStrength,
                    SurfaceType = authoring.SurfaceType
                });
            }
        }
    }
}
