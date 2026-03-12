using Unity.Entities;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Authoring component for PvP capture point zones.
    /// Place on trigger collider GameObjects in arena subscene.
    /// </summary>
    [AddComponentMenu("DIG/PvP/Capture Zone")]
    public class PvPCaptureZoneAuthoring : MonoBehaviour
    {
        [Tooltip("Unique zone identifier.")]
        public byte ZoneId;

        [Tooltip("Score rate when controlled (points per second).")]
        [Min(0.1f)] public float PointsPerSecond = 1f;

        [Tooltip("Capture zone radius.")]
        [Min(1f)] public float Radius = 10f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, Radius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }

        public class Baker : Baker<PvPCaptureZoneAuthoring>
        {
            public override void Bake(PvPCaptureZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PvPCaptureZone
                {
                    ZoneId = authoring.ZoneId,
                    ControllingTeam = 0,
                    ContestingTeam = 0,
                    PlayersInZone = 0,
                    CaptureProgress = 0f,
                    PointsPerSecond = authoring.PointsPerSecond,
                    Radius = authoring.Radius
                });
            }
        }
    }
}
