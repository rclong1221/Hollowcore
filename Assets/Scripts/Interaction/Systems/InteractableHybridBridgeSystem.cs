using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using DIG.Interaction.Bridges;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 13.17.6 + 13.17.9: Hybrid bridge system for managed references.
    ///
    /// This managed system runs on the client and handles:
    /// - Audio playback via InteractableHybridLink
    /// - Animator parameter updates via InteractableHybridLink
    /// - Reset operations that need managed object access
    ///
    /// Uses a static registry to map entities to their hybrid links.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InteractableHybridBridgeSystem : SystemBase
    {
        /// <summary>
        /// Static registry mapping entities to their hybrid links.
        /// Populated by InteractableHybridLink.OnEnable/OnDisable.
        /// </summary>
        private static readonly Dictionary<Entity, InteractableHybridLink> s_EntityToHybridLink = new();

        /// <summary>
        /// Register a hybrid link for an entity.
        /// Called by InteractableHybridLink when it discovers its entity.
        /// </summary>
        public static void RegisterHybridLink(Entity entity, InteractableHybridLink link)
        {
            if (entity != Entity.Null && link != null)
            {
                s_EntityToHybridLink[entity] = link;
            }
        }

        /// <summary>
        /// Unregister a hybrid link for an entity.
        /// Called by InteractableHybridLink.OnDisable.
        /// </summary>
        public static void UnregisterHybridLink(Entity entity)
        {
            s_EntityToHybridLink.Remove(entity);
        }

        /// <summary>
        /// Get a hybrid link for an entity.
        /// </summary>
        public static InteractableHybridLink GetHybridLink(Entity entity)
        {
            s_EntityToHybridLink.TryGetValue(entity, out var link);
            return link;
        }

        protected override void OnUpdate()
        {
            // Process audio playback requests
            ProcessAudioPlayback();

            // Process animator parameter updates
            ProcessAnimatorParameters();

            // Process reset requests that need managed cleanup
            ProcessManagedResets();
        }

        /// <summary>
        /// EPIC 13.17.6: Play audio when AudioClipIndex changes.
        /// </summary>
        private void ProcessAudioPlayback()
        {
            foreach (var (animated, audioConfig, entity) in
                     SystemAPI.Query<RefRO<AnimatedInteractable>, RefRO<InteractableAudioConfig>>()
                     .WithEntityAccess())
            {
                // Find the hybrid link for this entity
                var hybridLink = GetHybridLink(entity);
                if (hybridLink == null)
                    continue;

                var anim = animated.ValueRO;
                var config = audioConfig.ValueRO;

                // Check if we need to play audio (index changed from previous frame)
                // AudioClipIndex >= 0 means play that index
                // AudioClipIndex == -2 means random selection
                if (anim.AudioClipIndex >= 0 && anim.IsAnimating && anim.CurrentTime < 0.05f)
                {
                    // Just started animating, play the clip
                    hybridLink.PlayAudioClip(anim.AudioClipIndex, config.Volume, config.PitchVariation);
                }
                else if (anim.AudioClipIndex == -2 && anim.IsAnimating && anim.CurrentTime < 0.05f)
                {
                    // Random selection
                    int randomIndex = Random.Range(0, config.ClipCount);
                    hybridLink.PlayAudioClip(randomIndex, config.Volume, config.PitchVariation);
                }
            }
        }

        /// <summary>
        /// EPIC 13.17.9: Update animator parameters when state changes.
        /// </summary>
        private void ProcessAnimatorParameters()
        {
            foreach (var (animated, entity) in
                     SystemAPI.Query<RefRO<AnimatedInteractable>>()
                     .WithEntityAccess())
            {
                var anim = animated.ValueRO;

                // Skip if no animator parameters configured
                if (anim.BoolParameterHash == 0 && anim.TriggerParameterHash == 0)
                    continue;

                var hybridLink = GetHybridLink(entity);
                if (hybridLink == null || !hybridLink.HasAnimator)
                    continue;

                // Update bool parameter based on IsOpen state
                if (anim.BoolParameterHash != 0)
                {
                    hybridLink.SetAnimatorBool(anim.BoolParameterHash, anim.IsOpen);
                }

                // Fire trigger at animation start
                if (anim.TriggerParameterHash != 0 && anim.IsAnimating && anim.CurrentTime < 0.05f)
                {
                    hybridLink.SetAnimatorTrigger(anim.TriggerParameterHash);
                }
            }
        }

        /// <summary>
        /// EPIC 13.17.7: Handle reset requests that need managed cleanup.
        /// </summary>
        private void ProcessManagedResets()
        {
            foreach (var (animated, entity) in
                     SystemAPI.Query<RefRO<AnimatedInteractable>>()
                     .WithAll<ResetInteractableRequest>()
                     .WithEntityAccess())
            {
                var hybridLink = GetHybridLink(entity);
                if (hybridLink != null)
                {
                    hybridLink.ResetInteractable();
                }
            }
        }
    }
}
