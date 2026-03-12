using Unity.Entities;
using UnityEngine;
using DIG.Interaction.Systems;

namespace DIG.Interaction.Bridges
{
    /// <summary>
    /// EPIC 13.17.6 + 13.17.9: Hybrid link for interactable managed references.
    ///
    /// Stores managed UnityEngine references (AudioClips, Animator) that can't be
    /// stored in ECS components. The ECS system uses a static registry to find
    /// this component by entity.
    ///
    /// Usage:
    /// 1. Add to interactable GameObjects alongside InteractableAuthoring
    /// 2. Assign AudioClips and/or Animator references in Inspector
    /// 3. ECS systems use InteractableHybridBridgeSystem.GetHybridLink(entity)
    /// </summary>
    public class InteractableHybridLink : MonoBehaviour
    {
        [Header("EPIC 13.17.6: Audio")]
        [Tooltip("Audio clips played on interaction (cycles through)")]
        public AudioClip[] InteractAudioClips;

        [Tooltip("AudioSource to use (auto-created if null)")]
        public AudioSource AudioSource;

        [Header("EPIC 13.17.9: Animator")]
        [Tooltip("Animator for this interactable")]
        public Animator Animator;

        [Header("State")]
        [Tooltip("Current audio clip index for cycling")]
        public int CurrentAudioIndex = -1;

        // Cached entity reference for registration
        private Entity _entity = Entity.Null;
        private bool _isRegistered;

        private void Reset()
        {
            // Try to find components on same GameObject
            AudioSource = GetComponent<AudioSource>();
            Animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            // Auto-create AudioSource if we have clips but no source
            if (InteractAudioClips != null && InteractAudioClips.Length > 0 && AudioSource == null)
            {
                AudioSource = gameObject.AddComponent<AudioSource>();
                AudioSource.playOnAwake = false;
                AudioSource.spatialBlend = 1f; // 3D sound
            }

            // Try to find Animator if not set
            if (Animator == null)
            {
                Animator = GetComponent<Animator>();
            }
        }

        private void Start()
        {
            // Try to find associated entity and register
            TryRegisterWithEntity();
        }

        private void OnEnable()
        {
            if (_entity != Entity.Null && !_isRegistered)
            {
                InteractableHybridBridgeSystem.RegisterHybridLink(_entity, this);
                _isRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_entity != Entity.Null && _isRegistered)
            {
                InteractableHybridBridgeSystem.UnregisterHybridLink(_entity);
                _isRegistered = false;
            }
        }

        /// <summary>
        /// Try to find the ECS entity for this GameObject and register.
        /// </summary>
        private void TryRegisterWithEntity()
        {
            // Get the default world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            // Try to get entity from EntityManager using transform reference
            // This works for companion GameObjects created during baking
            var entityManager = world.EntityManager;

            // Search for entity with matching transform position (for runtime spawned objects)
            // or use the baked entity reference if available
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<AnimatedInteractable>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()
            );

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var transforms = query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);

            Vector3 myPos = transform.position;
            float closestDist = float.MaxValue;
            Entity closestEntity = Entity.Null;

            for (int i = 0; i < entities.Length; i++)
            {
                Vector3 entityPos = transforms[i].Position;
                float dist = Vector3.Distance(myPos, entityPos);

                // Very close match (within 0.1 units)
                if (dist < 0.1f && dist < closestDist)
                {
                    closestDist = dist;
                    closestEntity = entities[i];
                }
            }

            entities.Dispose();
            transforms.Dispose();

            if (closestEntity != Entity.Null)
            {
                _entity = closestEntity;
                InteractableHybridBridgeSystem.RegisterHybridLink(_entity, this);
                _isRegistered = true;
            }
        }

        /// <summary>
        /// Manually set the entity for this hybrid link.
        /// Call this from an authoring component or spawning system.
        /// </summary>
        public void SetEntity(Entity entity)
        {
            if (_isRegistered && _entity != Entity.Null)
            {
                InteractableHybridBridgeSystem.UnregisterHybridLink(_entity);
            }

            _entity = entity;

            if (_entity != Entity.Null && enabled)
            {
                InteractableHybridBridgeSystem.RegisterHybridLink(_entity, this);
                _isRegistered = true;
            }
        }

        /// <summary>
        /// Play the next audio clip in sequence (EPIC 13.17.6).
        /// </summary>
        /// <param name="volume">Playback volume (0-1)</param>
        /// <param name="pitchVariation">Random pitch variation (+/- from 1.0)</param>
        public void PlayNextAudioClip(float volume = 1f, float pitchVariation = 0f)
        {
            if (InteractAudioClips == null || InteractAudioClips.Length == 0 || AudioSource == null)
                return;

            // Cycle to next clip
            CurrentAudioIndex = (CurrentAudioIndex + 1) % InteractAudioClips.Length;
            var clip = InteractAudioClips[CurrentAudioIndex];

            if (clip != null)
            {
                AudioSource.clip = clip;
                AudioSource.volume = volume;

                if (pitchVariation > 0)
                    AudioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                else
                    AudioSource.pitch = 1f;

                AudioSource.Play();
            }
        }

        /// <summary>
        /// Play a specific audio clip by index (EPIC 13.17.6).
        /// </summary>
        /// <param name="index">Clip index</param>
        /// <param name="volume">Playback volume</param>
        /// <param name="pitchVariation">Random pitch variation</param>
        public void PlayAudioClip(int index, float volume = 1f, float pitchVariation = 0f)
        {
            if (InteractAudioClips == null || index < 0 || index >= InteractAudioClips.Length || AudioSource == null)
                return;

            CurrentAudioIndex = index;
            var clip = InteractAudioClips[index];

            if (clip != null)
            {
                AudioSource.clip = clip;
                AudioSource.volume = volume;

                if (pitchVariation > 0)
                    AudioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                else
                    AudioSource.pitch = 1f;

                AudioSource.Play();
            }
        }

        /// <summary>
        /// Set animator bool parameter (EPIC 13.17.9).
        /// </summary>
        /// <param name="parameterHash">Parameter hash from Animator.StringToHash</param>
        /// <param name="value">Bool value to set</param>
        public void SetAnimatorBool(int parameterHash, bool value)
        {
            if (Animator != null && parameterHash != 0)
            {
                Animator.SetBool(parameterHash, value);
            }
        }

        /// <summary>
        /// Set animator trigger parameter (EPIC 13.17.9).
        /// </summary>
        /// <param name="parameterHash">Parameter hash from Animator.StringToHash</param>
        public void SetAnimatorTrigger(int parameterHash)
        {
            if (Animator != null && parameterHash != 0)
            {
                Animator.SetTrigger(parameterHash);
            }
        }

        /// <summary>
        /// Reset the interactable state (EPIC 13.17.7).
        /// Resets audio index, animator, and any other managed state.
        /// </summary>
        public void ResetInteractable()
        {
            CurrentAudioIndex = -1;

            if (Animator != null)
            {
                Animator.Rebind();
                Animator.Update(0f);
            }
        }

        /// <summary>
        /// Check if this interactable has audio configured.
        /// </summary>
        public bool HasAudio => InteractAudioClips != null && InteractAudioClips.Length > 0;

        /// <summary>
        /// Check if this interactable has an animator configured.
        /// </summary>
        public bool HasAnimator => Animator != null;

        /// <summary>
        /// Get the number of audio clips available.
        /// </summary>
        public int AudioClipCount => InteractAudioClips?.Length ?? 0;

        /// <summary>
        /// Get the associated entity.
        /// </summary>
        public Entity Entity => _entity;
    }

    /// <summary>
    /// Class component for linking managed InteractableHybridLink to ECS entity.
    /// </summary>
    public class InteractableHybridLinkComponent : IComponentData
    {
        public InteractableHybridLink Link;
    }
}
