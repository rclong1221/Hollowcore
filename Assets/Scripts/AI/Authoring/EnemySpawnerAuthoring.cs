using Unity.Entities;
using UnityEngine;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// Spawns enemy prefabs at runtime with configurable count, area, and batching.
    /// Supports 1 to 1,000,000 entities with frame-budgeted instantiation.
    ///
    /// Setup:
    /// 1. Create an empty GameObject in your subscene
    /// 2. Add EnemySpawnerAuthoring
    /// 3. Assign the enemy prefab (e.g., BoxingJoe)
    /// 4. Set TotalCount, SpawnRadius, and BatchSize
    /// 5. Enter play mode — entities spawn server-side, ghost replication handles clients
    /// </summary>
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("The enemy prefab to spawn (must be a ghost prefab in a subscene)")]
        public GameObject EnemyPrefab;

        [Header("Spawn Count")]
        [Tooltip("Total number of entities to spawn")]
        [Min(1)]
        public int TotalCount = 100;

        [Header("Performance")]
        [Tooltip("Max entities to instantiate per frame. Higher = faster but larger frame spikes. " +
                 "1000 is good for most cases. Use 10000+ for stress tests.")]
        [Min(1)]
        public int BatchSize = 1000;

        [Header("Placement")]
        [Tooltip("Radius around this object to scatter spawned entities. 0 = all at this position")]
        public float SpawnRadius = 50f;

        [Tooltip("Grid spacing for organized rows/columns. 0 = random scatter within radius")]
        public float GridSpacing = 0f;

        [Tooltip("Vertical offset above spawner position (prevents spawning inside ground)")]
        public float YOffset = 0.5f;

        [Header("Timing")]
        [Tooltip("Start spawning as soon as the subscene loads")]
        public bool SpawnOnStart = true;

        [Tooltip("Random seed for deterministic placement. 0 = use entity index for unique seed")]
        public uint Seed = 0;

        public class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new EnemySpawner
                {
                    Prefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                    TotalCount = authoring.TotalCount,
                    BatchSize = authoring.BatchSize,
                    SpawnRadius = authoring.SpawnRadius,
                    GridSpacing = authoring.GridSpacing,
                    YOffset = authoring.YOffset,
                    SpawnOnStart = authoring.SpawnOnStart,
                    Seed = authoring.Seed,
                    SpawnedCount = 0,
                    IsSpawning = false,
                    IsComplete = false
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (SpawnRadius <= 0) return;

            // Spawn area circle
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            DrawCircleGizmo(transform.position, SpawnRadius);

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.15f);
            Gizmos.DrawSphere(transform.position, SpawnRadius);

            // Grid preview
            if (GridSpacing > 0)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.3f);
                int gridSize = Mathf.CeilToInt(SpawnRadius * 2f / GridSpacing);
                float halfExtent = gridSize * GridSpacing * 0.5f;
                int count = 0;

                for (int x = 0; x < gridSize && count < TotalCount && count < 500; x++)
                {
                    for (int z = 0; z < gridSize && count < TotalCount && count < 500; z++)
                    {
                        var pos = transform.position + new Vector3(
                            -halfExtent + x * GridSpacing + GridSpacing * 0.5f,
                            YOffset,
                            -halfExtent + z * GridSpacing + GridSpacing * 0.5f);

                        float dist = Vector3.Distance(
                            new Vector3(pos.x, transform.position.y, pos.z),
                            transform.position);
                        if (dist <= SpawnRadius)
                        {
                            Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);
                            count++;
                        }
                    }
                }

                // Label
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Spawner: {TotalCount} entities\n" +
                    $"Grid: {GridSpacing}m spacing\n" +
                    $"Batch: {BatchSize}/frame");
            }
            else
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Spawner: {TotalCount} entities\n" +
                    $"Random within {SpawnRadius}m\n" +
                    $"Batch: {BatchSize}/frame");
            }
        }

        private static void DrawCircleGizmo(Vector3 center, float radius, int segments = 48)
        {
            float step = 2f * Mathf.PI / segments;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
