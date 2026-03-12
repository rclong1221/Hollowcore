using UnityEngine;
using Unity.Entities;
using DIG.Ship.LocalSpace;
using DIG.Survival.Environment;
using DIG.Survival.Authoring;

namespace DIG.Ship.Power
{
    /// <summary>
    /// Authoring component for ship life support systems.
    /// Controls whether ship interior is pressurized and safe.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Life Support")]
    public class LifeSupportAuthoring : MonoBehaviour
    {
        [Header("Power Settings")]
        [Tooltip("Power required for life support to function")]
        [Range(5f, 500f)]
        public float PowerRequired = 50f;

        [Header("Oxygen Generation")]
        [Tooltip("Oxygen generation rate when online")]
        [Range(0f, 10f)]
        public float OxygenGenerationRate = 1f;

        [Header("Initial State")]
        [Tooltip("Is life support initially online?")]
        public bool StartOnline = true;

        [Header("Interior Zone")]
        [Tooltip("Reference to the interior environment zone (optional, will auto-detect if not set)")]
        public GameObject InteriorZone;

        [Header("Gizmo")]
        public Color OnlineColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        public Color OfflineColor = new Color(1f, 0.2f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            Gizmos.color = StartOnline ? OnlineColor : OfflineColor;
            
            // Draw life support symbol (circle with wave)
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            
            // Draw "breathing" lines
            var up = transform.up;
            var right = transform.right;
            Gizmos.DrawLine(transform.position + right * 0.2f + up * 0.1f, 
                           transform.position + right * 0.1f - up * 0.1f);
            Gizmos.DrawLine(transform.position + right * 0.1f - up * 0.1f, 
                           transform.position - right * 0.1f + up * 0.1f);
            Gizmos.DrawLine(transform.position - right * 0.1f + up * 0.1f, 
                           transform.position - right * 0.2f - up * 0.1f);
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, 
                $"Life Support\nPower: {PowerRequired}W\n{(StartOnline ? "ONLINE" : "OFFLINE")}");
#endif
        }
    }

    public class LifeSupportBaker : Baker<LifeSupportAuthoring>
    {
        public override void Bake(LifeSupportAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ship
            Entity shipEntity = Entity.Null;
            var shipRoot = authoring.GetComponentInParent<ShipRootAuthoring>();
            if (shipRoot != null)
            {
                shipEntity = GetEntity(shipRoot, TransformUsageFlags.Dynamic);
            }

            // Find or create interior zone reference
            Entity interiorZoneEntity = Entity.Null;
            if (authoring.InteriorZone != null)
            {
                // Use explicit reference
                var zoneAuthoring = authoring.InteriorZone.GetComponent<EnvironmentZoneAuthoring>();
                if (zoneAuthoring != null)
                {
                    interiorZoneEntity = GetEntity(zoneAuthoring, TransformUsageFlags.Dynamic);
                }
            }
            else if (shipRoot != null)
            {
                // Auto-detect: find first EnvironmentZoneAuthoring child of ship with Pressurized type
                var zones = shipRoot.GetComponentsInChildren<EnvironmentZoneAuthoring>();
                foreach (var zone in zones)
                {
                    if (zone.ZoneType == EnvironmentZoneType.Pressurized)
                    {
                        interiorZoneEntity = GetEntity(zone, TransformUsageFlags.Dynamic);
                        break;
                    }
                }
            }

            // Add LifeSupport component
            AddComponent(entity, new LifeSupport
            {
                IsOnline = authoring.StartOnline,
                PowerRequired = authoring.PowerRequired,
                OxygenGenerationRate = authoring.OxygenGenerationRate,
                InteriorZoneEntity = interiorZoneEntity,
                ShipEntity = shipEntity,
                IsDamaged = false
            });

            // Add PowerConsumer component for power integration
            AddComponent(entity, new ShipPowerConsumer
            {
                RequiredPower = authoring.PowerRequired,
                Priority = PowerPriority.LifeSupport,
                CurrentPower = authoring.StartOnline ? authoring.PowerRequired : 0f,
                ShipEntity = shipEntity
            });
        }
    }
}
