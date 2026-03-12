using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.5: Authoring component for swept melee hitbox detection.
    /// Add to melee weapon prefabs to enable anti-tunneling collision.
    /// </summary>
    public class SweptMeleeAuthoring : MonoBehaviour
    {
        [Header("Hitbox Geometry")]
        [Tooltip("Offset from weapon pivot to blade tip")]
        public Vector3 tipOffset = new Vector3(0f, 0f, 1.2f);

        [Tooltip("Offset from weapon pivot to handle/base")]
        public Vector3 handleOffset = new Vector3(0f, 0f, 0.1f);

        [Tooltip("Radius for swept capsule detection")]
        public float capsuleRadius = 0.08f;

        [Header("Detection Settings")]
        [Tooltip("Enable swept detection (recommended for fast attacks)")]
        public bool useSweptDetection = true;

        [Tooltip("Maximum targets per swing (0 = unlimited)")]
        public int maxHitsPerSwing = 3;

        [Header("Physics Layers")]
        [Tooltip("Layers to detect hits on")]
        public LayerMask collisionMask = ~0;

        [Header("Preset")]
        [Tooltip("Use a preset configuration")]
        public MeleeWeaponPreset preset = MeleeWeaponPreset.Sword;

        public class SweptMeleeBaker : Baker<SweptMeleeAuthoring>
        {
            public override void Bake(SweptMeleeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Apply preset if not custom
                float3 tipOff = authoring.tipOffset;
                float3 handleOff = authoring.handleOffset;
                float radius = authoring.capsuleRadius;

                switch (authoring.preset)
                {
                    case MeleeWeaponPreset.Sword:
                        tipOff = new float3(0f, 0f, 1.2f);
                        handleOff = new float3(0f, 0f, 0.1f);
                        radius = 0.08f;
                        break;

                    case MeleeWeaponPreset.Greatsword:
                        tipOff = new float3(0f, 0f, 1.8f);
                        handleOff = new float3(0f, 0f, 0.2f);
                        radius = 0.12f;
                        break;

                    case MeleeWeaponPreset.Dagger:
                        tipOff = new float3(0f, 0f, 0.5f);
                        handleOff = new float3(0f, 0f, 0.05f);
                        radius = 0.05f;
                        break;

                    case MeleeWeaponPreset.Fist:
                        tipOff = new float3(0f, 0f, 0.4f);
                        handleOff = float3.zero;
                        radius = 0.12f;
                        break;

                    case MeleeWeaponPreset.Spear:
                        tipOff = new float3(0f, 0f, 2.2f);
                        handleOff = new float3(0f, 0f, 0.3f);
                        radius = 0.06f;
                        break;

                    case MeleeWeaponPreset.Axe:
                        tipOff = new float3(0f, 0f, 1.0f);
                        handleOff = new float3(0f, 0f, 0.15f);
                        radius = 0.15f;
                        break;

                    case MeleeWeaponPreset.Custom:
                        // Use authored values
                        tipOff = authoring.tipOffset;
                        handleOff = authoring.handleOffset;
                        radius = authoring.capsuleRadius;
                        break;
                }

                AddComponent(entity, new MeleeHitboxDefinition
                {
                    TipOffset = tipOff,
                    HandleOffset = handleOff,
                    CapsuleRadius = radius,
                    UseSweptDetection = authoring.useSweptDetection,
                    CollisionMask = (uint)authoring.collisionMask.value
                });

                AddComponent(entity, new SweptMeleeState
                {
                    PreviousTipPosition = float3.zero,
                    PreviousHandlePosition = float3.zero,
                    IsInitialized = false,
                    HitCount = 0,
                    MaxHitsPerSwing = authoring.maxHitsPerSwing
                });

                // Add hit event buffer
                AddBuffer<SweptMeleeHitEvent>(entity);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw hitbox geometry for debugging
            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 tip = tipOffset;
            Vector3 handle = handleOffset;

            // Draw capsule representation
            Gizmos.DrawWireSphere(tip, capsuleRadius);
            Gizmos.DrawWireSphere(handle, capsuleRadius);
            Gizmos.DrawLine(tip, handle);

            // Draw direction indicator
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(handle, tip + Vector3.forward * 0.1f);
        }
#endif
    }

    /// <summary>
    /// Preset configurations for common melee weapon types.
    /// </summary>
    public enum MeleeWeaponPreset
    {
        Sword,
        Greatsword,
        Dagger,
        Fist,
        Spear,
        Axe,
        Custom
    }
}
