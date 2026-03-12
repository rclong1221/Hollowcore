using UnityEngine;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Objects;

namespace Opsive.UltimateCharacterController.AddOns.Swimming.Demo
{
    /// <summary>
    /// Adds a basic fog effect when the character is underwater.
    /// </summary>
    public class UnderwaterEffect : MonoBehaviour
    {
        [Tooltip("The color of the water fog.")]
        [SerializeField] protected Color m_WaterColor = new Color(0, 0.4f, 0.7f, 0.6f);
        [Tooltip("The clip that should play when underwater.")]
        [SerializeField] protected AudioClip m_AmbientAudioClip;
        [Tooltip("The Object Identifier ID of the water.")]
        [SerializeField] protected int m_WaterID = 301;

        private Transform m_Transform;
        private AudioSource m_AudioSource;
        private List<BoxCollider> m_WaterColliders = new List<BoxCollider>();

        private Color m_DefaultColor;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_DefaultColor = RenderSettings.fogColor;
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource != null) {
                m_AudioSource.clip = m_AmbientAudioClip;
            }
            EnableEffect(false);

#if UNITY_2023_1_OR_NEWER
            var objectIdentifiers = GameObject.FindObjectsByType<ObjectIdentifier>(FindObjectsSortMode.None);
#else
            var objectIdentifiers = GameObject.FindObjectsOfType<ObjectIdentifier>();
#endif
            for (int i = 0; i < objectIdentifiers.Length; ++i) {
                if (objectIdentifiers[i].ID != m_WaterID) {
                    continue;
                }

                var boxCollider = objectIdentifiers[i].GetComponent<BoxCollider>();
                if (boxCollider != null) {
                    m_WaterColliders.Add(boxCollider);
                }
            }
        }

        /// <summary>
        /// Change the water effect based on depth.
        /// </summary>
        private void Update()
        {
            // Enable the effect if the camera is within the water bounds.
            var enableEffect = false;
            for (int i = 0; i < m_WaterColliders.Count; ++i) {
                if (m_WaterColliders[i].bounds.Contains(m_Transform.position)) {
                    enableEffect = true;
                    break;
                }
            }

            if (RenderSettings.fog != enableEffect) {
                EnableEffect(enableEffect);
            }
        }

        /// <summary>
        /// Enables or disables the water effect.
        /// </summary>
        /// <param name="enableEffect">Should the effect be enabled?</param>
        private void EnableEffect(bool enableEffect)
        {
            if (enableEffect) {
                if (m_AudioSource != null) {
                    m_AudioSource.Play();
                }
                RenderSettings.fog = true;
                RenderSettings.fogColor = m_WaterColor;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.15f;
            } else {
                if (m_AudioSource != null) {
                    m_AudioSource.Stop();
                }
                RenderSettings.fog = false;
            }
        }
    }
}