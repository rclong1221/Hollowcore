using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 6: Place on GameObjects with trigger colliders to create
    /// surface damage zones. Example: Lava pool, electrified grating, acid floor.
    /// </summary>
    public class SurfaceDamageZoneAuthoring : MonoBehaviour
    {
        public float DamagePerSecond = 10f;
        public DamageType DamageType = DamageType.Heat;
        public SurfaceID RequiredSurfaceId = SurfaceID.Default;

        [Range(0.1f, 2f)]
        public float TickInterval = 0.5f;

        [Range(0f, 5f)]
        public float RampUpDuration = 0f;

        public bool AffectsNPCs = true;

        public class Baker : Baker<SurfaceDamageZoneAuthoring>
        {
            public override void Bake(SurfaceDamageZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SurfaceDamageZone
                {
                    DamagePerSecond = authoring.DamagePerSecond,
                    DamageType = authoring.DamageType,
                    RequiredSurfaceId = authoring.RequiredSurfaceId,
                    TickInterval = authoring.TickInterval,
                    RampUpDuration = authoring.RampUpDuration,
                    AffectsNPCs = authoring.AffectsNPCs
                });
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
                Gizmos.DrawCube(transform.position + col.center, col.size);
        }
    }
}
