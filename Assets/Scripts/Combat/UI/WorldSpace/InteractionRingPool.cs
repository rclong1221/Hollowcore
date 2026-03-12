// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · InteractionRingPool
// Pool manager for InteractionProgressRing instances
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Combat.UI.WorldSpace
{
    /// <summary>
    /// EPIC 15.9: Pool manager for InteractionProgressRing instances.
    /// Handles spawning, recycling, and lifecycle management of interaction rings.
    /// </summary>
    public class InteractionRingPool : MonoBehaviour, IInteractionRingProvider
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Pool Settings")]
        [SerializeField] private InteractionProgressRing _ringPrefab;
        [SerializeField] private int _initialPoolSize = 10;
        [SerializeField] private bool _expandPool = true;
        
        // ─────────────────────────────────────────────────────────────────
        // Pool State
        // ─────────────────────────────────────────────────────────────────
        private Queue<InteractionProgressRing> _pool = new();
        private List<InteractionProgressRing> _activeRings = new();
        private Dictionary<int, InteractionProgressRing> _targetToRing = new();
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Pre-warm pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                var ring = CreateRing();
                ring.ResetForPool();
                _pool.Enqueue(ring);
            }
        }
        
        private void OnEnable()
        {
            CombatUIRegistry.RegisterInteractionRings(this);
        }
        
        private void OnDisable()
        {
            CombatUIRegistry.UnregisterInteractionRings(this);
        }
        
        private void Update()
        {
            // Clean up inactive rings
            for (int i = _activeRings.Count - 1; i >= 0; i--)
            {
                var ring = _activeRings[i];
                if (!ring.IsActive)
                {
                    ReturnToPool(ring);
                    _activeRings.RemoveAt(i);
                }
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // IInteractionRingProvider Implementation
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>Show interaction ring for a target with optional label</summary>
        public InteractionProgressRing ShowRing(Transform target, Vector3 offset, string label = null)
        {
            if (target == null) return null;
            
            int targetId = target.GetInstanceID();
            
            // Reuse existing ring if one exists for this target
            if (_targetToRing.TryGetValue(targetId, out var existingRing))
            {
                existingRing.Show(target, offset, label);
                return existingRing;
            }
            
            // Get from pool
            var ring = GetFromPool();
            ring.Show(target, offset, label);
            
            _activeRings.Add(ring);
            _targetToRing[targetId] = ring;
            
            return ring;
        }
        
        /// <summary>Update progress for a target's ring</summary>
        public void UpdateProgress(Transform target, float progress)
        {
            if (target == null) return;
            
            int targetId = target.GetInstanceID();
            if (_targetToRing.TryGetValue(targetId, out var ring))
            {
                ring.SetProgress(progress);
            }
        }
        
        /// <summary>Complete interaction for a target</summary>
        public void CompleteRing(Transform target)
        {
            if (target == null) return;
            
            int targetId = target.GetInstanceID();
            if (_targetToRing.TryGetValue(targetId, out var ring))
            {
                ring.Complete();
                _targetToRing.Remove(targetId);
            }
        }
        
        /// <summary>Cancel/hide interaction ring for a target</summary>
        public void CancelRing(Transform target)
        {
            if (target == null) return;
            
            int targetId = target.GetInstanceID();
            if (_targetToRing.TryGetValue(targetId, out var ring))
            {
                ring.Cancel();
                _targetToRing.Remove(targetId);
            }
        }
        
        /// <summary>Hide all active rings</summary>
        public void HideAllRings()
        {
            foreach (var ring in _activeRings)
            {
                ring.HideImmediate();
            }
            _activeRings.Clear();
            _targetToRing.Clear();
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Pool Management
        // ─────────────────────────────────────────────────────────────────
        private InteractionProgressRing GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            
            if (_expandPool)
            {
                return CreateRing();
            }
            
            // Recycle oldest active ring
            if (_activeRings.Count > 0)
            {
                var oldest = _activeRings[0];
                _activeRings.RemoveAt(0);
                
                // Clean up target mapping
                foreach (var kvp in _targetToRing)
                {
                    if (kvp.Value == oldest)
                    {
                        _targetToRing.Remove(kvp.Key);
                        break;
                    }
                }
                
                oldest.HideImmediate();
                return oldest;
            }
            
            return CreateRing();
        }
        
        private void ReturnToPool(InteractionProgressRing ring)
        {
            ring.ResetForPool();
            _pool.Enqueue(ring);
        }
        
        private InteractionProgressRing CreateRing()
        {
            if (_ringPrefab == null)
            {
                Debug.LogError("[InteractionRingPool] No prefab assigned!");
                return null;
            }
            
            var ring = Instantiate(_ringPrefab, transform);
            ring.name = $"InteractionRing_{_pool.Count + _activeRings.Count}";
            return ring;
        }
    }
    
    /// <summary>
    /// Interface for interaction ring provider.
    /// Allows decoupling of interaction systems from UI implementation.
    /// </summary>
    public interface IInteractionRingProvider
    {
        InteractionProgressRing ShowRing(Transform target, Vector3 offset, string label = null);
        void UpdateProgress(Transform target, float progress);
        void CompleteRing(Transform target);
        void CancelRing(Transform target);
        void HideAllRings();
    }
}
