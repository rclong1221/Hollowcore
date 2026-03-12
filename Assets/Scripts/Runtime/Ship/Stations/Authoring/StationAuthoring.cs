using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// Authoring component for operable ship stations.
    /// Place on station GameObjects (helm, drill controls, weapon turrets, etc.)
    /// NOTE: Add GhostAuthoringComponent manually for networking.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Station Authoring")]
    public class StationAuthoring : MonoBehaviour
    {
        [Header("Station Type")]
        [Tooltip("Type of station - determines input mapping and behavior")]
        public StationType Type = StationType.Helm;

        [Header("Interaction")]
        [Tooltip("Transform marking where the player stands/sits when operating")]
        public Transform InteractionPoint;

        [Tooltip("Maximum distance from which player can interact")]
        [Range(0.5f, 5f)]
        public float InteractionRange = 2f;

        [Header("Prompts")]
        [Tooltip("Prompt shown when player can enter")]
        public string PromptEnter = "Press T: Operate";

        [Tooltip("Prompt shown when player can exit")]
        public string PromptExit = "Press E: Exit";

        [Tooltip("Prompt shown when station is occupied")]
        public string PromptOccupied = "Station Occupied";

        [Tooltip("Prompt shown when station is disabled")]
        public string PromptDisabled = "Station Disabled";

        [Header("Camera (Optional)")]
        [Tooltip("Camera target for this station (leave null to use player camera)")]
        public Transform CameraTarget;

        [Header("Networking")]
        [Tooltip("Unique stable ID for cross-world entity lookup. Must be unique per ship.")]
        public int StableId = 1;

        [Header("Gizmo Settings")]
        public Color GizmoColor = new Color(0.2f, 0.6f, 1f, 0.5f);

        private void OnValidate()
        {
            InteractionRange = Mathf.Clamp(InteractionRange, 0.5f, 5f);
        }

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
            // Draw interaction point
            if (InteractionPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(InteractionPoint.position, 0.2f);
                Gizmos.DrawRay(InteractionPoint.position, InteractionPoint.forward * 0.5f);

                // Line from station to interaction point
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, InteractionPoint.position);
            }

            // Draw camera target if assigned
            if (CameraTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(CameraTarget.position, 0.15f);
                Gizmos.DrawRay(CameraTarget.position, CameraTarget.forward * 0.3f);
            }

            // Draw station type label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, Type.ToString());
#endif
        }
    }

    /// <summary>
    /// Baker for StationAuthoring.
    /// </summary>
    public class StationBaker : Baker<StationAuthoring>
    {
        public override void Bake(StationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Calculate interaction point relative to the station entity
            float3 interactionPos;
            float3 interactionFwd;

            // NEW: Find Parent Ship
            Entity shipEntity = Entity.Null;
            var shipAuthoring = authoring.GetComponentInParent<DIG.Ship.LocalSpace.ShipRootAuthoring>();
            if (shipAuthoring != null)
            {
                shipEntity = GetEntity(shipAuthoring, TransformUsageFlags.Dynamic);
            }
            else
            {
                // Fallback: try to find any ShipRoot in parents (if authoring missing but component exists?)
                // Usually Authoring is the way.
            }

            if (authoring.InteractionPoint != null)
            {
                // Store InteractionPoint as LOCAL position relative to the station transform
                // This allows correct snapping when the ship has moved from its baking position
                interactionPos = authoring.transform.InverseTransformPoint(authoring.InteractionPoint.position);
                interactionFwd = authoring.transform.InverseTransformDirection(authoring.InteractionPoint.forward);

                // Get camera target entity
                Entity cameraTargetEntity = Entity.Null;
                if (authoring.CameraTarget != null)
                {
                    cameraTargetEntity = GetEntity(authoring.CameraTarget, TransformUsageFlags.Dynamic);
                }

                AddComponent(entity, new OperableStation
                {
                    Type = authoring.Type,
                    InteractionPoint = interactionPos,
                    InteractionForward = interactionFwd,
                    Range = authoring.InteractionRange,
                    CurrentOperator = Entity.Null,
                    CameraTarget = cameraTargetEntity,
                    StableId = authoring.StableId,
                    ShipEntity = shipEntity
                });
            }
            else
            {
                 // Handle missing interaction point
                 UnityEngine.Debug.LogWarning($"Station {authoring.name} missing Interaction Point ref!");

                 // Add a default OperableStation component with zero local offset
                 AddComponent(entity, new OperableStation
                 {
                     Type = authoring.Type,
                     InteractionPoint = float3.zero, // Zero offset means station center
                     InteractionForward = new float3(0, 0, 1), // Default forward
                     Range = authoring.InteractionRange,
                     CurrentOperator = Entity.Null,
                     CameraTarget = Entity.Null,
                     StableId = authoring.StableId,
                     ShipEntity = shipEntity
                 });
            }

            // Add interactable prompts
            var promptEnter = new Unity.Collections.FixedString64Bytes();
            var promptExit = new Unity.Collections.FixedString64Bytes();
            var promptOccupied = new Unity.Collections.FixedString64Bytes();
            var promptDisabled = new Unity.Collections.FixedString64Bytes();

            if (!string.IsNullOrEmpty(authoring.PromptEnter))
                promptEnter = authoring.PromptEnter;
            if (!string.IsNullOrEmpty(authoring.PromptExit))
                promptExit = authoring.PromptExit;
            if (!string.IsNullOrEmpty(authoring.PromptOccupied))
                promptOccupied = authoring.PromptOccupied;
            if (!string.IsNullOrEmpty(authoring.PromptDisabled))
                promptDisabled = authoring.PromptDisabled;

            AddComponent(entity, new StationInteractable
            {
                PromptEnter = promptEnter,
                PromptExit = promptExit,
                PromptOccupied = promptOccupied,
                PromptDisabled = promptDisabled
            });

            // Add StationInput for when the station is operated
            AddComponent<StationInput>(entity);
        }
    }

    /// <summary>
    /// Authoring component for player station support.
    /// Add to player prefab to enable station interactions.
    /// </summary>
    public class StationPlayerAuthoring : MonoBehaviour
    {
        [Header("Debounce Settings")]
        [Tooltip("Minimum ticks between interaction requests")]
        public uint DebounceTickCount = 10;
    }

    /// <summary>
    /// Baker for StationPlayerAuthoring.
    /// </summary>
    public class StationPlayerBaker : Baker<StationPlayerAuthoring>
    {
        public override void Bake(StationPlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add request buffer
            AddBuffer<StationUseRequest>(entity);

            // Add prompt state for UI
            AddComponent(entity, new StationPromptState());

            // Add debounce state
            AddComponent(entity, new StationInteractDebounce
            {
                LastRequestTick = 0,
                DebounceTickCount = authoring.DebounceTickCount
            });
        }
    }

    /// <summary>
    /// Authoring for station camera targets.
    /// Optional - place on child object of station for custom camera view.
    /// </summary>
    public class StationCameraTargetAuthoring : MonoBehaviour
    {
        [Header("Camera Settings")]
        [Tooltip("Position offset from this transform")]
        public Vector3 PositionOffset = Vector3.zero;

        [Tooltip("Look-at offset")]
        public Vector3 LookAtOffset = new Vector3(0, 0, 10f);

        [Tooltip("Field of view")]
        [Range(30f, 120f)]
        public float FOV = 60f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + PositionOffset, 0.1f);
            Gizmos.DrawLine(transform.position + PositionOffset, 
                            transform.position + PositionOffset + LookAtOffset.normalized * 0.5f);
        }
    }

    /// <summary>
    /// Baker for StationCameraTargetAuthoring.
    /// </summary>
    public class StationCameraTargetBaker : Baker<StationCameraTargetAuthoring>
    {
        public override void Bake(StationCameraTargetAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new StationCameraTarget
            {
                PositionOffset = authoring.PositionOffset,
                LookAtOffset = authoring.LookAtOffset,
                FOV = authoring.FOV
            });
        }
    }
}
