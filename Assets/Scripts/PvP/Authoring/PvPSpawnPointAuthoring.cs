using Unity.Entities;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Authoring component for PvP arena spawn points.
    /// Place on empty GameObjects in arena subscene to mark valid spawn locations.
    /// </summary>
    [AddComponentMenu("DIG/PvP/Spawn Point")]
    public class PvPSpawnPointAuthoring : MonoBehaviour
    {
        [Tooltip("Which team uses this spawn (0 = any team).")]
        [Range(0, 4)] public byte TeamId;

        [Tooltip("Index within team's spawn array.")]
        public byte SpawnIndex;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = TeamId switch
            {
                1 => new Color(1f, 0.2f, 0.2f, 0.5f),
                2 => new Color(0.2f, 0.2f, 1f, 0.5f),
                3 => new Color(0.2f, 1f, 0.2f, 0.5f),
                4 => new Color(1f, 1f, 0.2f, 0.5f),
                _ => new Color(1f, 1f, 1f, 0.5f)
            };
            Gizmos.DrawWireSphere(transform.position, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }

        public class Baker : Baker<PvPSpawnPointAuthoring>
        {
            public override void Bake(PvPSpawnPointAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PvPSpawnPoint
                {
                    TeamId = authoring.TeamId,
                    SpawnIndex = authoring.SpawnIndex,
                    IsActive = 1,
                    LastUsedTick = 0
                });
            }
        }
    }
}
