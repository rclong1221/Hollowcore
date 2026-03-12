using Unity.Entities;
using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Registry for shell prefabs. Place in a SubScene.
    /// Bakes shell prefab references into ECS entities.
    /// ItemVFXAuthoring looks up shells by ID at runtime.
    /// </summary>
    public class ShellPrefabRegistryAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct ShellEntry
        {
            [Tooltip("Unique ID for this shell type (e.g., 'AssaultRifle', 'Pistol', 'Shotgun')")]
            public string ShellID;
            
            [Tooltip("Shell prefab with ShellPhysicsAuthoring component")]
            public GameObject ShellPrefab;
            
            [Tooltip("Lifetime in seconds before auto-destroy")]
            public float Lifetime;
        }
        
        public ShellEntry[] Shells;
        
        public class Baker : Baker<ShellPrefabRegistryAuthoring>
        {
            public override void Bake(ShellPrefabRegistryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Add buffer for shell entries
                var buffer = AddBuffer<ShellPrefabElement>(entity);
                
                if (authoring.Shells != null)
                {
                    foreach (var shell in authoring.Shells)
                    {
                        if (shell.ShellPrefab == null) continue;
                        
                        var prefabEntity = GetEntity(shell.ShellPrefab, TransformUsageFlags.Dynamic);
                        
                        // Create fixed string from shell ID
                        var fixedId = new Unity.Collections.FixedString64Bytes(shell.ShellID ?? "");
                        
                        buffer.Add(new ShellPrefabElement
                        {
                            ShellID = fixedId,
                            PrefabEntity = prefabEntity,
                            Lifetime = shell.Lifetime > 0 ? shell.Lifetime : 5f
                        });
                    }
                }
                
                // Mark as singleton for easy lookup
                AddComponent<ShellPrefabRegistrySingleton>(entity);
            }
        }
    }
    
    /// <summary>
    /// Tag to identify the registry singleton.
    /// </summary>
    public struct ShellPrefabRegistrySingleton : IComponentData { }
    
    /// <summary>
    /// Buffer element for shell prefab entries.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ShellPrefabElement : IBufferElementData
    {
        public Unity.Collections.FixedString64Bytes ShellID;
        public Entity PrefabEntity;
        public float Lifetime;
    }
}
