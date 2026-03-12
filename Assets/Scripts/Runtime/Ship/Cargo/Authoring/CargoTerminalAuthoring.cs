using UnityEngine;
using Unity.Entities;

namespace DIG.Ship.Cargo
{
    /// <summary>
    /// Authoring component for cargo terminals.
    /// Place on a console/terminal in the ship interior to allow cargo access.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Cargo Terminal Authoring")]
    public class CargoTerminalAuthoring : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Maximum distance from which player can interact")]
        [Range(0.5f, 5f)]
        public float InteractionRange = 2f;

        [Tooltip("Prompt shown when player can interact")]
        public string PromptText = "Press E: Access Cargo";

        [Header("Networking")]
        [Tooltip("Unique stable ID for cross-world entity lookup. Must be unique per ship.")]
        public int StableId = 10;

        [Header("Gizmo Settings")]
        public Color GizmoColor = new Color(0.8f, 0.6f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            // Draw interaction range
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.1f);
            Gizmos.DrawSphere(transform.position, InteractionRange);

            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, InteractionRange);
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Cargo Terminal");
#endif
        }
    }

    /// <summary>
    /// Baker for CargoTerminalAuthoring.
    /// </summary>
    public class CargoTerminalBaker : Baker<CargoTerminalAuthoring>
    {
        public override void Bake(CargoTerminalAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ShipRoot to link terminal to ship
            Entity shipEntity = Entity.Null;
            var shipAuthoring = authoring.GetComponentInParent<DIG.Ship.LocalSpace.ShipRootAuthoring>();
            if (shipAuthoring != null)
            {
                shipEntity = GetEntity(shipAuthoring, TransformUsageFlags.Dynamic);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[CargoTerminalBaker] Terminal '{authoring.name}' is not a child of a ShipRoot! It will not function correctly.");
            }

            // Add CargoTerminal component
            AddComponent(entity, new CargoTerminal
            {
                ShipEntity = shipEntity,
                Range = authoring.InteractionRange,
                StableId = authoring.StableId
            });

            // Add interactable prompt
            var promptText = new Unity.Collections.FixedString64Bytes();
            if (!string.IsNullOrEmpty(authoring.PromptText) && authoring.PromptText.Length <= 60)
            {
                promptText = authoring.PromptText;
            }

            AddComponent(entity, new CargoTerminalInteractable
            {
                PromptText = promptText
            });
        }
    }
}
