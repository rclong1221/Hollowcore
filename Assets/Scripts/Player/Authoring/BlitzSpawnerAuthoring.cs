using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Player.Authoring
{
    /// <summary>
    /// Singleton component that holds reference to Blitz prefab.
    /// Place in SubScene alongside PlayerSpawnerAuthoring.
    /// </summary>
    public struct BlitzSpawner : IComponentData
    {
        public Entity BlitzPrefab;
    }

    /// <summary>
    /// Authoring component for Blitz spawner.
    /// Place in SubScene and assign Blitz_Server prefab.
    /// Blitz will spawn at the position of this GameObject when game starts.
    /// </summary>
    [AddComponentMenu("DIG/Player/Blitz Spawner")]
    [DisallowMultipleComponent]
    public class BlitzSpawnerAuthoring : MonoBehaviour
    {
        [Header("Blitz Prefab")]
        [Tooltip("Assign the Blitz_Server prefab")]
        public GameObject BlitzPrefab;
        
        [Header("Spawn Settings")]
        [Tooltip("If true, spawns Blitz at this transform's position on game start")]
        public bool SpawnOnStart = true;

        class Baker : Baker<BlitzSpawnerAuthoring>
        {
            public override void Bake(BlitzSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                if (authoring.BlitzPrefab != null)
                {
                    AddComponent(entity, new BlitzSpawner
                    {
                        BlitzPrefab = GetEntity(authoring.BlitzPrefab, TransformUsageFlags.Dynamic)
                    });
                    
                    // Mark that we want to spawn at this position
                    if (authoring.SpawnOnStart)
                    {
                        AddComponent<BlitzSpawnRequest>(entity);
                    }
                }
                else
                {
                    Debug.LogWarning($"BlitzSpawnerAuthoring on '{authoring.gameObject.name}' has no BlitzPrefab assigned.", authoring.gameObject);
                }
            }
        }
    }
    
    /// <summary>
    /// Tag component indicating Blitz should spawn here.
    /// Consumed by BlitzSpawnSystem.
    /// </summary>
    public struct BlitzSpawnRequest : IComponentData { }
}

