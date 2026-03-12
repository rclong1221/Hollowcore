using UnityEngine;
using Unity.Entities;
using DIG.Targeting.Core;

/// <summary>
/// Debug component to test different targeting configurations at runtime.
/// Add this to any GameObject in your scene to control targeting modes.
/// </summary>
[AddComponentMenu("DIG/Targeting/Targeting Mode Tester")]
public class TargetingModeTester : MonoBehaviour
{
    // STATIC: Directly accessible by CameraLockOnSystem without ECS world sync issues
    public static LockBehaviorType StaticCurrentMode = LockBehaviorType.HardLock;
    public static LockInputMode StaticInputMode = LockInputMode.Hold;
    public static LockInputHandler StaticInputHandler = LockInputHandler.CameraLockOnSystem;
    public static bool StaticModeSet = false;
    
    [Header("Active Mode")]
    public LockBehaviorType CurrentMode = LockBehaviorType.HardLock;
    
    [Header("Feature Flags")]
    public bool MultiLock;
    public bool PartTargeting;
    public bool PredictiveAim;
    public bool PriorityAutoSwitch = true;
    public bool StickyAim;
    public bool SnapAim;
    
    [Header("Input Mode")]
    [Tooltip("Toggle = Press Tab to lock, press again to unlock.\\nHold = Hold Tab to lock, release to unlock.")]
    public LockInputMode InputMode = LockInputMode.Hold;
    
    [Tooltip("Which system handles lock input. CameraLockOnSystem is default and supports Soft Lock break detection.")]
    public LockInputHandler InputHandler = LockInputHandler.CameraLockOnSystem;
    
    [Header("Parameters")]
    [Range(0f, 1f)] public float CharacterRotationStrength = 0.15f;
    [Range(0f, 1f)] public float AimMagnetismStrength = 0.3f;
    [Range(0f, 1f)] public float StickyAimStrength = 0.4f;
    public float CameraTrackingSpeed = 720f;
    [Range(1, 8)] public int MaxLockedTargets = 6;
    [Range(-1f, 1f)] public float ShoulderSide = 1f;
    
    [Header("Range & Detection (EPIC 15.16)")]
    [Tooltip("Maximum distance to acquire or maintain lock (meters).")]
    [Range(5f, 100f)] public float MaxLockRange = 30f;
    
    [Tooltip("Maximum angle from crosshair to acquire lock (degrees). Higher = easier to lock on.")]
    [Range(5f, 180f)] public float MaxLockAngle = 60f;
    
    [Tooltip("Default height offset for lock point above entity origin (meters).")]
    [Range(0f, 5f)] public float DefaultHeightOffset = 1.5f;
    
    [Tooltip("Position matching tolerance for cross-world entity lookup (meters).")]
    [Range(0.5f, 5f)] public float PositionMatchTolerance = 2f;
    
    [Header("Debug")]
    public bool ApplyOnChange = true;
    
    private LockBehaviorType _lastMode;
    private LockFeatureFlags _lastFlags;
    
    private void Start()
    {
        // Set static immediately
        StaticCurrentMode = CurrentMode;
        StaticInputMode = InputMode;
        StaticInputHandler = InputHandler;
        StaticModeSet = true;
        
        // Force apply on startup to ensure singleton exists in all worlds
        StartCoroutine(ApplyAfterWorldsCreated());
    }
    
    private System.Collections.IEnumerator ApplyAfterWorldsCreated()
    {
        // Wait for ECS worlds to be created (ClientWorld may take a moment)
        yield return new WaitForSeconds(0.5f);
        ApplyCurrentSettings();
    }
    
    private void Update()
    {
        // Always keep static in sync (CameraLockOnSystem reads this directly)
        StaticCurrentMode = CurrentMode;
        StaticInputMode = InputMode;
        StaticInputHandler = InputHandler;
        StaticModeSet = true;
        
        if (!ApplyOnChange) return;
        
        var newFlags = BuildFlags();
        
        if (CurrentMode != _lastMode || newFlags != _lastFlags)
        {
            _lastMode = CurrentMode;
            _lastFlags = newFlags;
            ApplyCurrentSettings();
        }
    }
    
    private LockFeatureFlags BuildFlags()
    {
        var flags = LockFeatureFlags.None;
        if (MultiLock) flags |= LockFeatureFlags.MultiLock;
        if (PartTargeting) flags |= LockFeatureFlags.PartTargeting;
        if (PredictiveAim) flags |= LockFeatureFlags.PredictiveAim;
        if (PriorityAutoSwitch) flags |= LockFeatureFlags.PriorityAutoSwitch;
        if (StickyAim) flags |= LockFeatureFlags.StickyAim;
        if (SnapAim) flags |= LockFeatureFlags.SnapAim;
        return flags;
    }
    
    [ContextMenu("Apply Settings")]
    public void ApplyCurrentSettings()
    {
        int worldCount = 0;
        
        // Debug: List all worlds
        Debug.Log($"[TargetingModeTester] World.All count: {World.All.Count}");
        foreach (var w in World.All)
        {
            Debug.Log($"[TargetingModeTester] Found world: '{w.Name}' IsCreated={w.IsCreated}");
        }
        
        // Apply to ALL worlds that might have this singleton (Default + ClientWorld)
        foreach (var world in World.All)
        {
            if (!world.IsCreated) continue;
            
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(ActiveLockBehavior));
            
            if (query.IsEmpty)
            {
                // Create the entity AND set data immediately
                var targetEntity = em.CreateEntity();
                em.AddComponentData(targetEntity, BuildBehaviorData());
                worldCount++;
                Debug.Log($"[TargetingModeTester] Created singleton in world: {world.Name}");
            }
            else
            {
                // Update existing entity
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var entity in entities)
                {
                    em.SetComponentData(entity, BuildBehaviorData());
                    Debug.Log($"[TargetingModeTester] Updated singleton in world: {world.Name}");
                }
                entities.Dispose();
                worldCount++;
            }
        }
        
        Debug.Log($"[TargetingModeTester] Applied Mode={CurrentMode} to {worldCount} worlds");
    }
    
    private ActiveLockBehavior BuildBehaviorData()
    {
        return new ActiveLockBehavior
        {
            BehaviorType = CurrentMode,
            Features = BuildFlags(),
            InputMode = InputMode,
            InputHandler = InputHandler,
            CharacterRotationStrength = CharacterRotationStrength,
            AimMagnetismStrength = AimMagnetismStrength,
            StickyAimStrength = StickyAimStrength,
            CameraTrackingSpeed = CameraTrackingSpeed,
            MaxLockedTargets = MaxLockedTargets,
            ShoulderSide = ShoulderSide,
            // New data-driven config fields (EPIC 15.16)
            MaxLockRange = MaxLockRange,
            MaxLockAngle = MaxLockAngle,
            DefaultHeightOffset = DefaultHeightOffset,
            PositionMatchTolerance = PositionMatchTolerance
        };
    }
    
    [ContextMenu("Set Hard Lock Mode")]
    public void SetHardLock()
    {
        CurrentMode = LockBehaviorType.HardLock;
        InputMode = LockInputMode.Toggle;
        PriorityAutoSwitch = true;
        StickyAim = false;
        SnapAim = false;
        ApplyCurrentSettings();
    }
    
    [ContextMenu("Set Soft Lock Mode")]
    public void SetSoftLock()
    {
        CurrentMode = LockBehaviorType.SoftLock;
        InputMode = LockInputMode.Toggle;
        PriorityAutoSwitch = true;
        StickyAim = true;
        SnapAim = false;
        ApplyCurrentSettings();
    }
    
    [ContextMenu("Set Over-the-Shoulder Mode")]
    public void SetOverTheShoulder()
    {
        CurrentMode = LockBehaviorType.OverTheShoulder;
        InputMode = LockInputMode.Hold;
        StickyAim = true;
        SnapAim = true;
        ApplyCurrentSettings();
    }
    
    [ContextMenu("Set First Person Mode")]
    public void SetFirstPerson()
    {
        CurrentMode = LockBehaviorType.FirstPerson;
        InputMode = LockInputMode.HoverTarget;
        StickyAim = true;
        SnapAim = true;
        ApplyCurrentSettings();
    }
    
    [ContextMenu("Set Mech Combat Mode")]
    public void SetMechCombat()
    {
        CurrentMode = LockBehaviorType.SoftLock;
        InputMode = LockInputMode.Toggle;
        MultiLock = true;
        PartTargeting = true;
        PredictiveAim = true;
        PriorityAutoSwitch = true;
        MaxLockedTargets = 6;
        ApplyCurrentSettings();
    }
}
