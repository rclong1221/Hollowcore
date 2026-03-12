using Unity.Entities;
using UnityEngine;

namespace DIG.Core.Spawning
{
    public struct SpawnPoint : IComponentData
    {
        public int GroupID; // For grouping spawns (e.g. Team 1, Team 2)
        public bool IsUsed;
    }

    /// <summary>
    /// Marks a GameObject as a valid Spawn Point for players/AI.
    /// Replaces Opsive SpawnPoint.cs.
    /// </summary>
    public class SpawnPointAuthoring : MonoBehaviour
    {
        public int GroupID = 0;
        public Color DebugColor = Color.green;

        class Baker : Baker<SpawnPointAuthoring>
        {
            public override void Bake(SpawnPointAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic); // Dynamic so it has transform
                AddComponent(entity, new SpawnPoint
                {
                    GroupID = authoring.GroupID,
                    IsUsed = false
                });
            }
        }

        private void OnDrawGizmos()
        {
             Gizmos.color = DebugColor;
             Gizmos.DrawWireSphere(transform.position, 0.5f);
             Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.0f);
        }
    }
}
