using UnityEngine;
using Unity.Entities;
using Player.Components;
using Audio.Systems;
using DIG.Items; // For DIGEquipmentProvider

namespace Player.Bridges
{
    /// <summary>
    /// Bridges physical footstep triggers to the ECS FootstepEvent system.
    /// Attached to the Character Root.
    /// </summary>
    public class CharacterFootEffects : MonoBehaviour
    {
        [Tooltip("Layer mask to detect ground surfaces.")]
        public LayerMask GroundLayers = -1;

        private DIGEquipmentProvider _provider;

        private void Awake()
        {
            _provider = GetComponent<DIGEquipmentProvider>();
        }

        /// <summary>
        /// Called by FootstepTrigger when foot hits ground.
        /// </summary>
        /// <param name="groundCollider">The collider hit by the foot</param>
        /// <param name="position">World position of the foot</param>
        /// <summary>
        /// Called by FootstepTrigger when foot hits ground.
        /// </summary>
        /// <param name="groundCollider">The collider hit by the foot</param>
        /// <param name="position">World position of the foot</param>
        public void OnFootstepHit(Collider groundCollider, Vector3 position)
        {
            // 1. Filter Layers
            if (((1 << groundCollider.gameObject.layer) & GroundLayers) == 0)
            {
                return;
            }

            // 2. Resolve Material ID (using our isolated SurfaceDetectionService)
            // Note: We only pass the GameObject since we are in Mono context here.
            int matID = SurfaceDetectionService.ResolveMaterialId(default, Entity.Null, groundCollider.gameObject);

            // 3. Dispatch to ECS
            if (_provider != null && _provider.PlayerEntity != Entity.Null)
            {
                var em = _provider.EntityWorld.EntityManager;

                // Determine stance/movement type for audio
                int audioStance = 0; // Default Walk
                if (em.HasComponent<PlayerState>(_provider.PlayerEntity))
                {
                    var pState = em.GetComponentData<PlayerState>(_provider.PlayerEntity);
                    
                    if (pState.Stance == PlayerStance.Crouching)
                    {
                        audioStance = 1; // Crouch
                    }
                    else if (pState.MovementState == PlayerMovementState.Running || 
                             pState.MovementState == PlayerMovementState.Sprinting)
                    {
                        audioStance = 3; // Run
                    }
                }

                // Create and add the transient FootstepEvent
                var evt = new FootstepEvent
                {
                    Position = position,
                    MaterialId = matID,
                    Stance = audioStance, 
                    Intensity = 1.0f,
                    FootIndex = 0 // Generic index for now
                };

                // Add component directly. The FootstepSystem/AudioPlaybackSystem will consume it.
                // Using AddComponentData is safe here since we are on the main thread (MonoBehaviour).
                em.AddComponentData(_provider.PlayerEntity, evt);
            }
        }
    }
}
