using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace DIG.Core.Zones
{
    /// <summary>
    /// Marks a collider as a zone trigger with specific collision filter.
    /// The legacy collider on this GameObject is baked by Unity's built-in baker,
    /// then ZoneTriggerColliderBakingSystem patches the filter/material to use
    /// the Trigger layer and RaiseTriggerEvents response.
    /// </summary>
    public class ZoneTriggerAuthoring : MonoBehaviour
    {
        public bool IsTrigger = true;

        // Optional: Tag for specific logic systems
        public ZoneType Type = ZoneType.Generic;

        public enum ZoneType
        {
            Generic,
            Gravity,
            Tutorial,
            AbilityUnlock,
            Damage
        }

        class Baker : Baker<ZoneTriggerAuthoring>
        {
            public override void Bake(ZoneTriggerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Zone type tag — consumed by gameplay systems
                AddComponent(entity, new ZoneTriggerComponent
                {
                    Type = authoring.Type
                });

                // PhysicsCollider is NOT added here.
                // Unity's built-in collider baker (BoxBaker/SphereBaker/CapsuleBaker)
                // creates PhysicsCollider from the legacy collider on this GameObject.
                // ZoneTriggerColliderBakingSystem then patches it with trigger settings.
            }
        }
    }

    public struct ZoneTriggerComponent : IComponentData
    {
        public ZoneTriggerAuthoring.ZoneType Type;
    }

    /// <summary>
    /// Baking system that patches PhysicsCollider on zone trigger entities
    /// to use the Trigger collision layer (Bit 6).
    /// Runs after all bakers (including Unity's built-in collider bakers).
    ///
    /// Collision response (RaiseTriggerEvents) is handled by the legacy collider's
    /// isTrigger checkbox — ensure it's checked in the Inspector.
    ///
    /// Uses same SetCollisionFilter pattern as EnemyCollisionFilterSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct ZoneTriggerColliderBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var triggerFilter = new CollisionFilter
            {
                BelongsTo = (1u << 6),  // CollisionLayers.Trigger
                CollidesWith = ~0u,     // Everything
                GroupIndex = 0
            };

            foreach (var (physicsCollider, zone) in
                SystemAPI.Query<RefRW<PhysicsCollider>, RefRO<ZoneTriggerComponent>>())
            {
                if (!physicsCollider.ValueRO.IsValid) continue;

                // Patch collision filter to Trigger layer
                // (same pattern as EnemyCollisionFilterSystem)
                physicsCollider.ValueRW.Value.Value.SetCollisionFilter(triggerFilter);
            }
        }
    }
}
