using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tracks dodge ability state for a player entity.
    /// Set by input detection, read by PlayerAnimationStateSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DodgeState : IComponentData
    {
        /// <summary>True when actively dodging.</summary>
        [GhostField] public bool IsDodging;

        /// <summary>
        /// Direction of dodge (0=Left, 1=Right, 2=Forward, 3=Backward).
        /// Maps to OpsiveAnimatorConstants.DODGE_* values.
        /// </summary>
        [GhostField] public int Direction;

        /// <summary>Time remaining in dodge animation.</summary>
        public float TimeRemaining;

        /// <summary>Cooldown timer before next dodge.</summary>
        public float CooldownRemaining;

        /// <summary>Default dodge duration in seconds.</summary>
        public float DodgeDuration;

        /// <summary>Cooldown between dodges in seconds.</summary>
        public float DodgeCooldown;

        public static DodgeState Default => new DodgeState
        {
            IsDodging = false,
            Direction = 0,
            TimeRemaining = 0f,
            CooldownRemaining = 0f,
            DodgeDuration = 0.5f,
            DodgeCooldown = 0.3f
        };
    }

    /// <summary>
    /// Tracks roll ability state for a player entity.
    /// Set by input detection, read by PlayerAnimationStateSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RollState : IComponentData
    {
        /// <summary>True when actively rolling.</summary>
        [GhostField] public bool IsRolling;

        /// <summary>
        /// Type of roll (0=Left, 1=Right, 2=Forward, 3=Land).
        /// Maps to OpsiveAnimatorConstants.ROLL_* values.
        /// </summary>
        [GhostField] public int RollType;

        /// <summary>Time remaining in roll animation.</summary>
        public float TimeRemaining;

        /// <summary>Cooldown timer before next roll.</summary>
        public float CooldownRemaining;

        /// <summary>Default roll duration in seconds.</summary>
        public float RollDuration;

        /// <summary>Cooldown between rolls in seconds.</summary>
        public float RollCooldown;

        public static RollState Default => new RollState
        {
            IsRolling = false,
            RollType = 0,
            TimeRemaining = 0f,
            CooldownRemaining = 0f,
            RollDuration = 0.8f,
            RollCooldown = 0.2f
        };
    }

    /// <summary>
    /// Tracks vault ability state for a player entity.
    /// Set by obstacle detection, read by PlayerAnimationStateSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct VaultState : IComponentData
    {
        /// <summary>True when actively vaulting.</summary>
        [GhostField] public bool IsVaulting;

        /// <summary>Starting velocity when vault began (for animation speed).</summary>
        [GhostField] public float StartVelocity;

        /// <summary>Height of the vault obstacle (affects animation variant).</summary>
        [GhostField] public float VaultHeight;

        /// <summary>Time remaining in vault animation.</summary>
        public float TimeRemaining;

        /// <summary>Default vault duration in seconds.</summary>
        public float VaultDuration;

        public static VaultState Default => new VaultState
        {
            IsVaulting = false,
            StartVelocity = 0f,
            VaultHeight = 0f,
            TimeRemaining = 0f,
            VaultDuration = 0.6f
        };
    }

    /// <summary>
    /// Tracks crawl ability state for a player entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CrawlState : IComponentData
    {
        /// <summary>True when actively crawling.</summary>
        [GhostField] public bool IsCrawling;

        /// <summary>
        /// Crawl sub-state (0=Active, 1=Stopping).
        /// Maps to OpsiveAnimatorConstants.CRAWL_* values.
        /// </summary>
        [GhostField] public int CrawlSubState;

        public static CrawlState Default => new CrawlState
        {
            IsCrawling = false,
            CrawlSubState = 0
        };
    }

    /// <summary>
    /// Tracks balance ability state (walking on narrow beams).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BalanceState : IComponentData
    {
        /// <summary>True when balancing on narrow surface.</summary>
        [GhostField] public bool IsBalancing;

        /// <summary>Entity of the balance surface (beam, ledge, etc.).</summary>
        public Entity BalanceSurfaceEntity;

        public static BalanceState Default => new BalanceState
        {
            IsBalancing = false,
            BalanceSurfaceEntity = Entity.Null
        };
    }

    /// <summary>
    /// Tracks ledge strafe ability state (shuffling along narrow ledges).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct LedgeStrafeState : IComponentData
    {
        /// <summary>True when strafing along ledge.</summary>
        [GhostField] public bool IsLedgeStrafing;

        /// <summary>Entity of the ledge surface.</summary>
        public Entity LedgeEntity;

        public static LedgeStrafeState Default => new LedgeStrafeState
        {
            IsLedgeStrafing = false,
            LedgeEntity = Entity.Null
        };
    }

    /// <summary>
    /// Animation event queue for agility abilities.
    /// MonoBehaviour animation events write here, ECS systems read to clear ability states.
    /// Uses static event queue pattern (like FreeClimbAnimationEvents).
    /// </summary>
    public struct AgilityAnimationEvents : IComponentData
    {
        /// <summary>Bitmask of completed ability animations.</summary>
        public AgilityEventFlags CompletedEvents;

        /// <summary>Frame number when events were set (for one-frame detection).</summary>
        public int EventFrame;

        public static AgilityAnimationEvents Default => new AgilityAnimationEvents
        {
            CompletedEvents = AgilityEventFlags.None,
            EventFrame = 0
        };

        // Instance methods for setting flags directly on component
        public void SetDodgeComplete() => CompletedEvents |= AgilityEventFlags.DodgeComplete;
        public void SetRollComplete() => CompletedEvents |= AgilityEventFlags.RollComplete;
        public void SetVaultComplete() => CompletedEvents |= AgilityEventFlags.VaultComplete;
        public void SetCrawlComplete() => CompletedEvents |= AgilityEventFlags.CrawlComplete;

        public bool HasDodgeComplete => (CompletedEvents & AgilityEventFlags.DodgeComplete) != 0;
        public bool HasRollComplete => (CompletedEvents & AgilityEventFlags.RollComplete) != 0;
        public bool HasVaultComplete => (CompletedEvents & AgilityEventFlags.VaultComplete) != 0;
        public bool HasCrawlComplete => (CompletedEvents & AgilityEventFlags.CrawlComplete) != 0;

        public void ClearAll() => CompletedEvents = AgilityEventFlags.None;

        // --- Static event queue (thread-safe for MonoBehaviour → ECS bridge) ---
        private static AgilityEventFlags _pendingEvents;
        private static readonly object _lock = new object();

        /// <summary>Queue a dodge complete event from MonoBehaviour animation callback.</summary>
        public static void QueueDodgeComplete()
        {
            lock (_lock) { _pendingEvents |= AgilityEventFlags.DodgeComplete; }
        }

        /// <summary>Queue a roll complete event from MonoBehaviour animation callback.</summary>
        public static void QueueRollComplete()
        {
            lock (_lock) { _pendingEvents |= AgilityEventFlags.RollComplete; }
        }

        /// <summary>Queue a vault complete event from MonoBehaviour animation callback.</summary>
        public static void QueueVaultComplete()
        {
            lock (_lock) { _pendingEvents |= AgilityEventFlags.VaultComplete; }
        }

        /// <summary>Queue a crawl complete event from MonoBehaviour animation callback.</summary>
        public static void QueueCrawlComplete()
        {
            lock (_lock) { _pendingEvents |= AgilityEventFlags.CrawlComplete; }
        }

        /// <summary>Consume and clear all pending events (called by ECS system).</summary>
        public static AgilityEventFlags ConsumeEvents()
        {
            lock (_lock)
            {
                var events = _pendingEvents;
                _pendingEvents = AgilityEventFlags.None;
                return events;
            }
        }

        /// <summary>Check if any events are pending.</summary>
        public static bool HasPendingEvents()
        {
            lock (_lock) { return _pendingEvents != AgilityEventFlags.None; }
        }
    }

    /// <summary>
    /// Flags for agility animation completion events.
    /// </summary>
    [System.Flags]
    public enum AgilityEventFlags
    {
        None = 0,
        DodgeComplete = 1 << 0,
        RollComplete = 1 << 1,
        VaultComplete = 1 << 2,
        CrawlComplete = 1 << 3,
        BalanceComplete = 1 << 4,
        LedgeStrafeComplete = 1 << 5
    }

    /// <summary>
    /// Configuration for agility abilities.
    /// Attached to player prefab to configure ability behavior.
    /// </summary>
    public struct AgilityConfig : IComponentData
    {
        /// <summary>Can the character dodge?</summary>
        public bool CanDodge;

        /// <summary>Can the character roll?</summary>
        public bool CanRoll;

        /// <summary>Can the character vault?</summary>
        public bool CanVault;

        /// <summary>Can the character crawl?</summary>
        public bool CanCrawl;

        /// <summary>Minimum height for vault obstacles.</summary>
        public float MinVaultHeight;

        /// <summary>Maximum height for vault obstacles.</summary>
        public float MaxVaultHeight;

        public static AgilityConfig Default => new AgilityConfig
        {
            CanDodge = true,
            CanRoll = true,
            CanVault = true,
            CanCrawl = true,
            MinVaultHeight = 0.3f,
            MaxVaultHeight = 1.2f
        };
    }

    /// <summary>
    /// Tag component for entities that have agility abilities enabled.
    /// </summary>
    public struct HasAgilityAbilities : IComponentData { }
}
