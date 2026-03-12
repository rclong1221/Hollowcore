using Unity.Entities;
using UnityEngine;

namespace DIG.Core.Input.Pathfinding
{
    /// <summary>
    /// ECS component tagging an NPC entity for A*-driven pathfinding.
    /// The NPCPathfindingBridge MonoBehaviour queries entities with this tag
    /// and manages their A* path requests.
    ///
    /// EPIC 15.20 Phase 4c
    /// </summary>
    public struct NPCPathfindingTag : IComponentData
    {
        /// <summary>How often the NPC recalculates its path (seconds).</summary>
        public float RepathInterval;

        /// <summary>Distance at which NPC considers itself at destination.</summary>
        public float StoppingDistance;

        /// <summary>Maximum movement speed for this NPC.</summary>
        public float MaxSpeed;
    }

    /// <summary>
    /// Authoring component for NPC pathfinding.
    /// Attach to NPC prefabs to enable A*-driven movement.
    ///
    /// EPIC 15.20 Phase 4c
    /// </summary>
    public class NPCPathfindingAuthoring : MonoBehaviour
    {
        [Tooltip("How often the NPC recalculates its path (seconds).")]
        public float RepathInterval = 1.0f;

        [Tooltip("Distance at which NPC considers itself at destination.")]
        public float StoppingDistance = 0.5f;

        [Tooltip("Maximum movement speed for this NPC.")]
        public float MaxSpeed = 5f;

        class Baker : Baker<NPCPathfindingAuthoring>
        {
            public override void Bake(NPCPathfindingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new NPCPathfindingTag
                {
                    RepathInterval = authoring.RepathInterval,
                    StoppingDistance = authoring.StoppingDistance,
                    MaxSpeed = authoring.MaxSpeed,
                });
            }
        }
    }
}
