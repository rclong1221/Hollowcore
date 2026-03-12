using UnityEngine;
using Unity.Entities;
using DIG.Ship.LocalSpace;

namespace DIG.Ship.Power
{
    /// <summary>
    /// Authoring component for power producers (reactors, generators, solar panels).
    /// </summary>
    [AddComponentMenu("DIG/Ship/Power Producer")]
    public class PowerProducerAuthoring : MonoBehaviour
    {
        [Header("Power Output")]
        [Tooltip("Maximum power output per second")]
        [Range(10f, 10000f)]
        public float MaxOutput = 100f;

        [Tooltip("Is this producer initially online?")]
        public bool StartOnline = true;

        [Header("Gizmo")]
        public Color GizmoColor = new Color(0.2f, 1f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // Draw power symbol
            Gizmos.DrawLine(transform.position + Vector3.up * 0.3f, transform.position + Vector3.down * 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.2f, transform.position + Vector3.right * 0.2f);
        }
    }

    public class PowerProducerBaker : Baker<PowerProducerAuthoring>
    {
        public override void Bake(PowerProducerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ship
            Entity shipEntity = Entity.Null;
            var shipRoot = authoring.GetComponentInParent<ShipRootAuthoring>();
            if (shipRoot != null)
            {
                shipEntity = GetEntity(shipRoot, TransformUsageFlags.Dynamic);
            }

            AddComponent(entity, new ShipPowerProducer
            {
                MaxOutput = authoring.MaxOutput,
                CurrentOutput = authoring.StartOnline ? authoring.MaxOutput : 0f,
                IsOnline = authoring.StartOnline,
                ShipEntity = shipEntity
            });
        }
    }

    /// <summary>
    /// Authoring component for power consumers (weapons, shields, subsystems).
    /// </summary>
    [AddComponentMenu("DIG/Ship/Power Consumer")]
    public class PowerConsumerAuthoring : MonoBehaviour
    {
        [Header("Power Requirements")]
        [Tooltip("Power required for full operation")]
        [Range(1f, 1000f)]
        public float RequiredPower = 10f;

        [Tooltip("Priority for power allocation (higher = more important)")]
        [Range(1, 100)]
        public int Priority = 50;

        [Header("Gizmo")]
        public Color GizmoColor = new Color(1f, 0.5f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
        }
    }

    public class PowerConsumerBaker : Baker<PowerConsumerAuthoring>
    {
        public override void Bake(PowerConsumerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ship
            Entity shipEntity = Entity.Null;
            var shipRoot = authoring.GetComponentInParent<ShipRootAuthoring>();
            if (shipRoot != null)
            {
                shipEntity = GetEntity(shipRoot, TransformUsageFlags.Dynamic);
            }

            AddComponent(entity, new ShipPowerConsumer
            {
                RequiredPower = authoring.RequiredPower,
                Priority = authoring.Priority,
                CurrentPower = 0f,
                ShipEntity = shipEntity
            });
        }
    }
}
