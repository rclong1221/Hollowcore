using UnityEngine;
using Unity.Mathematics;
using DIG.Targeting.Theming;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Simple built-in combat feedback provider using Unity's standard systems.
    /// Use this as a starting point or replace with Asset Store packages.
    /// </summary>
    public class SimpleCombatFeedback : MonoBehaviour, ICombatFeedbackProvider
    {
        [Header("Hit Stop")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private float defaultHitStopDuration = 0.05f;
        
        [Header("Camera Shake")]
        [SerializeField] private bool enableCameraShake = true;
        [SerializeField] private float maxShakeIntensity = 0.3f;
        [SerializeField] private Transform cameraTransform;
        
        [Header("Screen Flash")]
        [SerializeField] private bool enableDamageFlash = true;
        [SerializeField] private CanvasGroup damageFlashOverlay;
        [SerializeField] private float flashDuration = 0.1f;
        
        private Vector3 _originalCameraPosition;
        private float _shakeTimer;
        private float _shakeIntensity;
        private float _flashTimer;
        private float _hitStopTimer;
        private float _originalTimeScale;
        
        private void Awake()
        {
            if (cameraTransform == null)
                cameraTransform = Camera.main?.transform;
            
            if (cameraTransform != null)
                _originalCameraPosition = cameraTransform.localPosition;
        }
        
        private void OnEnable()
        {
            CombatUIRegistry.RegisterFeedback(this);
        }
        
        private void OnDisable()
        {
            CombatUIRegistry.UnregisterFeedback(this);
            
            // Restore time scale if we were in hit stop
            if (_hitStopTimer > 0)
            {
                Time.timeScale = _originalTimeScale;
            }
        }
        
        private void Update()
        {
            UpdateCameraShake();
            UpdateDamageFlash();
            UpdateHitStop();
        }
        
        private void UpdateCameraShake()
        {
            if (!enableCameraShake || cameraTransform == null) return;
            
            if (_shakeTimer > 0)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                float intensity = _shakeIntensity * (_shakeTimer / 0.15f); // Fade out
                
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-intensity, intensity),
                    UnityEngine.Random.Range(-intensity, intensity),
                    0
                );
                
                cameraTransform.localPosition = _originalCameraPosition + offset;
            }
            else if (cameraTransform.localPosition != _originalCameraPosition)
            {
                cameraTransform.localPosition = _originalCameraPosition;
            }
        }
        
        private void UpdateDamageFlash()
        {
            if (!enableDamageFlash || damageFlashOverlay == null) return;
            
            if (_flashTimer > 0)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                damageFlashOverlay.alpha = _flashTimer / flashDuration;
            }
            else if (damageFlashOverlay.alpha > 0)
            {
                damageFlashOverlay.alpha = 0;
            }
        }
        
        private void UpdateHitStop()
        {
            if (!enableHitStop) return;
            
            if (_hitStopTimer > 0)
            {
                _hitStopTimer -= Time.unscaledDeltaTime;
                if (_hitStopTimer <= 0)
                {
                    Time.timeScale = _originalTimeScale;
                }
            }
        }
        
        public void OnPlayerDealtDamage(float damage, HitType hitType, DamageType damageType)
        {
            // Slight camera punch for hits
            if (hitType == HitType.Critical)
            {
                TriggerCameraShake(0.2f, 0.1f);
            }
        }
        
        public void OnPlayerTookDamage(float damage, HitType hitType, DamageType damageType)
        {
            // Flash screen red
            if (enableDamageFlash && damageFlashOverlay != null)
            {
                _flashTimer = flashDuration;
                damageFlashOverlay.alpha = 1f;
            }
        }
        
        public void OnEntityKilled(bool wasPlayer, DamageType killingBlowType)
        {
            if (wasPlayer)
            {
                // Player died - could trigger death screen
                TriggerCameraShake(0.5f, 0.3f);
            }
            else
            {
                // Enemy killed - satisfying hit stop
                TriggerHitStop(0.03f);
            }
        }
        
        public void TriggerHitStop(float duration)
        {
            if (!enableHitStop) return;
            
            float actualDuration = duration > 0 ? duration : defaultHitStopDuration;
            
            _originalTimeScale = Time.timeScale;
            Time.timeScale = 0.05f;
            _hitStopTimer = actualDuration;
        }
        
        public void TriggerCameraShake(float intensity, float duration)
        {
            if (!enableCameraShake) return;
            
            _shakeIntensity = Mathf.Min(intensity, maxShakeIntensity);
            _shakeTimer = duration;
        }
    }
}
