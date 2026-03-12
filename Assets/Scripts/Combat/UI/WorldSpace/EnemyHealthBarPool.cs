using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using DIG.Combat.UI;
namespace DIG.Combat.UI.WorldSpace
{
    /// <summary>
    /// EPIC 15.9/15.14: Pool manager for world-space enemy health bars.
    /// Integrates with HealthBarSettingsManager for visibility modes.
    /// </summary>
    public class EnemyHealthBarPool : MonoBehaviour, IEnemyHealthBarProvider
    {
        public static EnemyHealthBarPool Instance { get; private set; }
        
        [Header("Prefab")]
        [Tooltip("World-space health bar prefab")]
        [SerializeField] private EnemyHealthBar healthBarPrefab;
        
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int maxPoolSize = 40;
        
        [Header("Legacy Settings (Used if no SettingsManager)")]
        [Tooltip("Maximum distance from camera to show health bars")]
        [SerializeField] private float maxShowDistance = 50f;
        
        [Tooltip("Time after damage before health bar fades")]
        [SerializeField] private float fadeAfterDamageTime = 3f;
        
        [Tooltip("Always show health bar for targeted entity")]
        [SerializeField] private bool alwaysShowTargeted = true;
        
        [Tooltip("Only show when entity has taken damage")]
        [SerializeField] private bool showOnDamageOnly = true;
        
        [Header("Visibility System")]
        [Tooltip("Enable EPIC 15.14 visibility system integration")]
        [SerializeField] private bool useVisibilitySystem = true;
        
        [Tooltip("Log visibility decisions for debugging")]
        [SerializeField] private bool debugVisibility = false;
        
        [Header("Fade Settings")]
        [SerializeField] private float fadeInSpeed = 4f;
        [SerializeField] private float fadeOutSpeed = 2f;
        
        // Debug logging
        private const string DEBUG_TAG = "[HB:HOVER]";
        private Entity _lastLoggedHovered;
        private Entity _lastLoggedTargeted;
        
        private Queue<EnemyHealthBar> _pool = new();
        private Dictionary<Entity, EnemyHealthBar> _activeByEntity = new();
        private Dictionary<Entity, VisibilityTracker> _trackers = new();
        private Camera _mainCamera;
        private Entity _targetedEntity;
        private Entity _hoveredEntity;
        
        // Cached config reference
        private HealthBarVisibilityConfig _cachedConfig;
        
        // Current per-entity state (passed from bridge each frame)
        private bool _currentIsInCombat;
        private float _currentTimeSinceCombatEnded;
        private bool _currentIsInLineOfSight;
        private bool _currentHasAggroOnPlayer; // EPIC 15.19 // EPIC 15.17
        
        // Combat state summary (updated each frame from bridge calls)
        private int _entitiesInCombatCount;
        private float _nearestCombatExitTimer;
        private float _nearestCombatDropTime;
        private int _totalEntitiesWithCombatState;
        
        /// <summary>Number of entities currently in combat (updated each frame)</summary>
        public int EntitiesInCombatCount => _entitiesInCombatCount;
        /// <summary>Time until nearest entity exits combat</summary>
        public float NearestCombatExitTimer => _nearestCombatExitTimer;
        /// <summary>Combat drop time of the nearest entity</summary>
        public float NearestCombatDropTime => _nearestCombatDropTime;
        /// <summary>Total entities with CombatState component</summary>
        public int TotalEntitiesWithCombatState => _totalEntitiesWithCombatState;
        
        // ═══════════════════════════════════════════════════════════════════
        // CONFIG-DRIVEN PROPERTIES (EPIC 15.16 Optimization)
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Maximum distance squared for health bar culling (from config or default 50m).
        /// Bridge system uses this for distance culling.
        /// </summary>
        public float MaxShowDistanceSq => _cachedConfig != null 
            ? _cachedConfig.maxShowDistance * _cachedConfig.maxShowDistance 
            : 50f * 50f;
        
        /// <summary>
        /// Position match tolerance squared for target lock entity lookup (from config or default 2m).
        /// Bridge system uses this to match client target entity to server entity.
        /// </summary>
        public float PositionMatchToleranceSq => _cachedConfig != null
            ? _cachedConfig.positionMatchTolerance * _cachedConfig.positionMatchTolerance
            : 4f;

        /// <summary>
        /// Whether the current visibility mode requires line-of-sight raycasts.
        /// Bridge system skips expensive per-entity LOS raycasts when this is false.
        /// </summary>
        public bool NeedsLineOfSight => _cachedConfig != null
            && _cachedConfig.primaryMode == HealthBarVisibilityMode.WhenInLineOfSight;
        
        #if UNITY_EDITOR
        private bool _firstShowCall = true;
        #endif
        
        // Tracker for per-entity visibility state (managed side)
        private struct VisibilityTracker
        {
            public double LastDamageTime;
            public float LastHP;
            public bool PlayerDealtDamage;
            public float CurrentAlpha; // Smoothed alpha for fade transitions
            public bool IsNew; // True on first frame, used to snap alpha for instant-show modes
        }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _mainCamera = Camera.main;
            InitializePool();
        }
        
        private void OnEnable()
        {
            CombatUIRegistry.RegisterEnemyHealthBars(this);
            
            // Subscribe to settings changes - always get/create the manager
            var manager = HealthBarSettingsManager.Instance; // This creates the instance if needed
            manager.OnSettingsChanged += OnSettingsChanged;
            _cachedConfig = manager.ActiveConfig;
            
            #if UNITY_EDITOR
            Debug.Log($"[HealthBarPool] OnEnable - Config mode: {(_cachedConfig != null ? _cachedConfig.primaryMode.ToString() : "NULL")}");
            #endif
        }
        
        private void OnDisable()
        {
            CombatUIRegistry.UnregisterEnemyHealthBars(this);
            
            if (HealthBarSettingsManager.HasInstance)
            {
                HealthBarSettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
            }
        }
        
        private void OnSettingsChanged()
        {
            // Always refresh config when settings change
            _cachedConfig = HealthBarSettingsManager.Instance.ActiveConfig;
            
            #if UNITY_EDITOR
            if (debugVisibility)
            {
                Debug.Log($"[HealthBarPool] Settings changed! New mode: {_cachedConfig?.primaryMode}, Flags: {_cachedConfig?.flags}");
            }
            #endif
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        
        private void InitializePool()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogWarning("[EnemyHealthBarPool] No health bar prefab assigned!");
                return;
            }
            
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreatePooledBar();
            }
        }
        
        private EnemyHealthBar CreatePooledBar()
        {
            var bar = Instantiate(healthBarPrefab, transform);
            bar.gameObject.SetActive(false);
            bar.ResetForPool();
            _pool.Enqueue(bar);
            return bar;
        }
        
        /// <summary>
        /// Called by bridge at start of frame to reset combat state counters.
        /// </summary>
        public void BeginFrame()
        {
            _entitiesInCombatCount = 0;
            _totalEntitiesWithCombatState = 0;
            _nearestCombatExitTimer = float.MaxValue;
            _nearestCombatDropTime = 5f;
        }
        
        public void ShowHealthBar(Entity entity, float3 position, float currentHealth, float maxHealth, string name = null,
            bool isInCombat = true, float timeSinceCombatEnded = 0f, bool isInLineOfSight = true, bool hasAggroOnPlayer = false)
        {
            #if UNITY_EDITOR
            // One-time startup diagnostic
            if (_firstShowCall)
            {
                _firstShowCall = false;
                Debug.Log($"[HealthBarPool] First ShowHealthBar call. useVisibilitySystem={useVisibilitySystem}, " +
                          $"_cachedConfig={((_cachedConfig != null) ? _cachedConfig.primaryMode.ToString() : "NULL")}");
            }
            #endif

            // Store per-entity state for use in EvaluateVisibility
            _currentIsInCombat = isInCombat;
            _currentTimeSinceCombatEnded = timeSinceCombatEnded;
            _currentIsInLineOfSight = isInLineOfSight; // EPIC 15.17
            _currentHasAggroOnPlayer = hasAggroOnPlayer; // EPIC 15.19
            
            // Track combat state summary
            _totalEntitiesWithCombatState++;
            if (isInCombat)
            {
                _entitiesInCombatCount++;
            }
            
            // Check prefab is assigned
            if (healthBarPrefab == null)
            {
                #if UNITY_EDITOR
                if (Time.frameCount % 60 == 0)
                    Debug.LogWarning("[EnemyHealthBarPool] No prefab assigned - cannot show health bars!");
                #endif
                return;
            }
            
            // Get or create tracker
            if (!_trackers.TryGetValue(entity, out var tracker))
            {
                tracker = new VisibilityTracker
                {
                    LastDamageTime = -100,
                    LastHP = currentHealth, // Use current health, not max - avoids false "damage" detection
                    PlayerDealtDamage = false,
                    CurrentAlpha = 0f,
                    IsNew = true // Flag to handle first-frame visibility
                };
            }
            
            // Detect damage - only if health DECREASED since last frame
            double currentTime = Time.timeAsDouble;
            if (currentHealth < tracker.LastHP - 0.01f)
            {
                tracker.LastDamageTime = currentTime;
                // TODO: Detect if local player dealt this damage
                tracker.PlayerDealtDamage = true;
            }
            tracker.LastHP = currentHealth;
            
            // Ensure we have a config - refresh if null
            if (useVisibilitySystem && _cachedConfig == null && HealthBarSettingsManager.HasInstance)
            {
                _cachedConfig = HealthBarSettingsManager.Instance.ActiveConfig;
                #if UNITY_EDITOR
                if (debugVisibility)
                {
                    Debug.Log($"[HealthBarPool] Late config fetch: {(_cachedConfig != null ? _cachedConfig.primaryMode.ToString() : "STILL NULL")}");
                }
                #endif
            }
            
            // Use visibility system if enabled
            if (useVisibilitySystem && _cachedConfig != null)
            {
                var result = EvaluateVisibility(entity, position, currentHealth, maxHealth, tracker);
                
                #if UNITY_EDITOR
                // Log first 5 frames for each entity, then every 60 frames
                bool shouldLog = debugVisibility && (tracker.IsNew || Time.frameCount % 60 == 0);
                if (shouldLog)
                {
                    Debug.Log($"[HealthBarPool] Entity {entity.Index}: Mode={_cachedConfig.primaryMode}, " +
                              $"ShouldShow={result.ShouldShow}, ResultAlpha={result.Alpha:F2}, " +
                              $"TrackerAlpha={tracker.CurrentAlpha:F2}, IsNew={tracker.IsNew}, HP={currentHealth}/{maxHealth}");
                }
                #endif
                
                // Smooth alpha interpolation
                float targetAlpha = result.ShouldShow ? result.Alpha : 0f;
                
                // On first frame, snap to target alpha if should show (no fade-in delay for Always mode, etc.)
                if (tracker.IsNew && result.ShouldShow)
                {
                    tracker.CurrentAlpha = targetAlpha;
                    tracker.IsNew = false;
                }
                else
                {
                    float fadeSpeed = targetAlpha > tracker.CurrentAlpha ? fadeInSpeed : fadeOutSpeed;
                    tracker.CurrentAlpha = Mathf.MoveTowards(tracker.CurrentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
                    tracker.IsNew = false;
                }
                
                // Update tracker
                _trackers[entity] = tracker;
                
                // Hide if fully faded out
                if (tracker.CurrentAlpha <= 0.01f && !result.ShouldShow)
                {
                    if (_activeByEntity.ContainsKey(entity))
                    {
                        HideHealthBar(entity);
                    }
                    return;
                }
                
                // Get or create bar
                bool isNewBar = false;
                if (!_activeByEntity.TryGetValue(entity, out var bar))
                {
                    bar = GetFromPool();
                    if (bar == null) return;

                    _activeByEntity[entity] = bar;
                    isNewBar = true;
                }

                // Apply smoothed alpha and scale
                bar.SetExternalAlpha(tracker.CurrentAlpha);
                bar.SetExternalScale(result.Scale);
                bar.UpdateHealth((Vector3)position, currentHealth, maxHealth, name, fadeAfterDamageTime);

                // Enable visibility AFTER UpdateHealth so instant snap triggers
                // while _isVisible is still false (from ResetForPool)
                if (isNewBar)
                    bar.SetUseExternalVisibility(true);
            }
            else
            {
                // Legacy mode
                _trackers[entity] = tracker;
                LegacyShowHealthBar(entity, position, currentHealth, maxHealth, name);
            }
        }
        
        private HealthBarVisibilityResult EvaluateVisibility(
            Entity entity, float3 position, float currentHealth, float maxHealth, VisibilityTracker tracker)
        {
            float distToPlayer = _mainCamera != null 
                ? math.distance((float3)_mainCamera.transform.position, position) 
                : 0f;
            
            double currentTime = Time.timeAsDouble;
            
            var context = new HealthBarVisibilityContext
            {
                Entity = entity,
                Tier = EntityTier.Normal, // TODO: Get from EntityTierComponent
                Relation = FactionRelation.Hostile, // TODO: Get from faction system
                IsNamed = false,
                CurrentHP = currentHealth,
                MaxHP = maxHealth,
                TimeSinceLastDamage = (float)(currentTime - tracker.LastDamageTime),
                TimeSinceCombatEnded = _currentTimeSinceCombatEnded,
                TimeSincePlayerDamage = tracker.PlayerDealtDamage ? (float)(currentTime - tracker.LastDamageTime) : 100f,
                IsInCombat = _currentIsInCombat,
                IsTargeted = entity == _targetedEntity,
                IsHovered = entity == _hoveredEntity,
                HasAggroOnPlayer = _currentHasAggroOnPlayer, // EPIC 15.19: From aggro system
                PlayerDealtDamage = tracker.PlayerDealtDamage,
                IsInLineOfSight = _currentIsInLineOfSight, // EPIC 15.17: Fed from bridge via VisionQueryUtility
                IsDiscovered = true,
                IsScanned = true,
                HasRequiredSkill = true,
                CustomConditionMet = false,
                DistanceToPlayer = distToPlayer,
                CurrentAlpha = tracker.CurrentAlpha // Use tracker's smoothed alpha
            };
            
            return _cachedConfig.Evaluate(in context);
        }
        
        private void LegacyShowHealthBar(Entity entity, float3 position, float currentHealth, float maxHealth, string name)
        {
            // Distance check (skip if too far)
            if (_mainCamera != null)
            {
                float dist = math.distance((float3)_mainCamera.transform.position, position);
                
                // Always show if targeted
                bool isTargeted = entity == _targetedEntity && alwaysShowTargeted;
                
                if (dist > maxShowDistance && !isTargeted)
                {
                    return;
                }
            }
            
            // Check ShowOnDamageOnly
            if (showOnDamageOnly && currentHealth >= maxHealth - 0.01f && !_activeByEntity.ContainsKey(entity))
            {
                return;
            }
            
            // Get or create bar for this entity
            if (!_activeByEntity.TryGetValue(entity, out var bar))
            {
                bar = GetFromPool();
                if (bar == null) return;
                
                _activeByEntity[entity] = bar;
            }
            
            bar.UpdateHealth((Vector3)position, currentHealth, maxHealth, name, fadeAfterDamageTime);
        }
        
        /// <summary>
        /// Show health bar while following a transform.
        /// </summary>
        public void ShowHealthBar(Entity entity, Transform target, float currentHealth, float maxHealth, string name = null)
        {
            if (!_activeByEntity.TryGetValue(entity, out var bar))
            {
                bar = GetFromPool();
                if (bar == null) return;
                
                _activeByEntity[entity] = bar;
            }
            
            bar.UpdateHealth(target, currentHealth, maxHealth, name, fadeAfterDamageTime);
        }
        
        public void HideHealthBar(Entity entity)
        {
            if (_activeByEntity.TryGetValue(entity, out var bar))
            {
                ReturnToPool(bar);
                _activeByEntity.Remove(entity);
            }
            
            // Reset alpha in tracker but keep damage timing data
            if (_trackers.TryGetValue(entity, out var tracker))
            {
                tracker.CurrentAlpha = 0f;
                _trackers[entity] = tracker;
            }
        }
        
        public void HideAll()
        {
            foreach (var kvp in _activeByEntity)
            {
                ReturnToPool(kvp.Value);
            }
            _activeByEntity.Clear();
            _trackers.Clear();
        }
        
        /// <summary>
        /// Set the currently targeted entity (for priority display).
        /// </summary>
        public void SetTargetedEntity(Entity entity)
        {
            if (entity != _lastLoggedTargeted)
            {
                Debug.Log($"{DEBUG_TAG} Pool.SetTargetedEntity: {_targetedEntity.Index} -> {entity.Index}");
                _lastLoggedTargeted = entity;
            }
            _targetedEntity = entity;
        }
        
        /// <summary>
        /// Set the currently hovered entity (for hover mode).
        /// </summary>
        public void SetHoveredEntity(Entity entity)
        {
            if (entity != _lastLoggedHovered)
            {
                Debug.Log($"{DEBUG_TAG} Pool.SetHoveredEntity: {_hoveredEntity.Index} -> {entity.Index}");
                _lastLoggedHovered = entity;
            }
            _hoveredEntity = entity;
        }
        
        /// <summary>
        /// Get active health bar for an entity if it exists.
        /// </summary>
        public EnemyHealthBar GetHealthBar(Entity entity)
        {
            return _activeByEntity.TryGetValue(entity, out var bar) ? bar : null;
        }
        
        private EnemyHealthBar GetFromPool()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();
            
            // Pool exhausted - create new if under max
            if (_activeByEntity.Count < maxPoolSize)
                return CreatePooledBar();
            
            // At max capacity - return null
            return null;
        }
        
        private void ReturnToPool(EnemyHealthBar bar)
        {
            bar.Hide();
            bar.ResetForPool();
            _pool.Enqueue(bar);
        }
        
        /// <summary>
        /// Cleanup dead entities (call periodically or on entity destroy).
        /// </summary>
        public void CleanupDeadEntities(HashSet<Entity> aliveEntities)
        {
            var toRemove = new List<Entity>();
            
            foreach (var entity in _activeByEntity.Keys)
            {
                if (!aliveEntities.Contains(entity))
                    toRemove.Add(entity);
            }
            
            foreach (var entity in toRemove)
            {
                HideHealthBar(entity);
            }
        }
    }
}
