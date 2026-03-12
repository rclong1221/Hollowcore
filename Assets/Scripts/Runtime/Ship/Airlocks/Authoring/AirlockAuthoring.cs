using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Authoring component for airlock entities.
    /// Place on GameObject with collider to create an airlock.
    /// NOTE: Add GhostAuthoringComponent manually for networking.
    /// </summary>
    public class AirlockAuthoring : MonoBehaviour
    {
        [Header("Spawn Points")]
        [Tooltip("Transform marking the spawn point inside the ship (pressurized)")]
        public Transform InteriorSpawnPoint;
        
        [Tooltip("Transform marking the spawn point outside the ship (vacuum)")]
        public Transform ExteriorSpawnPoint;

        [Header("Cycle Settings")]
        [Tooltip("Time in seconds for a complete airlock cycle")]
        [Range(0.5f, 10f)]
        public float CycleTime = 3f;

        [Header("Interaction")]
        [Tooltip("Maximum distance from which player can interact with airlock")]
        [Range(1f, 5f)]
        public float InteractionRange = 2.5f;

        [Tooltip("Prompt shown when player can enter ship")]
        public string PromptEnter = "Press E: Enter Ship";

        [Tooltip("Prompt shown when player can exit ship")]
        public string PromptExit = "Press E: Exit Ship";

        [Tooltip("Prompt shown when airlock is busy")]
        public string PromptBusy = "Airlock Busy";

        [Tooltip("Prompt shown when airlock is locked")]
        public string PromptLocked = "Airlock Locked";

        [Header("Networking")]
        [Tooltip("Unique stable ID for cross-world entity lookup. Must be unique per ship.")]
        public int StableId = 1;

        [Header("Doors (Optional)")]
        [Tooltip("Interior door GameObject (optional)")]
        public GameObject InteriorDoor;

        [Tooltip("Exterior door GameObject (optional)")]
        public GameObject ExteriorDoor;

        private void OnValidate()
        {
            // Clamp values
            CycleTime = Mathf.Max(0.5f, CycleTime);
            InteractionRange = Mathf.Max(0.5f, InteractionRange);
        }

        private void OnDrawGizmos()
        {
            // Draw interaction range
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, InteractionRange);

            // Draw spawn points
            if (InteriorSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(InteriorSpawnPoint.position, 0.3f);
                Gizmos.DrawRay(InteriorSpawnPoint.position, InteriorSpawnPoint.forward);
            }

            if (ExteriorSpawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(ExteriorSpawnPoint.position, 0.3f);
                Gizmos.DrawRay(ExteriorSpawnPoint.position, ExteriorSpawnPoint.forward);
            }

            // Draw connection between spawn points
            if (InteriorSpawnPoint != null && ExteriorSpawnPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(InteriorSpawnPoint.position, ExteriorSpawnPoint.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // More visible when selected
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.2f);

            if (InteriorSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(InteriorSpawnPoint.position, 0.2f);
                Gizmos.DrawRay(InteriorSpawnPoint.position, InteriorSpawnPoint.forward * 0.5f);
            }

            if (ExteriorSpawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(ExteriorSpawnPoint.position, 0.2f);
                Gizmos.DrawRay(ExteriorSpawnPoint.position, ExteriorSpawnPoint.forward * 0.5f);
            }
        }
    }

    /// <summary>
    /// Baker for AirlockAuthoring.
    /// </summary>
    public class AirlockBaker : Baker<AirlockAuthoring>
    {
        public override void Bake(AirlockAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Find parent ShipRoot to link airlock to ship
            var shipAuthoring = GetComponentInParent<DIG.Ship.LocalSpace.ShipRootAuthoring>(authoring.gameObject);
            Entity shipEntity = Entity.Null;
            if (shipAuthoring != null)
            {
                shipEntity = GetEntity(shipAuthoring, TransformUsageFlags.Dynamic);
            }
            else
            {
                // Warn but don't fail hard, in case of standalone testing
                UnityEngine.Debug.LogWarning($"[AirlockBaker] Airlock '{authoring.name}' is not a child of a ShipRoot! It will not function correctly in Local Space.");
            }

            // Calculate spawn positions (LOCAL to the airlock) and forwards
            // We use local positions so they remain correct when the ship moves
            float3 interiorSpawn = authoring.InteriorSpawnPoint != null
                ? (float3)authoring.transform.InverseTransformPoint(authoring.InteriorSpawnPoint.position)
                : new float3(0, 0, 2);

            float3 exteriorSpawn = authoring.ExteriorSpawnPoint != null
                ? (float3)authoring.transform.InverseTransformPoint(authoring.ExteriorSpawnPoint.position)
                : new float3(0, 0, -2);

            float3 interiorForward = authoring.InteriorSpawnPoint != null
                ? (float3)authoring.transform.InverseTransformDirection(authoring.InteriorSpawnPoint.forward)
                : new float3(0, 0, 1);

            float3 exteriorForward = authoring.ExteriorSpawnPoint != null
                ? (float3)authoring.transform.InverseTransformDirection(authoring.ExteriorSpawnPoint.forward)
                : new float3(0, 0, -1);

            // Add main Airlock component
            AddComponent(entity, new Airlock
            {
                ShipEntity = shipEntity,
                InteriorSpawn = interiorSpawn,
                ExteriorSpawn = exteriorSpawn,
                InteriorForward = interiorForward,
                ExteriorForward = exteriorForward,
                State = AirlockState.Idle,
                CycleTime = authoring.CycleTime,
                CycleProgress = 0f,
                CurrentUser = Entity.Null,
                StableId = authoring.StableId
            });

            // Add interactable component
            var promptEnter = new Unity.Collections.FixedString64Bytes();
            var promptExit = new Unity.Collections.FixedString64Bytes();
            var promptBusy = new Unity.Collections.FixedString64Bytes();
            var promptLocked = new Unity.Collections.FixedString64Bytes();

            if (!string.IsNullOrEmpty(authoring.PromptEnter))
                promptEnter = authoring.PromptEnter;
            if (!string.IsNullOrEmpty(authoring.PromptExit))
                promptExit = authoring.PromptExit;
            if (!string.IsNullOrEmpty(authoring.PromptBusy))
                promptBusy = authoring.PromptBusy;
            if (!string.IsNullOrEmpty(authoring.PromptLocked))
                promptLocked = authoring.PromptLocked;

            AddComponent(entity, new AirlockInteractable
            {
                Range = authoring.InteractionRange,
                PromptEnter = promptEnter,
                PromptExit = promptExit,
                PromptBusy = promptBusy,
                PromptLocked = promptLocked
            });

            // Add audio state for sound system
            AddComponent(entity, AirlockAudioState.Default);

            // Note: Door components are added by AirlockDoorBaker
            // But we must ensure they are part of the LinkedEntityGroup for networking
            // unconditionally, just in case the user forgets to Rescan the Ghost.
            var linkedEntityGroup = AddBuffer<LinkedEntityGroup>(entity);
            
            // Ensure root is first (Unity requirement)
            if (linkedEntityGroup.Length == 0)
            {
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = entity });
            }

            var childDoors = authoring.GetComponentsInChildren<AirlockDoorAuthoring>();
            foreach (var door in childDoors)
            {
                var doorEntity = GetEntity(door.gameObject, TransformUsageFlags.Dynamic);
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = doorEntity });
            }
        }
    }

    /// <summary>
    /// Authoring component for player airlock support.
    /// Add to player prefab to enable airlock interactions.
    /// </summary>
    public class AirlockPlayerAuthoring : MonoBehaviour
    {
        [Header("Debounce Settings")]
        [Tooltip("Minimum ticks between interaction requests")]
        public uint DebounceTickCount = 10;
    }

    /// <summary>
    /// Baker for AirlockPlayerAuthoring.
    /// </summary>
    public class AirlockPlayerBaker : Baker<AirlockPlayerAuthoring>
    {
        public override void Bake(AirlockPlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add request buffer
            AddBuffer<AirlockUseRequest>(entity);

            // Add prompt state for UI
            AddComponent(entity, new AirlockPromptState());

            // Add debounce state
            AddComponent(entity, new AirlockInteractDebounce
            {
                LastRequestTick = 0,
                DebounceTickCount = authoring.DebounceTickCount
            });
        }

    }
}


