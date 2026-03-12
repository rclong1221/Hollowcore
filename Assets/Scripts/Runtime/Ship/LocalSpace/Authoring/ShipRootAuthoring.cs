using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// Authoring component for ship root entity.
    /// Place on the main ship GameObject.
    /// NOTE: Add GhostAuthoringComponent manually for networking.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Ship Root Authoring")]
    public class ShipRootAuthoring : MonoBehaviour
    {
        [Header("Ship Identity")]
        [Tooltip("Unique ID for this ship (for debugging/ownership)")]
        public int ShipId = 1;

        [Tooltip("Display name for this ship")]
        public string ShipName = "Ship";

        [Header("Movement Limits")]
        [Tooltip("Maximum linear speed (m/s)")]
        public float MaxLinearSpeed = 50f;

        [Tooltip("Maximum angular speed (rad/s)")]
        public float MaxAngularSpeed = 2f;

        [Header("Debug")]
        public Color GizmoColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            // Draw ship bounds
            Gizmos.color = GizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 10f);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 5f);

            // Draw up direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up * 3f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Ship: {ShipName} (ID: {ShipId})");
#endif
        }
    }

    /// <summary>
    /// Baker for ShipRootAuthoring.
    /// </summary>
    public class ShipRootBaker : Baker<ShipRootAuthoring>
    {
        public override void Bake(ShipRootAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Ship root identity
            var shipName = new Unity.Collections.FixedString32Bytes();
            if (!string.IsNullOrEmpty(authoring.ShipName) && authoring.ShipName.Length <= 30)
            {
                shipName = authoring.ShipName;
            }

            AddComponent(entity, new ShipRoot
            {
                ShipId = authoring.ShipId,
                ShipName = shipName
            });

            // Kinematics (velocity state)
            AddComponent(entity, new ShipKinematics
            {
                LinearVelocity = float3.zero,
                AngularVelocity = float3.zero,
                IsMoving = false,
                MaxLinearSpeed = authoring.MaxLinearSpeed,
                MaxAngularSpeed = authoring.MaxAngularSpeed
            });

            // Previous transform tracking
            AddComponent(entity, new ShipPreviousTransform
            {
                PreviousPosition = authoring.transform.position,
                PreviousRotation = authoring.transform.rotation,
                IsFirstFrame = true
            });

            // Occupant buffer
            AddBuffer<ShipOccupant>(entity);
        }
    }
}
