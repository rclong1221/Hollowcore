// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · CombatUIBootstrap
// Central initialization and wiring for all Combat UI systems
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using DIG.Combat.UI.Views;
using DIG.Combat.UI.ViewModels;
using DIG.Targeting.Theming;
using Unity.Entities;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 15.9: Central bootstrap for all Combat UI systems.
    /// Place on a persistent GameObject in your scene.
    /// Initializes ViewModels, binds Views, and registers providers.
    /// </summary>
    public class CombatUIBootstrap : MonoBehaviour
    {
        [Header("Core Views")]
        [SerializeField] private EnhancedHitmarkerView _hitmarkerView;
        [SerializeField] private DirectionalDamageIndicatorView _directionalDamageView;
        
        [Header("Combat Info Views")]
        [SerializeField] private ComboCounterView _comboCounterView;
        [SerializeField] private KillFeedView _killFeedView;
        [SerializeField] private CombatLogView _combatLogView;
        [SerializeField] private StatusEffectBarView _statusEffectView;
        [SerializeField] private BossHealthBarView _bossHealthBarView;
        
        [Header("Auto-Find Settings")]
        [SerializeField] private bool _autoFindViews = true;
        
        // ViewModels (created at runtime)
        private ComboCounterViewModel _comboViewModel;
        private KillFeedViewModel _killFeedViewModel;
        private CombatLogViewModel _combatLogViewModel;
        private StatusEffectBarViewModel _statusEffectViewModel;
        private BossHealthBarViewModel _bossHealthBarViewModel;
        
        // Singleton for global access
        public static CombatUIBootstrap Instance { get; private set; }
        
        // Public accessors for ViewModels
        public ComboCounterViewModel ComboCounter => _comboViewModel;
        public KillFeedViewModel KillFeed => _killFeedViewModel;
        public CombatLogViewModel CombatLog => _combatLogViewModel;
        public StatusEffectBarViewModel StatusEffects => _statusEffectViewModel;
        public BossHealthBarViewModel BossHealthBar => _bossHealthBarViewModel;
        
        // View accessors for external triggering
        public EnhancedHitmarkerView Hitmarker => _hitmarkerView;
        public DirectionalDamageIndicatorView DirectionalDamage => _directionalDamageView;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (_autoFindViews)
            {
                FindViews();
            }
            
            CreateViewModels();
            BindViews();
            RegisterProviders();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            UnregisterProviders();
        }
        
        private void FindViews()
        {
            if (_hitmarkerView == null)
                _hitmarkerView = FindAnyObjectByType<EnhancedHitmarkerView>();
            if (_directionalDamageView == null)
                _directionalDamageView = FindAnyObjectByType<DirectionalDamageIndicatorView>();
            if (_comboCounterView == null)
                _comboCounterView = FindAnyObjectByType<ComboCounterView>();
            if (_killFeedView == null)
                _killFeedView = FindAnyObjectByType<KillFeedView>();
            if (_combatLogView == null)
                _combatLogView = FindAnyObjectByType<CombatLogView>();
            if (_statusEffectView == null)
                _statusEffectView = FindAnyObjectByType<StatusEffectBarView>();
            if (_bossHealthBarView == null)
                _bossHealthBarView = FindAnyObjectByType<BossHealthBarView>();
        }
        
        private void CreateViewModels()
        {
            _comboViewModel = new ComboCounterViewModel();
            _killFeedViewModel = new KillFeedViewModel();
            _combatLogViewModel = new CombatLogViewModel();
            _statusEffectViewModel = new StatusEffectBarViewModel();
            _bossHealthBarViewModel = new BossHealthBarViewModel();
        }
        
        private void BindViews()
        {
            // Bind views to their ViewModels
            _comboCounterView?.Bind(_comboViewModel);
            _killFeedView?.Bind(_killFeedViewModel);
            _combatLogView?.Bind(_combatLogViewModel);
            _statusEffectView?.Bind(_statusEffectViewModel);
            _bossHealthBarView?.Bind(_bossHealthBarViewModel);
        }
        
        private void RegisterProviders()
        {
            // Register ViewModels that implement provider interfaces
            if (_killFeedViewModel != null)
            {
                CombatUIRegistry.RegisterKillFeed(_killFeedViewModel);
            }
            
            if (_combatLogViewModel != null)
            {
                CombatUIRegistry.RegisterCombatLog(_combatLogViewModel);
            }
        }
        
        private void UnregisterProviders()
        {
            CombatUIRegistry.UnregisterKillFeed(_killFeedViewModel);
            CombatUIRegistry.UnregisterCombatLog(_combatLogViewModel);
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Public API for external systems to trigger UI
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Show hitmarker for a successful hit.
        /// Called by CombatUIBridgeSystem.
        /// </summary>
        public void ShowHitmarker(HitType hitType)
        {
            _hitmarkerView?.ShowHit(hitType);
        }
        
        /// <summary>
        /// Show kill confirmation hitmarker.
        /// </summary>
        public void ShowKillmarker(bool isHeadshot = false)
        {
            _hitmarkerView?.ShowKill(isHeadshot);
        }
        
        /// <summary>
        /// Show directional damage indicator.
        /// Called when player takes damage.
        /// </summary>
        public void ShowDirectionalDamage(Vector3 sourceWorldPos, float damage)
        {
            _directionalDamageView?.ShowDamage(sourceWorldPos, damage);
        }
        
        /// <summary>
        /// Register a combo hit. Call when player lands a hit.
        /// </summary>
        public void RegisterComboHit(float comboWindow = 3f)
        {
            _comboViewModel?.RegisterHit(comboWindow);
        }
        
        /// <summary>
        /// Break the combo (player took damage, etc).
        /// </summary>
        public void BreakCombo()
        {
            _comboViewModel?.BreakCombo();
        }
        
        /// <summary>
        /// Add or update a status effect on player.
        /// </summary>
        public void AddStatusEffect(StatusEffectType type, float duration, int stacks = 1)
        {
            _statusEffectViewModel?.AddOrUpdateEffect(type, duration, stacks);
        }
        
        /// <summary>
        /// Remove a status effect from player.
        /// </summary>
        public void RemoveStatusEffect(StatusEffectType type)
        {
            _statusEffectViewModel?.RemoveEffect(type);
        }
        
        /// <summary>
        /// Show boss health bar for an encounter.
        /// </summary>
        public void ShowBossHealthBar(string bossName, int totalPhases = 1)
        {
            if (_bossHealthBarViewModel != null)
            {
                _bossHealthBarViewModel.StartBossFight(bossName, 1f, totalPhases);
            }
        }
        
        /// <summary>
        /// Update boss health during fight.
        /// </summary>
        public void UpdateBossHealth(float healthPercent, float shieldPercent = 0f)
        {
            _bossHealthBarViewModel?.UpdateHealth(healthPercent, shieldPercent);
        }
        
        /// <summary>
        /// Hide boss health bar (boss died or despawned).
        /// </summary>
        public void HideBossHealthBar(bool defeated = true)
        {
            _bossHealthBarViewModel?.EndBossFight();
        }
        
        private void Update()
        {
            // Update combo timer
            _comboViewModel?.UpdateTimer(Time.deltaTime);
            
            // Update status effect durations
            _statusEffectViewModel?.UpdateDurations(Time.deltaTime);
        }
    }
}
