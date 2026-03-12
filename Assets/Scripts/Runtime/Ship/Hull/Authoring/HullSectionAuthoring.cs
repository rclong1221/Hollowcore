using UnityEngine;
using Unity.Entities;
using DIG.Ship.LocalSpace;
using DIG.Survival.Tools;

namespace DIG.Ship.Hull.Authoring
{
    /// <summary>
    /// Authoring component for ship hull sections.
    /// Makes the object damageable and repairable (via welder).
    /// </summary>
    [AddComponentMenu("DIG/Ship/Hull Section")]
    [RequireComponent(typeof(Collider))]
    public class HullSectionAuthoring : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Maximum hull integrity for this section")]
        [Range(10f, 5000f)]
        public float MaxHealth = 200f;

        [Tooltip("Starts with full health?")]
        public bool StartFullHealth = true;

        [Header("Gizmos")]
        public bool ShowGizmos = true;

        private void OnDrawGizmos()
        {
            if (ShowGizmos)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                var collider = GetComponent<Collider>();
                if (collider is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
        }
    }

    public class HullSectionBaker : Baker<HullSectionAuthoring>
    {
        public override void Bake(HullSectionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ship
            Entity shipEntity = Entity.Null;
            var shipRoot = authoring.GetComponentInParent<ShipRootAuthoring>();
            if (shipRoot != null)
            {
                shipEntity = GetEntity(shipRoot, TransformUsageFlags.Dynamic);
            }

            float initialHealth = authoring.StartFullHealth ? authoring.MaxHealth : 0f;

            // Add ShipHullSection (Logic Source)
            AddComponent(entity, new ShipHullSection
            {
                Current = initialHealth,
                Max = authoring.MaxHealth,
                IsBreached = !authoring.StartFullHealth, // Assume breached if starting 0
                BreachSeverity = authoring.StartFullHealth ? 0f : 1f,
                ShipEntity = shipEntity
            });

            // Add WeldRepairable (Tool Interface)
            // Systems will keep these in sync
            AddComponent(entity, new WeldRepairable
            {
                CurrentHealth = initialHealth,
                MaxHealth = authoring.MaxHealth
            });
        }
    }
}
