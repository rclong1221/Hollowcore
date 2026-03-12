using UnityEngine;
using UnityEngine.UI;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Controller for airlock transition screen effects.
    /// Manages helmet HUD, screen flashes, and post-process transitions.
    /// </summary>
    /// <remarks>
    /// Setup:
    /// 1. Add to a Canvas overlay layer
    /// 2. Create child objects for helmet HUD elements
    /// 3. Add a full-screen Image for flash effects
    /// 4. Optionally connect to post-process volume
    /// </remarks>
    public class AirlockScreenFXController : MonoBehaviour
    {
        public static AirlockScreenFXController Instance { get; private set; }

        [Header("Helmet HUD")]
        [Tooltip("Root object for EVA helmet HUD elements")]
        public GameObject HelmetHUDRoot;

        [Tooltip("Helmet visor overlay image")]
        public Image HelmetVisorOverlay;

        [Tooltip("Helmet edge vignette")]
        public Image HelmetVignette;

        [Header("Flash Effects")]
        [Tooltip("Full-screen flash image for teleport effect")]
        public Image TeleportFlashImage;

        [Tooltip("Color for vacuum transition flash")]
        public Color VacuumFlashColor = new Color(0.1f, 0.2f, 0.4f, 1f);

        [Tooltip("Color for pressurized transition flash")]
        public Color PressurizedFlashColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("Timing")]
        [Tooltip("Duration of teleport flash")]
        public float FlashDuration = 0.3f;

        [Tooltip("Time to fade in/out helmet HUD")]
        public float HelmetFadeDuration = 0.5f;

        [Header("Audio")]
        [Tooltip("Sound for helmet activation")]
        public AudioClip HelmetActivateSound;

        [Tooltip("Sound for helmet deactivation")]
        public AudioClip HelmetDeactivateSound;

        private AudioSource _audioSource;
        private float _currentHelmetAlpha;
        private float _targetHelmetAlpha;
        private float _flashAlpha;
        private bool _isInEVA;
        private bool _flashActive;
        private Color _currentFlashColor;

        void Awake()
        {
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D sound for UI
            }

            // Initialize hidden
            if (HelmetHUDRoot != null)
                HelmetHUDRoot.SetActive(false);

            if (TeleportFlashImage != null)
            {
                var color = TeleportFlashImage.color;
                color.a = 0f;
                TeleportFlashImage.color = color;
            }

            if (HelmetVisorOverlay != null)
            {
                var color = HelmetVisorOverlay.color;
                color.a = 0f;
                HelmetVisorOverlay.color = color;
            }

            if (HelmetVignette != null)
            {
                var color = HelmetVignette.color;
                color.a = 0f;
                HelmetVignette.color = color;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Animate helmet fade
            if (Mathf.Abs(_currentHelmetAlpha - _targetHelmetAlpha) > 0.01f)
            {
                _currentHelmetAlpha = Mathf.Lerp(_currentHelmetAlpha, _targetHelmetAlpha, 
                    Time.deltaTime / HelmetFadeDuration * 3f);
                
                UpdateHelmetAlpha(_currentHelmetAlpha);
            }

            // Animate flash
            if (_flashActive)
            {
                _flashAlpha -= Time.deltaTime / FlashDuration;
                if (_flashAlpha <= 0f)
                {
                    _flashAlpha = 0f;
                    _flashActive = false;
                }

                if (TeleportFlashImage != null)
                {
                    var color = _currentFlashColor;
                    color.a = _flashAlpha;
                    TeleportFlashImage.color = color;
                }
            }
        }

        /// <summary>
        /// Begin visual transition for exiting to vacuum.
        /// </summary>
        public void BeginVacuumTransition()
        {
            _currentFlashColor = VacuumFlashColor;
            
            // Start slight darkening/vignette
            if (HelmetVignette != null)
            {
                var color = HelmetVignette.color;
                color.a = 0.2f;
                HelmetVignette.color = color;
            }
        }

        /// <summary>
        /// Begin visual transition for entering pressurized area.
        /// </summary>
        public void BeginPressurizeTransition()
        {
            _currentFlashColor = PressurizedFlashColor;
        }

        /// <summary>
        /// Update visuals during transition progress.
        /// </summary>
        public void UpdateTransitionProgress(float progress)
        {
            // Increase vignette as we approach teleport
            if (HelmetVignette != null)
            {
                var color = HelmetVignette.color;
                color.a = Mathf.Lerp(0.2f, 0.5f, progress);
                HelmetVignette.color = color;
            }
        }

        /// <summary>
        /// Prepare for the teleport flash (called just before completion).
        /// </summary>
        public void PrepareTeleportFlash()
        {
            // Could add screen effects here like distortion
        }

        /// <summary>
        /// Trigger the teleport flash effect.
        /// </summary>
        public void TriggerTeleportFlash()
        {
            _flashAlpha = 1f;
            _flashActive = true;

            if (TeleportFlashImage != null)
            {
                var color = _currentFlashColor;
                color.a = 1f;
                TeleportFlashImage.color = color;
            }

            // Clear vignette
            if (HelmetVignette != null)
            {
                var color = HelmetVignette.color;
                color.a = 0f;
                HelmetVignette.color = color;
            }
        }

        /// <summary>
        /// Activate EVA mode helmet HUD.
        /// </summary>
        public void ActivateEVAMode()
        {
            if (_isInEVA) return;
            _isInEVA = true;

            if (HelmetHUDRoot != null)
                HelmetHUDRoot.SetActive(true);

            _targetHelmetAlpha = 1f;

            // Play helmet activation sound
            if (_audioSource != null && HelmetActivateSound != null)
            {
                _audioSource.PlayOneShot(HelmetActivateSound);
            }
        }

        /// <summary>
        /// Deactivate EVA mode helmet HUD.
        /// </summary>
        public void DeactivateEVAMode()
        {
            if (!_isInEVA) return;
            _isInEVA = false;

            _targetHelmetAlpha = 0f;

            // Play helmet deactivation sound
            if (_audioSource != null && HelmetDeactivateSound != null)
            {
                _audioSource.PlayOneShot(HelmetDeactivateSound);
            }

            // Will be disabled when fully faded
            StartCoroutine(DisableHelmetAfterFade());
        }

        private void UpdateHelmetAlpha(float alpha)
        {
            if (HelmetVisorOverlay != null)
            {
                var color = HelmetVisorOverlay.color;
                color.a = alpha * 0.3f; // Subtle visor tint
                HelmetVisorOverlay.color = color;
            }
        }

        System.Collections.IEnumerator DisableHelmetAfterFade()
        {
            yield return new WaitForSeconds(HelmetFadeDuration);
            
            if (!_isInEVA && HelmetHUDRoot != null)
            {
                HelmetHUDRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Force set EVA mode (for initialization or debug).
        /// </summary>
        public void ForceEVAMode(bool enabled)
        {
            _isInEVA = enabled;
            _currentHelmetAlpha = enabled ? 1f : 0f;
            _targetHelmetAlpha = _currentHelmetAlpha;
            
            if (HelmetHUDRoot != null)
                HelmetHUDRoot.SetActive(enabled);

            UpdateHelmetAlpha(_currentHelmetAlpha);
        }
    }
}
