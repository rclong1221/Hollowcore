using UnityEngine;
using MoreMountains.Feedbacks;
using Audio.Systems;
using System.Collections.Generic;

namespace DIG.Core.Feedback
{
    /// <summary>
    /// Central manager for gameplay haptic and visual feedback using FEEL.
    /// Provides static access for ECS systems to trigger feedbacks via bridge pattern.
    /// </summary>
    public class GameplayFeedbackManager : MonoBehaviour
    {
        public static GameplayFeedbackManager Instance { get; private set; }

        [Header("Data")]
        [Tooltip("Registry for looking up audio clips by surface ID")]
        [SerializeField] private SurfaceMaterialRegistry _surfaceRegistry;

        [Header("Combat Feedbacks")]
        [Tooltip("Feedback played when firing a weapon")]
        [SerializeField] private MMF_Player _fireFeedback;
        
        [Tooltip("Feedback played when taking damage")]
        [SerializeField] private MMF_Player _damageFeedback;
        
        [Tooltip("Feedback played on heavy hit/critical")]
        [SerializeField] private MMF_Player _heavyHitFeedback;

        [Header("Movement Feedbacks")]
        [Tooltip("Feedback played when footing/stepping")]
        [SerializeField] private MMF_Player _footstepFeedback;

        [Tooltip("Feedback played when jumping")]
        [SerializeField] private MMF_Player _jumpFeedback;

        [Tooltip("Feedback played when rolling/dodging")]
        [SerializeField] private MMF_Player _rollFeedback;

        [Tooltip("Feedback played when diving")]
        [SerializeField] private MMF_Player _diveFeedback;

        [Tooltip("Feedback played when starting a climb")]
        [SerializeField] private MMF_Player _climbStartFeedback;

        [Tooltip("Feedback played when landing from a jump/fall")]
        [SerializeField] private MMF_Player _landFeedback;
        
        [Tooltip("Feedback played when sliding")]
        [SerializeField] private MMF_Player _slideFeedback;
        
        [Tooltip("Feedback played when wall running")]
        [SerializeField] private MMF_Player _wallRunFeedback;

        [Header("Interaction Feedbacks")]
        [Tooltip("Feedback played when picking up an item")]
        [SerializeField] private MMF_Player _pickupFeedback;
        
        [Tooltip("Feedback played on successful interaction")]
        [SerializeField] private MMF_Player _interactFeedback;

        [Header("Settings")]
        [Tooltip("Global intensity multiplier for all feedbacks")]
        [Range(0f, 2f)]
        [SerializeField] private float _globalIntensity = 1f;
        
        [Tooltip("Whether haptics are enabled")]
        [SerializeField] private bool _hapticsEnabled = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ===== Combat Events =====

        /// <summary>Trigger fire feedback (rumble + screen shake)</summary>
        public void OnFire()
        {
            PlayFeedback(_fireFeedback, _globalIntensity);
        }

        /// <summary>Trigger damage feedback with intensity based on damage amount</summary>
        public void OnDamage(float normalizedIntensity = 1f)
        {
            PlayFeedback(_damageFeedback, normalizedIntensity * _globalIntensity);
        }

        /// <summary>Trigger heavy hit feedback (critical, explosion, etc.)</summary>
        public void OnHeavyHit()
        {
            PlayFeedback(_heavyHitFeedback, _globalIntensity);
        }

        // ===== Movement Events =====

        /// <summary>Trigger footstep feedback with surface-dependent audio</summary>
        public void OnFootstep(int materialId, int stance, Vector3 position)
        {
            PrepareSurfaceFeedback(_footstepFeedback, materialId, mat => 
            {
                if (stance == 3) return mat.RunClips; // Run
                if (stance == 1) return mat.CrouchClips; // Crouch
                return mat.WalkClips; // Walk
            });
            PlayFeedback(_footstepFeedback, position, _globalIntensity);
        }

        /// <summary>Trigger jump feedback</summary>
        public void OnJump(int materialId, float intensity, Vector3 position)
        {
            PrepareSurfaceFeedback(_jumpFeedback, materialId, mat => mat.JumpClips);
            PlayFeedback(_jumpFeedback, position, intensity * _globalIntensity);
        }

        /// <summary>Trigger roll feedback</summary>
        public void OnRoll(int materialId, float intensity, Vector3 position)
        {
            PrepareSurfaceFeedback(_rollFeedback, materialId, mat => mat.RollClips);
            PlayFeedback(_rollFeedback, position, intensity * _globalIntensity);
        }

        /// <summary>Trigger dive feedback</summary>
        public void OnDive(int materialId, float intensity, Vector3 position)
        {
            PrepareSurfaceFeedback(_diveFeedback, materialId, mat => mat.DiveClips);
            PlayFeedback(_diveFeedback, position, intensity * _globalIntensity);
        }

        /// <summary>Trigger climb start feedback</summary>
        public void OnClimbStart(int materialId, Vector3 position)
        {
            PrepareSurfaceFeedback(_climbStartFeedback, materialId, mat => mat.ClimbClips);
            PlayFeedback(_climbStartFeedback, position, _globalIntensity);
        }

        /// <summary>Trigger land feedback with intensity based on fall height</summary>
        public void OnLand(float fallIntensity, int materialId, Vector3 position)
        {
            PrepareSurfaceFeedback(_landFeedback, materialId, mat => mat.LandingClips);
            PlayFeedback(_landFeedback, position, fallIntensity * _globalIntensity);
        }

        /// <summary>Trigger slide feedback</summary>
        public void OnSlide(float intensity, int materialId, Vector3 position)
        {
            PrepareSurfaceFeedback(_slideFeedback, materialId, mat => mat.SlideClips);
            PlayFeedback(_slideFeedback, position, intensity * _globalIntensity);
        }

        /// <summary>Trigger wall run feedback</summary>
        public void OnWallRun()
        {
            PlayFeedback(_wallRunFeedback, _globalIntensity * 0.5f);
        }

        // ===== Interaction Events =====

        /// <summary>Trigger pickup feedback</summary>
        public void OnPickup()
        {
            PlayFeedback(_pickupFeedback, _globalIntensity);
        }

        /// <summary>Trigger interact feedback</summary>
        public void OnInteract()
        {
            PlayFeedback(_interactFeedback, _globalIntensity);
        }

        // ===== EPIC 15.9: Enhanced Combat Feedback =====
        
        [Header("Combat Feedback Presets")]
        [Tooltip("Feedback for critical/headshot hits")]
        [SerializeField] private MMF_Player _criticalHitFeedback;
        
        [Tooltip("Feedback for kill confirmation")]
        [SerializeField] private MMF_Player _killConfirmFeedback;
        
        [Tooltip("Feedback when shield breaks")]
        [SerializeField] private MMF_Player _shieldBreakFeedback;
        
        [Tooltip("Feedback for successful parry")]
        [SerializeField] private MMF_Player _parryFeedback;
        
        [Tooltip("Feedback for blocking damage")]
        [SerializeField] private MMF_Player _blockFeedback;
        
        [Tooltip("Looping feedback for low health warning")]
        [SerializeField] private MMF_Player _lowHealthFeedback;
        
        [Tooltip("Feedback for combo milestones")]
        [SerializeField] private MMF_Player _comboMilestoneFeedback;
        
        private bool _lowHealthActive;
        
        /// <summary>EPIC 15.9: Trigger critical hit feedback with damage number</summary>
        public void OnCriticalHit(float damage, Vector3 worldPosition)
        {
            PlayFeedback(_criticalHitFeedback, _globalIntensity);
            
            // Spawn damage number
            DIG.Combat.UI.CombatUIRegistry.DamageNumbers?.ShowDamageNumber(
                damage, worldPosition, DIG.Targeting.Theming.HitType.Critical, DIG.Targeting.Theming.DamageType.Physical);
        }
        
        /// <summary>EPIC 15.9: Trigger kill confirmation feedback</summary>
        public void OnKillConfirm(Vector3 worldPosition, bool isHeadshot = false)
        {
            float intensity = isHeadshot ? 1.5f : 1f;
            PlayFeedback(_killConfirmFeedback, worldPosition, intensity * _globalIntensity);
        }
        
        /// <summary>EPIC 15.9: Trigger shield break feedback</summary>
        public void OnShieldBreak(Vector3 worldPosition)
        {
            PlayFeedback(_shieldBreakFeedback, worldPosition, _globalIntensity);
            DIG.Combat.UI.CombatUIRegistry.FloatingText?.ShowText("SHIELD BROKEN", worldPosition, DIG.Combat.UI.FloatingTextStyle.Warning);
        }
        
        /// <summary>EPIC 15.9: Trigger parry success feedback</summary>
        public void OnParry(Vector3 worldPosition)
        {
            PlayFeedback(_parryFeedback, worldPosition, _globalIntensity);
            DIG.Combat.UI.CombatUIRegistry.FloatingText?.ShowCombatVerb(DIG.Combat.UI.CombatVerb.Parry, worldPosition);
        }
        
        /// <summary>EPIC 15.9: Trigger block feedback with damage number</summary>
        public void OnBlock(Vector3 worldPosition, float blockedDamage)
        {
            PlayFeedback(_blockFeedback, worldPosition, _globalIntensity * 0.5f);
            
            // Show blocked damage via adapter
            if (DIG.Combat.UI.CombatUIRegistry.DamageNumbers is DIG.Combat.UI.Adapters.DamageNumbersProAdapter adapter)
            {
                adapter.ShowBlock(worldPosition, blockedDamage);
            }
        }
        
        /// <summary>EPIC 15.9: Start low health warning loop</summary>
        public void StartLowHealthWarning()
        {
            if (_lowHealthFeedback != null && !_lowHealthActive)
            {
                _lowHealthActive = true;
                _lowHealthFeedback.PlayFeedbacks();
            }
        }
        
        /// <summary>EPIC 15.9: Stop low health warning</summary>
        public void StopLowHealthWarning()
        {
            if (_lowHealthFeedback != null && _lowHealthActive)
            {
                _lowHealthActive = false;
                _lowHealthFeedback.StopFeedbacks();
            }
        }
        
        /// <summary>EPIC 15.9: Trigger combo milestone feedback</summary>
        public void OnComboMilestone(int comboCount, Vector3 worldPosition)
        {
            float intensity = Mathf.Clamp01(comboCount / 50f) * _globalIntensity;
            PlayFeedback(_comboMilestoneFeedback, worldPosition, intensity);
            DIG.Combat.UI.CombatUIRegistry.FloatingText?.ShowText($"COMBO x{comboCount}!", worldPosition, DIG.Combat.UI.FloatingTextStyle.Success);
        }
        
        /// <summary>EPIC 15.9: Show status effect application</summary>
        public void OnStatusEffectApplied(DIG.Combat.UI.StatusEffectType status, Vector3 worldPosition)
        {
            DIG.Combat.UI.CombatUIRegistry.FloatingText?.ShowStatusApplied(status, worldPosition);
        }

        // ===== Settings =====

        /// <summary>Enable or disable haptics globally</summary>
        public void SetHapticsEnabled(bool enabled)
        {
            _hapticsEnabled = enabled;
        }

        /// <summary>Set global intensity multiplier (0-2)</summary>
        public void SetGlobalIntensity(float intensity)
        {
            _globalIntensity = Mathf.Clamp(intensity, 0f, 2f);
        }

        // ===== Internal =====

        private void PlayFeedback(MMF_Player feedback, float intensity)
        {
            if (!_hapticsEnabled || feedback == null) return;
            feedback.PlayFeedbacks(transform.position, intensity);
        }

        private void PlayFeedback(MMF_Player feedback, Vector3 position, float intensity)
        {
            if (!_hapticsEnabled || feedback == null) return;
            feedback.PlayFeedbacks(position, intensity);
        }

        /// <summary>
        /// Looks up the surface material and injects a random clip into the MMF_Sound feedback.
        /// </summary>
        private void PrepareSurfaceFeedback(MMF_Player player, int materialId, System.Func<SurfaceMaterial, List<AudioClip>> clipSelector)
        {
            if (player == null || _surfaceRegistry == null) return;

            // 1. Resolve Material
            var mat = _surfaceRegistry.GetById(materialId);
            if (mat == null) return;

            // 2. Select Clip
            var clips = clipSelector(mat);
            if (clips == null || clips.Count == 0) return;
            var clip = clips[Random.Range(0, clips.Count)];

            // 3. Inject into MMF_Sound
            var soundFeedback = player.GetFeedbackOfType<MMF_Sound>();
            if (soundFeedback != null)
            {
                soundFeedback.Sfx = clip;
                soundFeedback.Active = true;
            }
            
            // 4. Inject into MMF_ParticlesInstantiation (VFX)
            if (mat.VFXPrefab != null)
            {
                var vfxFeedback = player.GetFeedbackOfType<MMF_ParticlesInstantiation>();
                if (vfxFeedback != null)
                {
                    if (mat.VFXPrefab.TryGetComponent<ParticleSystem>(out var ps))
                    {
                        vfxFeedback.ParticlesPrefab = ps;
                        vfxFeedback.Active = true;
                    }
                }
            }
        }

        // ===== Static Accessors for ECS Bridge =====

        public static void TriggerFire() => Instance?.OnFire();
        public static void TriggerDamage(float intensity = 1f) => Instance?.OnDamage(intensity);
        public static void TriggerHeavyHit() => Instance?.OnHeavyHit();
        
        // Updated movement triggers
        public static void TriggerFootstep(int matId, int stance, Vector3 pos) => Instance?.OnFootstep(matId, stance, pos);
        public static void TriggerJump(int matId, float intensity, Vector3 pos) => Instance?.OnJump(matId, intensity, pos);
        public static void TriggerRoll(int matId, float intensity, Vector3 pos) => Instance?.OnRoll(matId, intensity, pos);
        public static void TriggerDive(int matId, float intensity, Vector3 pos) => Instance?.OnDive(matId, intensity, pos);
        public static void TriggerClimbStart(int matId, Vector3 pos) => Instance?.OnClimbStart(matId, pos);

        public static void TriggerLand(float intensity, int matId, Vector3 pos) => Instance?.OnLand(intensity, matId, pos);
        public static void TriggerSlide(float intensity, int matId, Vector3 pos) => Instance?.OnSlide(intensity, matId, pos);

        // Kept for backward compatibility if needed, but redirects to defaults
        public static void TriggerLandLegacy(float intensity = 1f) => Instance?.OnLand(intensity, 0, Instance.transform.position);
        public static void TriggerSlideLegacy() => Instance?.OnSlide(1f, 0, Instance.transform.position);

        public static void TriggerPickup() => Instance?.OnPickup();
        public static void TriggerInteract() => Instance?.OnInteract();
        
        // EPIC 15.9: Static combat accessors
        public static void TriggerCriticalHit(float damage, Vector3 pos) => Instance?.OnCriticalHit(damage, pos);
        public static void TriggerKillConfirm(Vector3 pos, bool headshot = false) => Instance?.OnKillConfirm(pos, headshot);
        public static void TriggerShieldBreak(Vector3 pos) => Instance?.OnShieldBreak(pos);
        public static void TriggerParry(Vector3 pos) => Instance?.OnParry(pos);
        public static void TriggerBlock(Vector3 pos, float blocked) => Instance?.OnBlock(pos, blocked);
        public static void TriggerComboMilestone(int combo, Vector3 pos) => Instance?.OnComboMilestone(combo, pos);
        public static void TriggerStatusEffect(DIG.Combat.UI.StatusEffectType status, Vector3 pos) => Instance?.OnStatusEffectApplied(status, pos);
    }
}
