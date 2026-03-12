/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming
{
    using Opsive.UltimateCharacterController.Character;
    using Opsive.Shared.Game;
    using UnityEngine;

    /// <summary>
    /// Stores and plays a water effect at the specified location.
    /// </summary>
    [System.Serializable]
    public class WaterEffect
    {
        [Tooltip("The prefab of the particle that should play.")]
        [SerializeField] protected GameObject m_ParticlePrefab;
        [Tooltip("The location that the particle should play.")]
        [SerializeField] protected Transform m_Location;
        [Tooltip("The audio that should play when the effect plays.")]
        [SerializeField] protected Shared.Audio.AudioClipSet m_AudioClipSet = new Shared.Audio.AudioClipSet();

        public GameObject ParticlePrefab { get { return m_ParticlePrefab; } }
        public Transform Location { get { return m_Location; } }

        private ParticleSystem m_Particles;
        private bool m_Continuous;
        private bool m_Playing;

        public bool Continuous { set { m_Continuous = value; } }
        public bool IsPlaying { get { return m_Playing; } }

        /// <summary>
        /// Plays the effect.
        /// </summary>
        public virtual void Play()
        {
            if (m_Location == null) {
                return;
            }

            Play(m_Location.position);
        }

        /// <summary>
        /// Plays the effect at the specified position.
        /// </summary>
        /// <param name="position">The position to play the effect.</param>
        public virtual void Play(Vector3 position)
        {
            if (m_ParticlePrefab != null) {
                // Continuous effects will persist on the character and have to be stopped manually.
                if (!m_Continuous || m_Particles == null) {
                    var instantiatedParticles = ObjectPoolBase.Instantiate(m_ParticlePrefab, position, m_ParticlePrefab.transform.rotation);
                    if (m_Continuous) {
                        instantiatedParticles.transform.parent = m_Location;
                        m_Particles = instantiatedParticles.GetCachedComponent<ParticleSystem>();
                    }
                }
            }

            m_Playing = true;
            if (m_Particles != null) {
                m_Particles.Play();
            }

            if (m_Continuous) {
                PlayAudioClip();
            } else {
                m_AudioClipSet.PlayAtPosition(position);
            }
        }

        /// <summary>
        /// Plays the AudioClip at the particle location.
        /// </summary>
        private void PlayAudioClip()
        {
            if (!m_Playing || m_Particles == null) {
                return;
            }
            var audioSource = m_AudioClipSet.PlayAudioClip(m_Particles.gameObject).AudioSource;
            if (audioSource == null || audioSource.clip == null) {
                return;
            }

            Scheduler.Schedule(audioSource.clip.length, PlayAudioClip);
        }

        /// <summary>
        /// Stops the effect. Only applies to continuous effects.
        /// </summary>
        public void Stop()
        {
            m_Playing = false;
            if (m_Particles != null) {
                m_Particles.Stop();
            }
        }
    }

    /// <summary>
    /// Inherits WaterEffect allowing a minimum velocity to be specified that limits when the splash can be played.
    /// </summary>
    [System.Serializable]
    public class WaterEffectVelocity : WaterEffect
    {
        [Tooltip("The minimum velocity that the character must be moving at in order for the effect to be played.")]
        [SerializeField] protected float m_MinVelocity;

        public float MinVelocity { get { return m_MinVelocity; } }

        protected UltimateCharacterLocomotion m_CharacterLocomotion;

        /// <summary>
        /// Initializes the object.
        /// </summary>
        /// <param name="characterLocomotion">A reference to the UltimateCharacterLocomotion component.</param>
        public virtual void Initialize(UltimateCharacterLocomotion characterLocomotion)
        {
            m_CharacterLocomotion = characterLocomotion;
        }

        /// <summary>
        /// Plays the effect if the character is moving faster than the specified velocity.
        /// </summary>
        public override void Play()
        {
            if (m_CharacterLocomotion.LocalVelocity.magnitude < m_MinVelocity) {
                return;
            }

            base.Play();
        }

        /// <summary>
        /// Plays the effect at the specified position.
        /// </summary>
        /// <param name="position">The position to play the effect.</param>
        public override void Play(Vector3 position)
        {
            if (m_CharacterLocomotion.LocalVelocity.magnitude < m_MinVelocity) {
                return;
            }

            base.Play(position);
        }
    }

    /// <summary>
    /// Inherits WaterEffectVelocity allowing an effect to be played based on an event.
    /// </summary>
    [System.Serializable]
    public class WaterEffectVelocityEvent : WaterEffectVelocity
    {
        [Tooltip("The name of the event that should play the effect.")]
        [SerializeField] protected string m_EventName;

        public string EventName { get { return m_EventName; } }
        public System.Action OnEvent { get { return () => { Play(); }; } }
    }

    /// <summary>
    /// Inherits WaterEffectVelocityEvent allowing a effect to be played based on the water depth.
    /// </summary>
    [System.Serializable]
    public class WaterEffectVelocityEventDistance : WaterEffectVelocityEvent
    {
        [Tooltip("The maximum distance that the location transform can be away from the surface.")]
        [SerializeField] protected float m_MaxSurfaceDistance = 0.45f;

        private Swim m_SwimAbility;

        /// <summary>
        /// Initializes the object.
        /// </summary>
        /// <param name="characterLocomotion">A reference to the UltimateCharacterLocomotion component.</param>
        public override void Initialize(UltimateCharacterLocomotion characterLocomotion)
        {
            base.Initialize(characterLocomotion);

            m_SwimAbility = m_CharacterLocomotion.GetAbility<Swim>();
        }

        /// <summary>
        /// Plays the effect.
        /// </summary>
        public override void Play()
        {
            // The event may be called when the limb is too far away from the surface of the water collider.
            if (m_SwimAbility.GetDepthInWater(m_Location) > m_MaxSurfaceDistance) {
                return;
            }

            base.Play();
        }
    }
}