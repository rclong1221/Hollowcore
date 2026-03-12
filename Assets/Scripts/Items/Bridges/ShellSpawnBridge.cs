using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Items.Components;
using DIG.Items.Authoring;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// Static bridge to spawn shell entities from MonoBehaviour code.
    /// Looks up shell prefabs from the ShellPrefabRegistry.
    /// </summary>
    public static class ShellSpawnBridge
    {
        /// <summary>
        /// Request a shell spawn by shell type ID.
        /// </summary>
        /// <param name="shellTypeID">ID matching ShellPrefabRegistryAuthoring entry (e.g., "AssaultRifle")</param>
        /// <param name="position">World position to spawn at</param>
        /// <param name="rotation">Rotation of the shell</param>
        /// <param name="ejectionVelocity">Initial velocity (world space)</param>
        public static void RequestShellSpawn(string shellTypeID, Vector3 position, Quaternion rotation, Vector3 ejectionVelocity)
        {
            // Find the ClientWorld (for NetCode projects) or default world
            World world = null;
            foreach (var w in World.All)
            {
                if (w.Name == "ClientWorld")
                {
                    world = w;
                    break;
                }
            }
            
            // Fallback to DefaultGameObjectInjectionWorld
            if (world == null)
            {
                world = World.DefaultGameObjectInjectionWorld;
            }
            
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[ShellSpawnBridge] No ECS world available.");
                return;
            }
            
            var em = world.EntityManager;
            
            // Find the registry singleton
            var query = em.CreateEntityQuery(typeof(ShellPrefabRegistrySingleton));
            Debug.Log($"[ShellSpawnBridge] Looking for registry. Query empty: {query.IsEmpty}, Entities in world: {em.UniversalQuery.CalculateEntityCount()}");
            
            if (query.IsEmpty)
            {
                // Try alternative query without buffer requirement
                var altQuery = em.CreateEntityQuery(typeof(ShellPrefabElement));
                Debug.LogWarning($"[ShellSpawnBridge] No ShellPrefabRegistry found. Alt query empty: {altQuery.IsEmpty}. Add ShellPrefabRegistryAuthoring to your SubScene.");
                return;
            }
            
            var registryEntity = query.GetSingletonEntity();
            
            if (!em.HasBuffer<ShellPrefabElement>(registryEntity))
            {
                Debug.LogWarning($"[ShellSpawnBridge] Registry entity found but has no ShellPrefabElement buffer!");
                return;
            }
            
            var buffer = em.GetBuffer<ShellPrefabElement>(registryEntity);
            Debug.Log($"[ShellSpawnBridge] Found registry with {buffer.Length} shell entries.");
            
            // Find the shell by ID
            var targetID = new FixedString64Bytes(shellTypeID);
            Entity prefabEntity = Entity.Null;
            float lifetime = 5f;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ShellID.Equals(targetID))
                {
                    prefabEntity = buffer[i].PrefabEntity;
                    lifetime = buffer[i].Lifetime;
                    break;
                }
            }
            
            if (prefabEntity == Entity.Null)
            {
                Debug.LogWarning($"[ShellSpawnBridge] Shell type '{shellTypeID}' not found in registry.");
                return;
            }
            
            // Random angular velocity for tumbling
            var angularVel = UnityEngine.Random.insideUnitSphere * 15f;
            
            // Create spawn request entity
            var requestEntity = em.CreateEntity();
            em.AddComponentData(requestEntity, new ShellSpawnRequest
            {
                ShellPrefab = prefabEntity,
                Position = new float3(position.x, position.y, position.z),
                Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                EjectionVelocity = new float3(ejectionVelocity.x, ejectionVelocity.y, ejectionVelocity.z),
                AngularVelocity = new float3(angularVel.x, angularVel.y, angularVel.z),
                Lifetime = lifetime
            });
            
            Debug.Log($"[ShellSpawnBridge] Created spawn request for '{shellTypeID}'. Prefab={prefabEntity}, Pos={position}");
        }
        
        /// <summary>
        /// Check if the registry is available.
        /// </summary>
        public static bool IsRegistryAvailable
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) return false;
                var query = world.EntityManager.CreateEntityQuery(typeof(ShellPrefabRegistrySingleton));
                return !query.IsEmpty;
            }
        }
    }
}
