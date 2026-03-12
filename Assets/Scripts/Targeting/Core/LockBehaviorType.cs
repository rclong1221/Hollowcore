using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting.Core
{
    /// <summary>
    /// Defines how lock-on affects the camera and character.
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════
    /// LOCK-ON PARADIGMS (Primary Modes)
    /// ═══════════════════════════════════════════════════════════════════════════
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ HARD LOCK (Dark Souls, Elden Ring, Monster Hunter)                      │
    /// │ - Camera FORCED to track target                                         │
    /// │ - Player movement becomes strafe-oriented (circle around target)        │
    /// │ - Best for: Melee combat, boss fights, 1v1 duels                        │
    /// │ - Downside: Tunnel vision, bad for multiple enemies                     │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ SOFT LOCK (God of War 2018, Assassin's Creed, Horizon)                  │
    /// │ - Camera stays FREE (player controls it)                                │
    /// │ - Character ROTATION snaps toward target                                │
    /// │ - Optional AIM MAGNETISM (crosshair pulls toward target hitbox)         │
    /// │ - Best for: Mixed combat, multiple enemies, ranged + melee              │
    /// │ - Downside: Less precision for duels                                    │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ ISOMETRIC LOCK (Diablo, LoL, Hades, XCOM)                               │
    /// │ - Camera is FIXED (overhead view, no rotation)                          │
    /// │ - Character FACES target direction                                      │
    /// │ - Often uses click-to-target or auto-target-nearest-to-cursor           │
    /// │ - Best for: AoE awareness, many enemies, tactical gameplay              │
    /// │ - Downside: Less immersive, harder precision aiming                     │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ OVER-THE-SHOULDER (RE4, Gears, TLOU)                                    │
    /// │ - Camera offset to one side of character                                │
    /// │ - Lock-on can SWAP shoulder side for visibility                         │
    /// │ - ADS (aim down sights) brings camera closer/centered                   │
    /// │ - Best for: Cover shooters, stealth, precision aiming                   │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ TWIN-STICK (Helldivers, Enter the Gungeon, Geometry Wars)               │
    /// │ - Move with left stick, AIM with right stick                            │
    /// │ - Lock = sticky aim (aim slows near targets)                            │
    /// │ - Often top-down or isometric camera                                    │
    /// │ - Best for: Fast action, many projectiles, arcade feel                  │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ FIRST PERSON (Halo, Destiny, CoD)                                       │
    /// │ - Camera IS the view (no separate character rotation)                   │
    /// │ - Lock = AIM ASSIST only (magnetism, bullet bending, hitbox expansion)  │
    /// │ - Best for: Immersion, precision shooting                               │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════
    /// LOCK-ON VARIATIONS (Modifiers)
    /// ═══════════════════════════════════════════════════════════════════════════
    /// 
    /// MULTI-LOCK:      Lock 2+ targets (missile salvos, chain lightning)
    /// PART-TARGETING:  Lock body parts (head, weak points, monster parts)
    /// PREDICTIVE:      Lead indicator for moving targets (flight sims)
    /// PRIORITY-SWITCH: Auto-switch to higher threat when target dies
    /// STICKY-AIM:      Aim SLOWS when crosshair over targets
    /// SNAP-AIM:        Quick snap to nearest on ADS press
    /// RANGE-ZONES:     Different lock strength by distance
    /// 
    /// </summary>
    public enum LockBehaviorType : byte
    {
        /// <summary>
        /// No lock behavior - target is tracked but nothing happens automatically.
        /// Used for UI indicators only.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Camera locks onto and tracks target. Player strafes around target.
        /// Movement is relative to target direction.
        /// </summary>
        HardLock = 1,
        
        /// <summary>
        /// Camera stays free. Character rotation and aim assist toward target.
        /// Attack inputs auto-aim toward locked target.
        /// </summary>
        SoftLock = 2,
        
        /// <summary>
        /// Isometric camera mode. Character faces target direction.
        /// No camera movement. Click or proximity based targeting.
        /// </summary>
        IsometricLock = 3,
        
        /// <summary>
        /// Over-the-shoulder camera. Offset left/right of character.
        /// Lock can swap shoulder for visibility. Tighter zoom when aiming.
        /// </summary>
        OverTheShoulder = 4,
        
        /// <summary>
        /// Twin-stick aiming. Right stick controls aim direction independently.
        /// Lock = sticky aim (aim movement slows near valid targets).
        /// </summary>
        TwinStick = 5,
        
        /// <summary>
        /// First person mode. Camera IS the view.
        /// Lock = aim magnetism only (no character rotation concept).
        /// </summary>
        FirstPerson = 6
    }
    
    /// <summary>
    /// Lock variations that can be combined with any behavior type.
    /// </summary>
    [System.Flags]
    public enum LockFeatureFlags : byte
    {
        None = 0,
        
        /// <summary>
        /// Allow locking multiple targets simultaneously.
        /// </summary>
        MultiLock = 1 << 0,
        
        /// <summary>
        /// Allow targeting specific body parts / weak points.
        /// </summary>
        PartTargeting = 1 << 1,
        
        /// <summary>
        /// Show lead indicator for moving targets (predictive aim).
        /// </summary>
        PredictiveAim = 1 << 2,
        
        /// <summary>
        /// Auto-switch to higher priority target when current dies.
        /// </summary>
        PriorityAutoSwitch = 1 << 3,
        
        /// <summary>
        /// Aim movement slows when over valid targets.
        /// </summary>
        StickyAim = 1 << 4,
        
        /// <summary>
        /// Quick snap to nearest target on ADS/lock button.
        /// </summary>
        SnapAim = 1 << 5
    }
    
    /// <summary>
    /// How lock input is interpreted.
    /// </summary>
    public enum LockInputMode : byte
    {
        /// <summary>
        /// Press to lock, press again to unlock.
        /// </summary>
        Toggle = 0,
        
        /// <summary>
        /// Hold to lock, release to unlock.
        /// </summary>
        Hold = 1,
        
        /// <summary>
        /// Click directly on target (mouse/touch).
        /// </summary>
        ClickTarget = 2,
        
        /// <summary>
        /// Always targets nearest valid target (no input needed).
        /// </summary>
        AutoNearest = 3,
        
        /// <summary>
        /// Target is whatever cursor/crosshair is hovering over.
        /// </summary>
        HoverTarget = 4
    }
    
    /// <summary>
    /// Which system handles lock-on input processing.
    /// Allows user preference between two implementations.
    /// </summary>
    public enum LockInputHandler : byte
    {
        /// <summary>
        /// CameraLockOnSystem handles all lock input (default).
        /// Runs in PredictedFixedStepSimulationSystemGroup on ClientWorld only.
        /// Full integration with camera/movement, soft lock break detection.
        /// </summary>
        CameraLockOnSystem = 0,
        
        /// <summary>
        /// LockInputModeSystem handles lock input.
        /// Runs in SimulationSystemGroup on both client and server.
        /// Supports Hold, AutoNearest, HoverTarget modes.
        /// </summary>
        LockInputModeSystem = 1
    }
    
    /// <summary>
    /// Lock Phase State Machine - shared by ALL lock modes.
    /// Camera/Body signals arrival for Locking → Locked transition.
    /// Break detection is ONLY enabled in Locked phase.
    /// </summary>
    public enum LockPhase : byte
    {
        /// <summary>
        /// No target locked. Free camera, normal movement.
        /// </summary>
        Unlocked = 0,
        
        /// <summary>
        /// Target acquired, camera/body en route to target.
        /// Break detection DISABLED - allows camera to settle.
        /// </summary>
        Locking = 1,
        
        /// <summary>
        /// Camera/body has arrived at target (within threshold).
        /// Break detection ENABLED - mouse/input can break lock.
        /// </summary>
        Locked = 2
    }
    
    /// <summary>
    /// Singleton component defining current lock behavior for all players.
    /// Changes based on camera mode (1st person, 3rd person, isometric).
    /// </summary>
    public struct ActiveLockBehavior : IComponentData
    {
        public LockBehaviorType BehaviorType;
        public LockFeatureFlags Features;
        public LockInputMode InputMode;
        
        /// <summary>
        /// Which system processes lock input. Default: CameraLockOnSystem.
        /// CameraLockOnSystem is recommended for full soft lock support.
        /// </summary>
        public LockInputHandler InputHandler;
        
        /// <summary>
        /// For SoftLock: How much the character rotates toward target (0-1).
        /// 0 = no rotation, 1 = instant snap, 0.1 = gentle turn
        /// </summary>
        public float CharacterRotationStrength;
        
        /// <summary>
        /// For SoftLock/FirstPerson: How much crosshair/aim pulls toward target (0-1).
        /// 0 = no magnetism, 1 = full snap, 0.2 = subtle pull
        /// </summary>
        public float AimMagnetismStrength;
        
        /// <summary>
        /// For StickyAim: How much aim movement slows near targets (0-1).
        /// 0 = no slowdown, 1 = full stop, 0.5 = half speed
        /// </summary>
        public float StickyAimStrength;
        
        /// <summary>
        /// For HardLock: How fast camera tracks target (degrees/second).
        /// Higher = snappier, lower = smoother
        /// </summary>
        public float CameraTrackingSpeed;
        
        /// <summary>
        /// For IsometricLock: Maximum angle character can rotate per frame.
        /// </summary>
        public float MaxCharacterRotationSpeed;
        
        /// <summary>
        /// For OverTheShoulder: Which side camera is on (-1 = left, 1 = right).
        /// </summary>
        public float ShoulderSide;
        
        /// <summary>
        /// For MultiLock: Maximum simultaneous locked targets.
        /// </summary>
        public int MaxLockedTargets;
        
        // ═══════════════════════════════════════════════════════════════════
        // RANGE & DETECTION SETTINGS (Data-Driven - EPIC 15.16 Optimization)
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Maximum distance to acquire or maintain a lock (meters).
        /// Default: 30m. Replaces hardcoded values across targeting systems.
        /// </summary>
        public float MaxLockRange;
        
        /// <summary>
        /// Maximum angle from crosshair/look direction to acquire lock (degrees).
        /// Default: 30°. Wider = easier to acquire, narrower = more precision needed.
        /// </summary>
        public float MaxLockAngle;
        
        /// <summary>
        /// Default height offset for lock point (meters above entity origin).
        /// Default: 1.5m (chest height). Can be overridden per-entity via LockOnTarget.
        /// </summary>
        public float DefaultHeightOffset;
        
        /// <summary>
        /// Position matching tolerance for cross-world entity lookup (meters).
        /// Used when matching client target to server entity. Default: 2m.
        /// </summary>
        public float PositionMatchTolerance;
        
        // ═══════════════════════════════════════════════════════════════════
        // FACTORY METHODS
        // ═══════════════════════════════════════════════════════════════════
        
        public static ActiveLockBehavior HardLock() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.HardLock,
            InputMode = LockInputMode.Toggle,
            Features = LockFeatureFlags.PriorityAutoSwitch,
            CameraTrackingSpeed = 720f,
            CharacterRotationStrength = 0f,
            AimMagnetismStrength = 0f,
            MaxLockedTargets = 1,
            // Range/Detection defaults
            MaxLockRange = 30f,
            MaxLockAngle = 30f,
            DefaultHeightOffset = 1.5f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior SoftLock() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.SoftLock,
            InputMode = LockInputMode.Toggle,
            Features = LockFeatureFlags.StickyAim | LockFeatureFlags.PriorityAutoSwitch,
            CharacterRotationStrength = 0.15f,
            AimMagnetismStrength = 0.3f,
            StickyAimStrength = 0.4f,
            CameraTrackingSpeed = 0f,
            MaxLockedTargets = 1,
            MaxLockRange = 30f,
            MaxLockAngle = 45f, // Wider for soft lock
            DefaultHeightOffset = 1.5f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior IsometricLock() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.IsometricLock,
            InputMode = LockInputMode.ClickTarget,
            Features = LockFeatureFlags.PriorityAutoSwitch,
            CharacterRotationStrength = 0.25f,
            MaxCharacterRotationSpeed = 360f,
            AimMagnetismStrength = 0f,
            CameraTrackingSpeed = 0f,
            MaxLockedTargets = 1,
            MaxLockRange = 40f, // Wider for isometric view
            MaxLockAngle = 180f, // Click anywhere
            DefaultHeightOffset = 1.0f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior OverTheShoulder() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.OverTheShoulder,
            InputMode = LockInputMode.Hold, // ADS to lock
            Features = LockFeatureFlags.StickyAim | LockFeatureFlags.SnapAim,
            CharacterRotationStrength = 0.2f,
            AimMagnetismStrength = 0.25f,
            StickyAimStrength = 0.5f,
            ShoulderSide = 1f, // Start on right
            MaxLockedTargets = 1,
            MaxLockRange = 25f, // Closer for shooter
            MaxLockAngle = 20f, // Tighter for precision
            DefaultHeightOffset = 1.5f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior TwinStick() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.TwinStick,
            InputMode = LockInputMode.AutoNearest, // Always targets nearest in aim dir
            Features = LockFeatureFlags.StickyAim,
            StickyAimStrength = 0.6f,
            AimMagnetismStrength = 0.15f,
            MaxLockedTargets = 1,
            MaxLockRange = 20f,
            MaxLockAngle = 60f, // Wide for arcade
            DefaultHeightOffset = 1.0f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior FirstPerson() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.FirstPerson,
            InputMode = LockInputMode.HoverTarget, // Crosshair determines target
            Features = LockFeatureFlags.StickyAim | LockFeatureFlags.SnapAim,
            AimMagnetismStrength = 0.15f, // Subtle for FPS
            StickyAimStrength = 0.3f,
            MaxLockedTargets = 1,
            MaxLockRange = 50f, // Long range for FPS
            MaxLockAngle = 15f, // Tight for precision
            DefaultHeightOffset = 1.5f,
            PositionMatchTolerance = 2f
        };
        
        public static ActiveLockBehavior MechCombat() => new ActiveLockBehavior
        {
            BehaviorType = LockBehaviorType.SoftLock,
            InputMode = LockInputMode.Toggle,
            Features = LockFeatureFlags.MultiLock | LockFeatureFlags.PredictiveAim | LockFeatureFlags.PartTargeting,
            CharacterRotationStrength = 0f, // Mech torso independent
            AimMagnetismStrength = 0f,
            MaxLockedTargets = 6, // Missile salvo
            MaxLockRange = 100f, // Long range for mech
            MaxLockAngle = 90f, // Wide for multi-lock
            DefaultHeightOffset = 2.0f,
            PositionMatchTolerance = 3f
        };
    }
    
    /// <summary>
    /// For MultiLock: Individual locked target in the multi-target buffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LockedTargetElement : IBufferElementData
    {
        public Entity Target;
        public float3 LastPosition;
        public float LockTime;
        
        /// <summary>
        /// For PartTargeting: Which part is locked (0 = center mass).
        /// </summary>
        public int TargetPartIndex;
    }
}
