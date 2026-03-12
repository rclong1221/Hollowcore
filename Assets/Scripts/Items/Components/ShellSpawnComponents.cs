using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Items.Components
{
    /// <summary>
    /// Request to spawn a shell casing at runtime.
    /// Processed by ShellSpawnSystem.
    /// </summary>
    public struct ShellSpawnRequest : IComponentData
    {
        public Entity ShellPrefab;       // The specific shell prefab entity for this weapon
        public float3 Position;
        public quaternion Rotation;
        public float3 EjectionVelocity;
        public float3 AngularVelocity;
        public float Lifetime;
    }
    
    /// <summary>
    /// Component on weapon entities that stores their shell prefab reference.
    /// Baked from ItemVFXAuthoring's shell configuration.
    /// </summary>
    public struct WeaponShellConfig : IComponentData
    {
        public Entity ShellPrefabEntity;
        public float ShellLifetime;
    }
    
    /// <summary>
    /// Component to track shell lifetime for auto-destruction.
    /// </summary>
    public struct ShellLifetime : IComponentData
    {
        public float RemainingTime;
    }
}
