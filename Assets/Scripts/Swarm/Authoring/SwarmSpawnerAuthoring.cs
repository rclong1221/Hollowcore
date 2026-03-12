using Unity.Entities;
using UnityEngine;
using DIG.Swarm.Components;

namespace DIG.Swarm.Authoring
{
    /// <summary>
    /// EPIC 16.2 Phase 2: Authoring component for the swarm particle spawner.
    /// Creates SwarmSpawner component that SwarmSpawnerSystem reads to batch-create particles.
    /// </summary>
    public class SwarmSpawnerAuthoring : MonoBehaviour
    {
        [Header("Spawn Mode")]
        [Tooltip("Area: scatter around this position. Edge: spawn at flow field perimeter. Continuous: maintain population from edges.")]
        public SwarmSpawnMode Mode = SwarmSpawnMode.Continuous;

        [Header("Population")]
        [Tooltip("Total particles for Area/Edge mode (one-time batch)")]
        public int TotalParticles = 1000;
        [Tooltip("Target alive particle count for Continuous mode. System spawns to maintain this.")]
        public int TargetPopulation = 1000;
        [Tooltip("Max particles spawned per second in Continuous mode")]
        public float SpawnRate = 200f;

        [Header("Performance")]
        [Tooltip("Particles created per frame during spawning (higher = faster spawn, heavier frame)")]
        public int BatchSize = 250;

        [Header("Placement")]
        [Tooltip("Radius for Area mode scatter")]
        public float SpawnRadius = 50f;
        [Tooltip("How far inside the grid edge to spawn (meters). Prevents spawning at exact boundary.")]
        public float EdgeInset = 5f;

        [Header("Trigger")]
        [Tooltip("Start spawning immediately when subscene loads")]
        public bool SpawnOnStart = true;
        [Tooltip("Deterministic seed for reproducible placement (0 = entity index)")]
        public uint Seed = 0;

        public class Baker : Baker<SwarmSpawnerAuthoring>
        {
            public override void Bake(SwarmSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new SwarmSpawner
                {
                    Mode = authoring.Mode,
                    TotalParticles = authoring.TotalParticles,
                    BatchSize = authoring.BatchSize,
                    SpawnRadius = authoring.SpawnRadius,
                    SpawnOnStart = authoring.SpawnOnStart,
                    Seed = authoring.Seed,
                    SpawnedCount = 0,
                    IsComplete = false,
                    IsSpawning = false,
                    TargetPopulation = authoring.TargetPopulation,
                    SpawnRate = authoring.SpawnRate,
                    SpawnAccumulator = 0f,
                    EdgeInset = authoring.EdgeInset,
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Mode == SwarmSpawnMode.Area)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.15f);
                Gizmos.DrawSphere(transform.position, SpawnRadius);
                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
                Gizmos.DrawWireSphere(transform.position, SpawnRadius);
            }

            string modeLabel = Mode switch
            {
                SwarmSpawnMode.Area => $"Area | {TotalParticles:N0} particles | R:{SpawnRadius}m",
                SwarmSpawnMode.Edge => $"Edge | {TotalParticles:N0} particles | Inset:{EdgeInset}m",
                SwarmSpawnMode.Continuous => $"Continuous | Target:{TargetPopulation:N0} | Rate:{SpawnRate}/s",
                _ => "Unknown"
            };

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"Swarm Spawner\n{modeLabel}\nBatch: {BatchSize}/frame"
            );
        }
#endif
    }
}
