using Unity.Entities;
using UnityEngine;

public struct PlayerSpawner : IComponentData
{
    public Entity Player;
}

[DisallowMultipleComponent]
public class PlayerSpawnerAuthoring : MonoBehaviour
{
    public GameObject Player;

    class Baker : Baker<PlayerSpawnerAuthoring>
    {
        public override void Bake(PlayerSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            PlayerSpawner component = default(PlayerSpawner);
            if (authoring.Player != null)
            {
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);
            }
            else
            {
                component.Player = Entity.Null;
                UnityEngine.Debug.LogWarning($"PlayerSpawnerAuthoring on '{authoring.gameObject.name}' has no Player assigned. Setting Player to Entity.Null.", authoring.gameObject);
            }

            AddComponent(entity, component);
        }
    }
}
