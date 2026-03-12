using UnityEngine;
using Unity.Entities;

namespace DIG.Combat.Resources.Authoring
{
    /// <summary>
    /// EPIC 16.8 Phase 6: Standalone authoring for resource pool configuration.
    /// Can be placed on player prefab, enemy prefabs, or boss prefabs.
    /// Baker creates ResourcePool + ResourcePoolBase components.
    /// </summary>
    public class ResourcePoolAuthoring : MonoBehaviour
    {
        [Header("Slot 0")]
        public ResourceType Slot0Type = ResourceType.Mana;
        public float Slot0Max = 100f;
        [Tooltip("Starting value (typically equal to Max)")]
        public float Slot0Start = 100f;
        public float Slot0RegenRate = 5f;
        [Tooltip("Seconds after last drain before regen starts")]
        public float Slot0RegenDelay = 2f;
        [Tooltip("Per-second decay when DecaysWhenIdle flag is set")]
        public float Slot0DecayRate = 0f;
        [Tooltip("Amount generated per trigger (hit/take) when flag is set")]
        public float Slot0GenerateAmount = 0f;
        public ResourceFlags Slot0Flags = ResourceFlags.None;

        [Header("Slot 1")]
        public ResourceType Slot1Type = ResourceType.None;
        public float Slot1Max = 0f;
        public float Slot1Start = 0f;
        public float Slot1RegenRate = 0f;
        public float Slot1RegenDelay = 0f;
        public float Slot1DecayRate = 0f;
        public float Slot1GenerateAmount = 0f;
        public ResourceFlags Slot1Flags = ResourceFlags.None;
    }

    public class ResourcePoolBaker : Baker<ResourcePoolAuthoring>
    {
        public override void Bake(ResourcePoolAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ResourcePool
            {
                Slot0 = new ResourceSlot
                {
                    Type = authoring.Slot0Type,
                    Current = authoring.Slot0Start,
                    Max = authoring.Slot0Max,
                    RegenRate = authoring.Slot0RegenRate,
                    RegenDelay = authoring.Slot0RegenDelay,
                    DecayRate = authoring.Slot0DecayRate,
                    GenerateAmount = authoring.Slot0GenerateAmount,
                    Flags = authoring.Slot0Flags
                },
                Slot1 = new ResourceSlot
                {
                    Type = authoring.Slot1Type,
                    Current = authoring.Slot1Start,
                    Max = authoring.Slot1Max,
                    RegenRate = authoring.Slot1RegenRate,
                    RegenDelay = authoring.Slot1RegenDelay,
                    DecayRate = authoring.Slot1DecayRate,
                    GenerateAmount = authoring.Slot1GenerateAmount,
                    Flags = authoring.Slot1Flags
                }
            });

            AddComponent(entity, new ResourcePoolBase
            {
                Slot0BaseMax = authoring.Slot0Max,
                Slot0BaseRegen = authoring.Slot0RegenRate,
                Slot1BaseMax = authoring.Slot1Max,
                Slot1BaseRegen = authoring.Slot1RegenRate
            });
        }
    }
}
