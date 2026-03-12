using UnityEngine;
using System;
using Unity.Entities;
using Player.Components;
using DIG.Targeting;
using DIG.Targeting.Components;
using DIG.Combat.UI;
using DIG.Core.Input;

namespace DIG.Combat.UI.WorldSpace
{
    using PlayerVisibilityPreset = HealthBarPlayerSettings.PlayerVisibilityPreset;
    
    /// <summary>
    /// Test component for health bar visibility system.
    /// Place on any GameObject in the scene to test visibility modes via inspector.
    /// </summary>
    public class HealthBarVisibilityTester : MonoBehaviour
    {
        [Header("Tester Settings")]
        [Tooltip("Show debug overlay with current settings")]
        [SerializeField] private bool showDebugOverlay = true;
        
        [Header("Direct Mode Override (bypasses manager)")]
        [Tooltip("Use these settings directly instead of going through the manager")]
        [SerializeField] private bool useDirectOverride;
        
        [SerializeField] private HealthBarVisibilityMode directMode = HealthBarVisibilityMode.WhenDamaged;
        [SerializeField] private HealthBarVisibilityFlags directFlags = HealthBarVisibilityFlags.UseFadeTransitions;
        [SerializeField] private float directFadeTimeout = 3f;
        [SerializeField] private float directProximityRange = 15f;
        [SerializeField] private float directFadeInDuration = 0.2f;
        
        [Header("Current State (Read Only)")]
        [SerializeField] private string currentPresetName = "None";
        [SerializeField] private string currentModeName = "Unknown";
        [SerializeField] private string currentFlagsDesc = "";
        
        // Cache
        private HealthBarSettingsManager _manager;
        private HealthBarVisibilityConfig _directConfig;
        private GUIStyle _overlayStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _combatStyle;
        private GUIStyle _noCombatStyle;
        private Rect _overlayRect;
        
        // Combat state tracking
        private int _entitiesInCombat;
        private float _nearestCombatTimer;
        private float _nearestCombatDropTime;
        private string _debugWorldStatus = "Not queried yet";
        
        // Lock-on state tracking
        private bool _isLockedOn;
        private Entity _lockedTarget;
        private string _lockedTargetName = "None";
        private bool _allowTargetLock;
        
        // EPIC 15.18: Hover and click-select state tracking
        private bool _hasHoverTarget;
        private Entity _hoveredEntity;
        private string _hoveredEntityName = "None";
        private bool _hasClickTarget;
        private Entity _clickTargetEntity;
        private string _clickTargetName = "None";
        private World _cachedClientWorld;
        
        private void Start()
        {
            _manager = HealthBarSettingsManager.Instance;
            
            // Create direct config if needed
            _directConfig = ScriptableObject.CreateInstance<HealthBarVisibilityConfig>();
            UpdateDirectConfig();
            
            // Apply immediately if using direct override
            if (useDirectOverride)
            {
                ApplyDirectConfig();
            }
            
            UpdateDisplayStrings();
        }
        
        private void OnDestroy()
        {
            if (_directConfig != null)
            {
                Destroy(_directConfig);
            }
        }
        
        private void Update()
        {
            if (useDirectOverride)
            {
                UpdateDirectConfig();
                ApplyDirectConfig();
            }
            
            UpdateDisplayStrings();
            UpdateCombatStateInfo();
            UpdateLockOnStateInfo();
            UpdateHoverAndClickTargetInfo(); // EPIC 15.18
        }
        
        // EPIC 15.18: Track hover and click-select state
        private const string DEBUG_TAG = "[HB:HOVER]";
        private float _lastHoverDebugLogTime;
        private const float HOVER_DEBUG_LOG_INTERVAL = 2f;
        
        private void UpdateHoverAndClickTargetInfo()
        {
            bool oldHasHover = _hasHoverTarget;
            bool oldHasClick = _hasClickTarget;
            
            _hasHoverTarget = false;
            _hoveredEntity = Entity.Null;
            _hoveredEntityName = "None";
            _hasClickTarget = false;
            _clickTargetEntity = Entity.Null;
            _clickTargetName = "None";
            
            // Find ClientWorld (NetCode) - re-search if we only have LocalWorld and others exist
            bool needsResearch = _cachedClientWorld == null || !_cachedClientWorld.IsCreated 
                || (_cachedClientWorld.Name == "LocalWorld" && World.All.Count > 1);
            
            if (needsResearch)
            {
                _cachedClientWorld = null;
                foreach (var world in World.All)
                {
                    // Prefer ClientWorld over LocalWorld
                    if (world.IsCreated && world.Name == "ClientWorld")
                    {
                        _cachedClientWorld = world;
                        break;
                    }
                    // Fall back to LocalWorld if no ClientWorld
                    if (world.IsCreated && world.Name == "LocalWorld" && _cachedClientWorld == null)
                    {
                        _cachedClientWorld = world;
                    }
                }
                if (_cachedClientWorld == null)
                    _cachedClientWorld = World.DefaultGameObjectInjectionWorld;
            }
            
            if (_cachedClientWorld == null || !_cachedClientWorld.IsCreated)
            {
                if (Time.time - _lastHoverDebugLogTime > HOVER_DEBUG_LOG_INTERVAL)
                {
                    _lastHoverDebugLogTime = Time.time;
                    Debug.LogWarning($"{DEBUG_TAG} Tester: No client world");
                }
                return;
            }
            
            var em = _cachedClientWorld.EntityManager;
            
            // Query for CursorHoverResult - log entity count for diagnosis
            int hoverQueryCount = 0;
            bool hoverCompDataValid = false;
            HoverCategory hoverCat = HoverCategory.None;
            
            using (var hoverQuery = em.CreateEntityQuery(typeof(CursorHoverResult)))
            {
                hoverQueryCount = hoverQuery.CalculateEntityCount();
                if (hoverQueryCount > 0)
                {
                    var entities = hoverQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var hover = em.GetComponentData<CursorHoverResult>(entities[0]);
                        hoverCompDataValid = hover.IsValid;
                        hoverCat = hover.Category;
                        
                        if (hover.IsValid && hover.HoveredEntity != Entity.Null 
                            && hover.Category != HoverCategory.Ground 
                            && hover.Category != HoverCategory.None)
                        {
                            _hasHoverTarget = true;
                            _hoveredEntity = hover.HoveredEntity;
                            _hoveredEntityName = $"Entity {hover.HoveredEntity.Index} ({hover.Category})";
                        }
                    }
                    entities.Dispose();
                }
            }
            
            // Query for TargetData (click-select)
            int targetQueryCount = 0;
            bool targetHasValid = false;
            
            using (var targetQuery = em.CreateEntityQuery(typeof(TargetData)))
            {
                targetQueryCount = targetQuery.CalculateEntityCount();
                if (targetQueryCount > 0)
                {
                    var entities = targetQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var targetData = em.GetComponentData<TargetData>(entities[0]);
                        targetHasValid = targetData.HasValidTarget;
                        
                        if (targetData.HasValidTarget && targetData.TargetEntity != Entity.Null)
                        {
                            _hasClickTarget = true;
                            _clickTargetEntity = targetData.TargetEntity;
                            _clickTargetName = $"Entity {targetData.TargetEntity.Index}";
                        }
                    }
                    entities.Dispose();
                }
            }
            
            // Log state periodically or on change
            bool stateChanged = (oldHasHover != _hasHoverTarget) || (oldHasClick != _hasClickTarget);
            if (stateChanged || Time.time - _lastHoverDebugLogTime > HOVER_DEBUG_LOG_INTERVAL)
            {
                _lastHoverDebugLogTime = Time.time;
                
                // Extra diagnostic: count all entities with just CursorHoverResult (no GhostOwnerIsLocal filter)
                int rawHoverCount = 0;
                int rawTargetCount = 0;
                using (var rawHoverQ = em.CreateEntityQuery(typeof(CursorHoverResult)))
                    rawHoverCount = rawHoverQ.CalculateEntityCount();
                using (var rawTargetQ = em.CreateEntityQuery(typeof(TargetData)))
                    rawTargetCount = rawTargetQ.CalculateEntityCount();
                
                // Also check InputSchemeManager state
                var scheme = InputSchemeManager.Instance;
                string schemeInfo = scheme != null 
                    ? $"scheme={scheme.ActiveScheme}, cursorFree={scheme.IsCursorFree}, tempCursor={scheme.IsTemporaryCursorActive}" 
                    : "NO_SCHEME_MGR";
                
                Debug.Log($"{DEBUG_TAG} Tester: world={_cachedClientWorld.Name}, rawHover={rawHoverCount}, rawTarget={rawTargetCount}, {schemeInfo} | hasHover={_hasHoverTarget}, hasClick={_hasClickTarget}");
            }
        }
        
        private void UpdateLockOnStateInfo()
        {
            _allowTargetLock = TargetLockSettingsManager.Instance.AllowTargetLock;
            _isLockedOn = false;
            _lockedTarget = Entity.Null;
            _lockedTargetName = "None";
            
            // Query client world for CameraTargetLockState
            var clientWorld = World.DefaultGameObjectInjectionWorld;
            if (clientWorld == null || !clientWorld.IsCreated) return;
            
            var em = clientWorld.EntityManager;
            
            // Find player entity with CameraTargetLockState
            using (var query = em.CreateEntityQuery(typeof(CameraTargetLockState)))
            {
                if (query.CalculateEntityCount() > 0)
                {
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var lockState = em.GetComponentData<CameraTargetLockState>(entities[0]);
                        _isLockedOn = lockState.IsLocked;
                        _lockedTarget = lockState.TargetEntity;
                        
                        if (_isLockedOn && _lockedTarget != Entity.Null && em.Exists(_lockedTarget))
                        {
                            _lockedTargetName = $"Entity {_lockedTarget.Index}";
                        }
                    }
                    entities.Dispose();
                }
            }
        }
        
        private void UpdateCombatStateInfo()
        {
            var pool = EnemyHealthBarPool.Instance;
            if (pool == null)
            {
                _debugWorldStatus = "No EnemyHealthBarPool";
                _entitiesInCombat = 0;
                return;
            }
            
            _entitiesInCombat = pool.EntitiesInCombatCount;
            _nearestCombatTimer = pool.NearestCombatExitTimer;
            _nearestCombatDropTime = pool.NearestCombatDropTime;
            
            int total = pool.TotalEntitiesWithCombatState;
            _debugWorldStatus = $"{total} entities tracked this frame";
        }
        
        private void ApplyPreset(PlayerVisibilityPreset preset)
        {
            if (_manager == null) return;
            
            _manager.ApplyPreset(preset);
            currentPresetName = preset.ToString();
            Debug.Log($"[HealthBarTester] Applied preset: {preset}");
        }
        
        private void UpdateDirectConfig()
        {
            if (_directConfig == null) return;
            
            _directConfig.primaryMode = directMode;
            _directConfig.flags = directFlags;
            _directConfig.hideAfterSeconds = directFadeTimeout;
            _directConfig.proximityDistance = directProximityRange;
            _directConfig.fadeInDuration = directFadeInDuration;
        }
        
        private void ApplyDirectConfig()
        {
            // In a real implementation, you'd push this to the pool manager
            // For now, we just update the manager's config
            if (_manager != null && useDirectOverride)
            {
                _manager.SetMode(directMode);
                _manager.SetFadeTimeout(directFadeTimeout);
                _manager.SetProximityRange(directProximityRange);
                
                // Apply flags
                _manager.SetUseFadeTransitions((directFlags & HealthBarVisibilityFlags.UseFadeTransitions) != 0);
                _manager.SetShowName((directFlags & HealthBarVisibilityFlags.ShowName) != 0);
                _manager.SetShowLevel((directFlags & HealthBarVisibilityFlags.ShowLevel) != 0);
            }
        }
        
        private void UpdateDisplayStrings()
        {
            if (_manager != null && !useDirectOverride)
            {
                var config = _manager.ActiveConfig;
                if (config != null)
                {
                    currentModeName = config.primaryMode.ToString();
                    currentFlagsDesc = GetFlagsDescription(config.flags);
                }
            }
            else if (useDirectOverride)
            {
                currentModeName = directMode.ToString();
                currentFlagsDesc = GetFlagsDescription(directFlags);
            }
        }
        
        private string GetFlagsDescription(HealthBarVisibilityFlags flags)
        {
            if (flags == HealthBarVisibilityFlags.None) return "None";
            
            var parts = new System.Collections.Generic.List<string>();
            
            if ((flags & HealthBarVisibilityFlags.UseFadeTransitions) != 0) parts.Add("Fade");
            if ((flags & HealthBarVisibilityFlags.UseShowDelay) != 0) parts.Add("Delay");
            if ((flags & HealthBarVisibilityFlags.HideAtFullHealth) != 0) parts.Add("HideFull");
            if ((flags & HealthBarVisibilityFlags.RequireDiscovered) != 0) parts.Add("Discover");
            if ((flags & HealthBarVisibilityFlags.BossesOnly) != 0) parts.Add("BossOnly");
            if ((flags & HealthBarVisibilityFlags.ElitesOnly) != 0) parts.Add("EliteOnly");
            if ((flags & HealthBarVisibilityFlags.NamedOnly) != 0) parts.Add("Named");
            if ((flags & HealthBarVisibilityFlags.HostileOnly) != 0) parts.Add("Hostile");
            if ((flags & HealthBarVisibilityFlags.IncludeFriendlies) != 0) parts.Add("Friendly");
            if ((flags & HealthBarVisibilityFlags.IncludeNeutrals) != 0) parts.Add("Neutral");
            if ((flags & HealthBarVisibilityFlags.RequireScanned) != 0) parts.Add("Scanned");
            if ((flags & HealthBarVisibilityFlags.RequireSkillUnlock) != 0) parts.Add("Skill");
            if ((flags & HealthBarVisibilityFlags.ShowLevel) != 0) parts.Add("Level");
            if ((flags & HealthBarVisibilityFlags.ShowName) != 0) parts.Add("Name");
            if ((flags & HealthBarVisibilityFlags.ShowStatusEffects) != 0) parts.Add("Status");
            if ((flags & HealthBarVisibilityFlags.ColorByThreatLevel) != 0) parts.Add("Threat");
            if ((flags & HealthBarVisibilityFlags.ScaleByImportance) != 0) parts.Add("Scale");
            if ((flags & HealthBarVisibilityFlags.ShowPlayerHealthBar) != 0) parts.Add("Player");
            if ((flags & HealthBarVisibilityFlags.ShowPartyHealthBars) != 0) parts.Add("Party");
            
            return string.Join(", ", parts);
        }
        
        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            
            InitStyles();
            
            float width = 320;
            float height = 520; // Increased for hover and click-select sections
            float x = Screen.width - width - 10;
            float y = 10;
            
            _overlayRect = new Rect(x, y, width, height);
            
            GUI.Box(_overlayRect, "");
            
            GUILayout.BeginArea(_overlayRect);
            GUILayout.Space(5);
            
            GUILayout.Label("Health Bar Visibility Tester", _headerStyle);
            GUILayout.Space(5);
            
            GUILayout.Label($"Mode: {currentModeName}", _overlayStyle);
            GUILayout.Label($"Preset: {currentPresetName}", _overlayStyle);
            GUILayout.Label($"Flags: {currentFlagsDesc}", _overlayStyle);
            
            if (_manager != null && !useDirectOverride)
            {
                GUILayout.Label($"Fade Timeout: {_manager.FadeTimeout:F1}s", _overlayStyle);
                GUILayout.Label($"Proximity Range: {_manager.ProximityRange:F0}m", _overlayStyle);
            }
            else if (useDirectOverride)
            {
                GUILayout.Label($"Fade Timeout: {directFadeTimeout:F1}s", _overlayStyle);
                GUILayout.Label($"Proximity Range: {directProximityRange:F0}m", _overlayStyle);
            }
            
            GUILayout.Space(5);
            GUILayout.Label($"Direct Override: {(useDirectOverride ? "ON" : "OFF")}", _overlayStyle);
            
            // Combat State Section
            GUILayout.Space(10);
            GUILayout.Label("Combat State", _headerStyle);
            GUILayout.Label(_debugWorldStatus, _overlayStyle);
            
            if (_entitiesInCombat > 0)
            {
                GUILayout.Label($"Entities In Combat: {_entitiesInCombat}", _combatStyle);
                GUILayout.Label($"Nearest Exit In: {_nearestCombatTimer:F1}s", _combatStyle);
                GUILayout.Label($"Exit Condition: No attacks for {_nearestCombatDropTime:F0}s", _overlayStyle);
            }
            else
            {
                GUILayout.Label("No entities in combat", _noCombatStyle);
                GUILayout.Label("Enter: Deal or receive damage", _overlayStyle);
                GUILayout.Label("Exit: No attacks for CombatDropTime", _overlayStyle);
            }
            
            // Lock-On Section
            GUILayout.Space(10);
            GUILayout.Label("Target Lock", _headerStyle);
            GUILayout.Label($"Allow Lock: {(_allowTargetLock ? "ON" : "OFF")}", _allowTargetLock ? _combatStyle : _noCombatStyle);
            
            if (_isLockedOn)
            {
                GUILayout.Label($"LOCKED ON: {_lockedTargetName}", _combatStyle);
            }
            else
            {
                GUILayout.Label("Not locked on", _noCombatStyle);
                GUILayout.Label("Press Grab input to lock", _overlayStyle);
            }
            
            // EPIC 15.18: Hover State Section
            GUILayout.Space(10);
            GUILayout.Label("Cursor Hover", _headerStyle);
            if (_hasHoverTarget)
            {
                GUILayout.Label($"HOVERED: {_hoveredEntityName}", _combatStyle);
            }
            else
            {
                GUILayout.Label("No hover target", _noCombatStyle);
            }
            
            // EPIC 15.18: Click-Select Section
            GUILayout.Space(5);
            GUILayout.Label("Click-Select", _headerStyle);
            if (_hasClickTarget)
            {
                GUILayout.Label($"SELECTED: {_clickTargetName}", _combatStyle);
            }
            else
            {
                GUILayout.Label("No selection", _noCombatStyle);
            }
            
            GUILayout.EndArea();
        }
        
        private void InitStyles()
        {
            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
            }
            
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };
            }
            
            if (_combatStyle == null)
            {
                _combatStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.3f, 0.3f) } // Red for in combat
                };
            }
            
            if (_noCombatStyle == null)
            {
                _noCombatStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.3f, 1f, 0.3f) } // Green for out of combat
                };
            }
        }
        
#if UNITY_EDITOR
        [ContextMenu("Apply Never Preset")]
        private void ApplyNeverPreset() => ApplyPreset(PlayerVisibilityPreset.Never);
        
        [ContextMenu("Apply WhenDamaged Preset")]
        private void ApplyWhenDamagedPreset() => ApplyPreset(PlayerVisibilityPreset.WhenDamaged);
        
        [ContextMenu("Apply WhenDamagedWithFade Preset")]
        private void ApplyWhenDamagedWithFadePreset() => ApplyPreset(PlayerVisibilityPreset.WhenDamagedWithFade);
        
        [ContextMenu("Apply AlwaysShow Preset")]
        private void ApplyAlwaysShowPreset() => ApplyPreset(PlayerVisibilityPreset.AlwaysShow);
        
        [ContextMenu("Apply TargetOnly Preset")]
        private void ApplyTargetOnlyPreset() => ApplyPreset(PlayerVisibilityPreset.TargetOnly);
        
        [ContextMenu("Apply NearbyOnly Preset")]
        private void ApplyNearbyOnlyPreset() => ApplyPreset(PlayerVisibilityPreset.NearbyOnly);

        [ContextMenu("Apply LineOfSight Preset")]
        private void ApplyLineOfSightPreset()
        {
            // LOS mode isn't a PlayerVisibilityPreset, so apply via direct mode override
            useDirectOverride = true;
            directMode = HealthBarVisibilityMode.WhenInLineOfSight;
            directFlags = HealthBarVisibilityFlags.UseFadeTransitions | HealthBarVisibilityFlags.HostileOnly;
            UpdateDirectConfig();
            ApplyDirectConfig();
            currentPresetName = "LineOfSight (Direct)";
            Debug.Log("[HealthBarTester] Applied LineOfSight mode via direct override");
        }

        [ContextMenu("Apply WhenHovered Preset")]
        private void ApplyWhenHoveredPreset()
        {
            useDirectOverride = true;
            directMode = HealthBarVisibilityMode.WhenHovered;
            directFlags = HealthBarVisibilityFlags.UseFadeTransitions | HealthBarVisibilityFlags.HostileOnly;
            UpdateDirectConfig();
            ApplyDirectConfig();
            currentPresetName = "WhenHovered (Direct)";
            Debug.Log("[HealthBarTester] Applied WhenHovered mode via direct override");
        }

        [ContextMenu("Apply WhenTargeted Preset")]
        private void ApplyWhenTargetedPreset()
        {
            useDirectOverride = true;
            directMode = HealthBarVisibilityMode.WhenTargeted;
            directFlags = HealthBarVisibilityFlags.UseFadeTransitions | HealthBarVisibilityFlags.HostileOnly;
            UpdateDirectConfig();
            ApplyDirectConfig();
            currentPresetName = "WhenTargeted (Direct)";
            Debug.Log("[HealthBarTester] Applied WhenTargeted mode via direct override");
        }

        [ContextMenu("Log Current Settings")]
        private void LogCurrentSettings()
        {
            Debug.Log($"[HealthBarTester] Current Settings:\n" +
                      $"  Mode: {currentModeName}\n" +
                      $"  Preset: {currentPresetName}\n" +
                      $"  Flags: {currentFlagsDesc}\n" +
                      $"  Direct Override: {useDirectOverride}");
        }
#endif
    }
}
