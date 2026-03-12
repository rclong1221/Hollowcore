using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 4: Authoring component for mountable interactables.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring (Type = Instant, Verb = Mount)
    /// 2. Add MountPointAuthoring (configure type, seat offset, dismount offset, etc.)
    /// 3. For turrets: enable TransferInputToMount, add game-specific turret system
    /// 4. For ladders: set min/max Y bounds and climb speed
    /// 5. For ziplines: set speed and max distance
    /// </summary>
    public class MountPointAuthoring : MonoBehaviour
    {
        [Header("Mount Configuration")]
        [Tooltip("What kind of mount this is")]
        public MountType Type = MountType.Seat;

        [Tooltip("Local-space offset from this object's origin to the seat position")]
        public Vector3 SeatOffset = new Vector3(0, 0.5f, 0);

        [Tooltip("Facing direction in local space (Euler angles)")]
        public Vector3 SeatRotationEuler = Vector3.zero;

        [Tooltip("Where the player appears on dismount (local space)")]
        public Vector3 DismountOffset = new Vector3(1f, 0, 0);

        [Header("Player Constraints")]
        [Tooltip("Hide the player's avatar while mounted")]
        public bool HidePlayerModel = false;

        [Tooltip("Forward player input to MountInput on this entity (for turrets, vehicles)")]
        public bool TransferInputToMount = false;

        [Header("Animation")]
        [Tooltip("Animator trigger name for mount enter (leave empty for no animation)")]
        public string MountAnimationTrigger = "";

        [Tooltip("Animator trigger name for mount exit (leave empty for no animation)")]
        public string DismountAnimationTrigger = "";

        [Tooltip("Duration of mount/dismount transition in seconds")]
        public float MountTransitionDuration = 0.5f;

        [Header("Ladder Settings")]
        [Tooltip("Climb speed in units/sec (Ladder type only)")]
        public float LadderSpeed = 2f;

        [Tooltip("Local Y minimum bound for ladder movement")]
        public float LadderMinY = 0f;

        [Tooltip("Local Y maximum bound for ladder movement")]
        public float LadderMaxY = 5f;

        [Header("Zipline Settings")]
        [Tooltip("Travel speed in units/sec (Zipline type only)")]
        public float ZiplineSpeed = 8f;

        [Tooltip("Maximum travel distance before auto-dismount (0 = infinite)")]
        public float ZiplineMaxDistance = 50f;

        public class Baker : Baker<MountPointAuthoring>
        {
            public override void Bake(MountPointAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                int mountAnimHash = string.IsNullOrEmpty(authoring.MountAnimationTrigger)
                    ? 0 : Animator.StringToHash(authoring.MountAnimationTrigger);
                int dismountAnimHash = string.IsNullOrEmpty(authoring.DismountAnimationTrigger)
                    ? 0 : Animator.StringToHash(authoring.DismountAnimationTrigger);

                AddComponent(entity, new MountPoint
                {
                    Type = authoring.Type,
                    SeatOffset = authoring.SeatOffset,
                    SeatRotation = quaternion.Euler(math.radians(authoring.SeatRotationEuler)),
                    DismountOffset = authoring.DismountOffset,
                    OccupantEntity = Entity.Null,
                    IsOccupied = false,
                    HidePlayerModel = authoring.HidePlayerModel,
                    TransferInputToMount = authoring.TransferInputToMount,
                    MountAnimationHash = mountAnimHash,
                    DismountAnimationHash = dismountAnimHash,
                    MountTransitionDuration = authoring.MountTransitionDuration,
                    LadderSpeed = authoring.LadderSpeed,
                    ZiplineSpeed = authoring.ZiplineSpeed,
                    LadderMinY = authoring.LadderMinY,
                    LadderMaxY = authoring.Type == MountType.Zipline
                        ? authoring.ZiplineMaxDistance
                        : authoring.LadderMaxY
                });

                // Add MountInput if this mount accepts input redirection
                if (authoring.TransferInputToMount)
                {
                    AddComponent(entity, new MountInput());
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Seat position (blue)
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            Vector3 seatWorld = transform.TransformPoint(SeatOffset);
            Gizmos.DrawWireSphere(seatWorld, 0.15f);

            // Seat facing direction (blue arrow)
            Quaternion seatRot = transform.rotation * Quaternion.Euler(SeatRotationEuler);
            Gizmos.DrawRay(seatWorld, seatRot * Vector3.forward * 0.5f);

            // Dismount position (green)
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
            Vector3 dismountWorld = transform.TransformPoint(DismountOffset);
            Gizmos.DrawWireSphere(dismountWorld, 0.12f);

            // Line from seat to dismount
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.4f);
            Gizmos.DrawLine(seatWorld, dismountWorld);

            // Ladder bounds (yellow vertical line)
            if (Type == MountType.Ladder)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
                Vector3 ladderBase = transform.TransformPoint(SeatOffset + Vector3.up * LadderMinY);
                Vector3 ladderTop = transform.TransformPoint(SeatOffset + Vector3.up * LadderMaxY);
                Gizmos.DrawLine(ladderBase, ladderTop);
                Gizmos.DrawWireSphere(ladderBase, 0.08f);
                Gizmos.DrawWireSphere(ladderTop, 0.08f);
            }

            // Zipline path (cyan forward line)
            if (Type == MountType.Zipline && ZiplineMaxDistance > 0)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
                Vector3 zipStart = seatWorld;
                Vector3 zipEnd = transform.TransformPoint(
                    SeatOffset + Vector3.forward * ZiplineMaxDistance);
                Gizmos.DrawLine(zipStart, zipEnd);
                Gizmos.DrawWireSphere(zipEnd, 0.08f);
            }
        }
#endif
    }
}
