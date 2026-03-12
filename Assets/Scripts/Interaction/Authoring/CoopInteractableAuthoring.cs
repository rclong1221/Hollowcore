using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 7: Authoring component for cooperative interactions.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring on the interactable entity
    /// 2. Add CoopInteractableAuthoring, configure RequiredPlayers, Mode, and slot positions
    /// 3. Each slot has a local-space position and rotation relative to this transform
    /// 4. Players joining are assigned to the first available slot
    ///
    /// Modes:
    /// - Simultaneous: All players must press Use within SyncTolerance seconds (dual key turn)
    /// - Sequential: Players confirm in slot order (relay race)
    /// - Parallel: All players channel simultaneously until ChannelDuration elapses (team revive)
    /// - Asymmetric: Like Parallel but different roles per slot (hack + defend)
    /// </summary>
    public class CoopInteractableAuthoring : MonoBehaviour
    {
        [Header("Cooperation Settings")]
        [Tooltip("How many players are needed to complete this interaction")]
        [Min(2)]
        public int RequiredPlayers = 2;

        [Tooltip("How cooperation works for this interaction")]
        public CoopMode Mode = CoopMode.Simultaneous;

        [Header("Mode-Specific Settings")]
        [Tooltip("Max seconds between players' Use inputs (Simultaneous mode only)")]
        public float SyncTolerance = 2f;

        [Tooltip("How long all players must channel together in seconds (Parallel/Asymmetric mode, 0 = instant)")]
        public float ChannelDuration = 5f;

        [Header("Slot Configuration")]
        [Tooltip("Positions and rotations for each player slot (local space)")]
        public SlotEntry[] Slots = new SlotEntry[]
        {
            new SlotEntry { Position = new Vector3(-1f, 0, 0), Rotation = Quaternion.identity },
            new SlotEntry { Position = new Vector3(1f, 0, 0), Rotation = Quaternion.identity }
        };

        [Serializable]
        public class SlotEntry
        {
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.identity;
        }

        public class Baker : Baker<CoopInteractableAuthoring>
        {
            public override void Bake(CoopInteractableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new CoopInteraction
                {
                    RequiredPlayers = authoring.RequiredPlayers,
                    CurrentPlayers = 0,
                    Mode = authoring.Mode,
                    SyncTolerance = authoring.SyncTolerance,
                    ChannelDuration = authoring.ChannelDuration,
                    ChannelProgress = 0,
                    AllPlayersReady = false,
                    CoopComplete = false,
                    CoopFailed = false,
                    CurrentSequenceSlot = 0
                });

                var slotBuffer = AddBuffer<CoopSlot>(entity);
                int slotCount = math.max(authoring.RequiredPlayers, authoring.Slots.Length);

                for (int i = 0; i < slotCount; i++)
                {
                    var entry = i < authoring.Slots.Length
                        ? authoring.Slots[i]
                        : new SlotEntry
                        {
                            Position = new Vector3(i * 1.5f, 0, 0),
                            Rotation = Quaternion.identity
                        };

                    slotBuffer.Add(new CoopSlot
                    {
                        PlayerEntity = Entity.Null,
                        SlotIndex = i,
                        SlotPosition = (float3)entry.Position,
                        SlotRotation = (quaternion)entry.Rotation,
                        IsOccupied = false,
                        IsReady = false,
                        ReadyTimestamp = 0
                    });
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Slots == null || Slots.Length == 0)
                return;

            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                Vector3 worldPos = transform.TransformPoint(slot.Position);
                Quaternion worldRot = transform.rotation * slot.Rotation;

                // Slot circle
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
                DrawCircleGizmo(worldPos, 0.5f);

                // Facing direction arrow
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
                Vector3 forward = worldRot * Vector3.forward;
                Gizmos.DrawRay(worldPos, forward * 0.8f);

                // Slot number
#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.6f, $"Slot {i}");
#endif

                // Connection line to center
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, worldPos);
            }

            // Center marker
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        private static void DrawCircleGizmo(Vector3 center, float radius, int segments = 24)
        {
            float step = 2f * Mathf.PI / segments;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0,
                    Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
