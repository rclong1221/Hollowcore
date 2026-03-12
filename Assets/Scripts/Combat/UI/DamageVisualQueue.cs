using Unity.Mathematics;
using Unity.Collections;
using DIG.Targeting.Theming;
using DIG.Combat.Systems;
using HitType = DIG.Targeting.Theming.HitType;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Lightweight data struct for cross-world damage visual events.
    /// Carries just enough info for the UI to show damage numbers.
    /// </summary>
    public struct DamageVisualData
    {
        public float Damage;
        public float3 HitPosition;
        public HitType HitType;
        public DamageType DamageType;
        public ResultFlags Flags;
        public bool IsDOT; // EPIC 15.30: Route to ShowDOTTick when true
        public int SourceNetworkId; // Attacker's GhostOwner.NetworkId (-1 = environment/unknown)
    }

    /// <summary>
    /// EPIC 15.30: Resolver data that CRS passes to DamageEventVisualBridgeSystem
    /// for the primary hit on DamagePreApplied=true weapons.
    /// </summary>
    public struct CombatVisualHint
    {
        public HitType HitType;
        public ResultFlags Flags;

        public static readonly CombatVisualHint Default = new()
        {
            HitType = HitType.Hit,
            Flags = ResultFlags.None
        };
    }

    /// <summary>
    /// Static queue bridging server-side damage events to client-side UI.
    /// Uses NativeQueue for Burst-compatible enqueue from DamageEventVisualBridgeSystem's IJobEntity.
    /// All access is main-thread (enqueue from managed OnUpdate or .Run() jobs, dequeue in PresentationSystemGroup).
    ///
    /// Lifecycle: DamageEventVisualBridgeSystem.OnCreate → Initialize, OnDestroy → Dispose.
    /// </summary>
    public static class DamageVisualQueue
    {
        private static NativeQueue<DamageVisualData> _queue;
        private static bool _queueInitialized;

        // ==================== Queue API ====================

        public static void Enqueue(DamageVisualData data)
        {
            if (_queueInitialized)
                _queue.Enqueue(data);
        }

        /// <summary>
        /// Direct access for Burst jobs using .Run() (single-threaded).
        /// Do NOT use from scheduled parallel jobs — use a local NativeQueue instead.
        /// </summary>
        public static NativeQueue<DamageVisualData> NativeQueueDirect => _queue;

        public static bool TryDequeue(out DamageVisualData data)
        {
            if (_queueInitialized && _queue.Count > 0)
                return _queue.TryDequeue(out data);
            data = default;
            return false;
        }

        public static int Count => _queueInitialized ? _queue.Count : 0;

        public static void Clear()
        {
            if (_queueInitialized)
                _queue.Clear();
        }

        // ==================== Combat Hints (NativeHashMap) ====================

        private static NativeHashMap<int, CombatVisualHint> _combatHints;
        private static bool _hintsInitialized;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            if (_queueInitialized && _queue.IsCreated)
                _queue.Dispose();
            _queueInitialized = false;
            if (_hintsInitialized && _combatHints.IsCreated)
                _combatHints.Dispose();
            _hintsInitialized = false;
            if (_pendingRpcsInitialized && _pendingServerRpcs.IsCreated)
                _pendingServerRpcs.Dispose();
            _pendingRpcsInitialized = false;
        }

        /// <summary>
        /// Initialize both NativeQueue and NativeHashMap.
        /// Called by DamageEventVisualBridgeSystem.OnCreate (lifecycle owner).
        /// </summary>
        public static void Initialize()
        {
            if (!_queueInitialized)
            {
                _queue = new NativeQueue<DamageVisualData>(Allocator.Persistent);
                _queueInitialized = true;
            }
            if (!_hintsInitialized)
            {
                _combatHints = new NativeHashMap<int, CombatVisualHint>(32, Allocator.Persistent);
                _hintsInitialized = true;
            }
        }

        /// <summary>Backward compat — calls Initialize().</summary>
        public static void InitializeCombatHints() => Initialize();

        /// <summary>
        /// Dispose all native collections.
        /// Called by DamageEventVisualBridgeSystem.OnDestroy.
        /// </summary>
        public static void Dispose()
        {
            if (_queueInitialized && _queue.IsCreated)
            {
                _queue.Dispose();
                _queueInitialized = false;
            }
            if (_hintsInitialized && _combatHints.IsCreated)
            {
                _combatHints.Dispose();
                _hintsInitialized = false;
            }
        }

        /// <summary>Backward compat — calls Dispose().</summary>
        public static void DisposeCombatHints() => Dispose();

        /// <summary>
        /// Direct access to the NativeHashMap for the Burst-compiled visual bridge job.
        /// </summary>
        public static NativeHashMap<int, CombatVisualHint> CombatHintsNative => _combatHints;

        public static void SetCombatHint(int entityIndex, CombatVisualHint hint)
        {
            if (_hintsInitialized)
                _combatHints[entityIndex] = hint;
        }

        public static bool TryConsumeCombatHint(int entityIndex, out CombatVisualHint hint)
        {
            if (_hintsInitialized && _combatHints.TryGetValue(entityIndex, out hint))
            {
                _combatHints.Remove(entityIndex);
                return true;
            }
            hint = CombatVisualHint.Default;
            return false;
        }

        public static void ClearCombatHints()
        {
            if (_hintsInitialized)
                _combatHints.Clear();
        }

        // ==================== Pending Server RPCs (relay out of PredictedFixedStep) ====================

        private static NativeList<DamageVisualRpc> _pendingServerRpcs;
        private static bool _pendingRpcsInitialized;

        public static void InitializePendingRpcs()
        {
            if (!_pendingRpcsInitialized)
            {
                _pendingServerRpcs = new NativeList<DamageVisualRpc>(16, Allocator.Persistent);
                _pendingRpcsInitialized = true;
            }
        }

        public static void EnqueueServerRpc(DamageVisualRpc rpc)
        {
            if (_pendingRpcsInitialized)
                _pendingServerRpcs.Add(rpc);
        }

        public static NativeList<DamageVisualRpc> PendingServerRpcs =>
            _pendingRpcsInitialized ? _pendingServerRpcs : default;

        public static void ClearPendingRpcs()
        {
            if (_pendingRpcsInitialized)
                _pendingServerRpcs.Clear();
        }

        public static void DisposePendingRpcs()
        {
            if (_pendingRpcsInitialized && _pendingServerRpcs.IsCreated)
            {
                _pendingServerRpcs.Dispose();
                _pendingRpcsInitialized = false;
            }
        }
    }
}
