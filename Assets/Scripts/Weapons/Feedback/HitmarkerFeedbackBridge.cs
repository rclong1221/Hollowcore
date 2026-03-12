using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;
using MoreMountains.Feedbacks;

namespace DIG.Weapons.Feedback
{
    /// <summary>
    /// EPIC 15.5: Bridge between ECS hit events and FEEL feedback system.
    /// Triggers hitmarkers, screen shake, hit sounds, and hitstop on confirmed hits.
    /// </summary>
    public class HitmarkerFeedbackBridge : MonoBehaviour
    {
        public static HitmarkerFeedbackBridge Instance { get; private set; }

        [Header("FEEL Feedbacks")]
        [Tooltip("Feedback player for regular hit")]
        [SerializeField] private MMF_Player hitFeedback;

        [Tooltip("Feedback player for headshot/critical hit")]
        [SerializeField] private MMF_Player criticalHitFeedback;

        [Tooltip("Feedback player for kill confirmation")]
        [SerializeField] private MMF_Player killFeedback;

        [Header("Audio Clips")]
        [Tooltip("Sound for regular hit confirmation")]
        [SerializeField] private AudioClip hitSound;

        [Tooltip("Sound for headshot/critical")]
        [SerializeField] private AudioClip criticalSound;

        [Tooltip("Sound for kill")]
        [SerializeField] private AudioClip killSound;

        [Header("Hitstop (Freeze Frame)")]
        [Tooltip("Enable hitstop on hit")]
        [SerializeField] private bool enableHitstop = true;

        [Tooltip("Hitstop duration for regular hits (seconds)")]
        [SerializeField] private float hitstopDuration = 0.05f;

        [Tooltip("Hitstop duration for critical hits (seconds)")]
        [SerializeField] private float criticalHitstopDuration = 0.08f;

        [Header("Visual Recoil Integration")]
        [Tooltip("Feedback for weapon kick (FOV punch, camera recoil)")]
        [SerializeField] private MMF_Player recoilKickFeedback;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Audio source for hit sounds
        private AudioSource _audioSource;

        // Hitstop state
        private float _hitstopTimer;
        private float _originalTimeScale;
        private bool _inHitstop;

        // Stats tracking
        private int _hitsThisSession;
        private int _criticalsThisSession;
        private int _killsThisSession;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Get or create audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D sound
            }
        }

        private void Update()
        {
            // Handle hitstop recovery
            if (_inHitstop)
            {
                _hitstopTimer -= Time.unscaledDeltaTime;
                if (_hitstopTimer <= 0)
                {
                    EndHitstop();
                }
            }
        }

        /// <summary>
        /// Trigger feedback for a confirmed hit.
        /// </summary>
        public void OnHitConfirmed(HitConfirmation hit)
        {
            _hitsThisSession++;

            // Determine feedback type
            bool isCritical = hit.IsCritical;
            bool isKill = hit.IsKill;

            if (debugLogging)
            {
                Debug.Log($"[HitmarkerFeedback] Hit confirmed: Critical={isCritical} Kill={isKill} " +
                    $"Damage={hit.Damage} Region={hit.HitRegion}");
            }

            // Trigger appropriate FEEL feedback
            if (isKill && killFeedback != null)
            {
                killFeedback.PlayFeedbacks();
                _killsThisSession++;
            }
            else if (isCritical && criticalHitFeedback != null)
            {
                criticalHitFeedback.PlayFeedbacks();
                _criticalsThisSession++;
            }
            else if (hitFeedback != null)
            {
                hitFeedback.PlayFeedbacks();
            }

            // Play hit sound
            PlayHitSound(isCritical, isKill);

            // Show hitmarker UI
            ShowHitmarkerUI(isCritical, isKill);

            // Apply hitstop
            if (enableHitstop)
            {
                ApplyHitstop(isCritical ? criticalHitstopDuration : hitstopDuration);
            }

            // Trigger camera shake via Cinemachine Impulse (if configured in FEEL)
            // The MMF_CinemachineImpulse in the feedback player handles this
        }

        /// <summary>
        /// Trigger visual recoil feedback (separate from gameplay recoil).
        /// </summary>
        public void OnWeaponFired(float kickStrength, float fovPunch)
        {
            if (recoilKickFeedback != null)
            {
                // Modify feedback intensity based on kick strength
                // The feedback player should have MMF_CameraFieldOfView and MMF_CinemachineImpulse
                recoilKickFeedback.PlayFeedbacks();
            }
        }

        private void PlayHitSound(bool isCritical, bool isKill)
        {
            AudioClip clip = hitSound;

            if (isKill && killSound != null)
            {
                clip = killSound;
            }
            else if (isCritical && criticalSound != null)
            {
                clip = criticalSound;
            }

            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void ShowHitmarkerUI(bool isCritical, bool isKill)
        {
            // Use existing WeaponHUD hitmarker
            if (UI.WeaponHUD.Instance != null)
            {
                UI.WeaponHUD.Instance.ShowHitMarker(isCritical);
            }
        }

        private void ApplyHitstop(float duration)
        {
            if (_inHitstop)
            {
                // Extend existing hitstop
                _hitstopTimer = Mathf.Max(_hitstopTimer, duration);
                return;
            }

            _inHitstop = true;
            _hitstopTimer = duration;
            _originalTimeScale = Time.timeScale;
            Time.timeScale = 0.01f; // Nearly frozen
        }

        private void EndHitstop()
        {
            _inHitstop = false;
            Time.timeScale = _originalTimeScale;
        }

        /// <summary>
        /// Get session statistics.
        /// </summary>
        public (int hits, int criticals, int kills) GetStats()
        {
            return (_hitsThisSession, _criticalsThisSession, _killsThisSession);
        }

        /// <summary>
        /// Reset session statistics.
        /// </summary>
        public void ResetStats()
        {
            _hitsThisSession = 0;
            _criticalsThisSession = 0;
            _killsThisSession = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Ensure timeScale is restored
            if (_inHitstop)
            {
                Time.timeScale = _originalTimeScale;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Restore timeScale when app pauses
            if (pauseStatus && _inHitstop)
            {
                Time.timeScale = _originalTimeScale;
                _inHitstop = false;
            }
        }
    }

    /// <summary>
    /// EPIC 15.5: Data structure for hit confirmation events.
    /// </summary>
    public struct HitConfirmation
    {
        public Entity TargetEntity;
        public float3 HitPosition;
        public float3 HitNormal;
        public float Damage;
        public bool IsCritical;
        public bool IsKill;
        public HitboxRegion HitRegion;
        public uint ServerTick;
    }
}
