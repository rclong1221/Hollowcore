using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Aggro.Components;

#pragma warning disable CS0414 // Private fields assigned but read via Inspector
namespace DIG.Aggro.Debug
{
    /// <summary>
    /// EPIC 15.19: Debug tester for the aggro/threat system.
    /// Displays runtime threat table state and provides test actions.
    /// </summary>
    [AddComponentMenu("DIG/Debug/Aggro Debug Tester")]
    public class AggroDebugTester : MonoBehaviour
    {
        [Header("Target Entity (Auto-detected or manual)")]
        [Tooltip("Leave at 0 to auto-detect from this GameObject's entity")]
        public int TargetEntityIndex = 0;
        
        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool _isAggroed;
        [SerializeField] private string _currentTargetName;
        [SerializeField] private float _currentTargetThreat;
        [SerializeField] private int _threatTableSize;
        [SerializeField] private string[] _threatTableEntries;
        
        [Header("Test Actions")]
        [Tooltip("Entity index to add threat for")]
        public int TestThreatSourceIndex = 1;
        [Tooltip("Amount of threat to add")]
        public float TestThreatAmount = 100f;
        
        [Header("Visualization")]
        public bool ShowGizmos = true;
        public Color CurrentTargetColor = Color.red;
        public Color ThreatEntryColor = Color.yellow;
        
        private Entity _entity;
        private EntityManager _entityManager;
        private World _world;
        
        void Start()
        {
            FindEntity();
        }
        
        void Update()
        {
            if (_world == null || !_world.IsCreated)
            {
                FindEntity();
                return;
            }
            
            UpdateRuntimeState();
        }
        
        private void FindEntity()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null) return;
            
            _entityManager = _world.EntityManager;
            
            if (TargetEntityIndex > 0)
            {
                _entity = new Entity { Index = TargetEntityIndex, Version = 1 };
            }
            else
            {
                // Try to find entity from this GameObject
                // This would require an entity reference component
                _entity = Entity.Null;
            }
        }
        
        private void UpdateRuntimeState()
        {
            if (_entity == Entity.Null || !_entityManager.Exists(_entity))
            {
                _isAggroed = false;
                _currentTargetName = "No Entity";
                _currentTargetThreat = 0;
                _threatTableSize = 0;
                return;
            }
            
            if (!_entityManager.HasComponent<AggroState>(_entity))
            {
                _isAggroed = false;
                _currentTargetName = "No AggroState";
                return;
            }
            
            var aggroState = _entityManager.GetComponentData<AggroState>(_entity);
            _isAggroed = aggroState.IsAggroed;
            _currentTargetThreat = aggroState.CurrentLeaderThreat;
            
            if (aggroState.CurrentThreatLeader != Entity.Null)
            {
                _currentTargetName = $"Entity {aggroState.CurrentThreatLeader.Index}";
            }
            else
            {
                _currentTargetName = "None";
            }
            
            // Get threat table
            if (_entityManager.HasBuffer<ThreatEntry>(_entity))
            {
                var buffer = _entityManager.GetBuffer<ThreatEntry>(_entity, true);
                _threatTableSize = buffer.Length;
                
                _threatTableEntries = new string[buffer.Length];
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    string visibility = entry.IsCurrentlyVisible ? "V" : "H";
                    _threatTableEntries[i] = $"[{visibility}] Entity {entry.SourceEntity.Index}: {entry.ThreatValue:F1} threat";
                }
            }
        }
        
        [ContextMenu("Add Test Threat")]
        public void AddTestThreat()
        {
            if (_world == null || !_world.IsCreated) return;
            if (_entity == Entity.Null || !_entityManager.Exists(_entity)) return;
            if (!_entityManager.HasBuffer<ThreatEntry>(_entity)) return;
            
            var buffer = _entityManager.GetBuffer<ThreatEntry>(_entity);
            var testSource = new Entity { Index = TestThreatSourceIndex, Version = 1 };
            
            // Find or add entry
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].SourceEntity == testSource)
                {
                    var entry = buffer[i];
                    entry.ThreatValue += TestThreatAmount;
                    buffer[i] = entry;
                    UnityEngine.Debug.Log($"[AggroDebug] Added {TestThreatAmount} threat to existing entry. New total: {entry.ThreatValue}");
                    return;
                }
            }
            
            // Add new entry
            buffer.Add(new ThreatEntry
            {
                SourceEntity = testSource,
                ThreatValue = TestThreatAmount,
                LastKnownPosition = float3.zero,
                TimeSinceVisible = 0f,
                IsCurrentlyVisible = false
            });
            UnityEngine.Debug.Log($"[AggroDebug] Created new threat entry with {TestThreatAmount} threat");
        }
        
        [ContextMenu("Wipe Threat Table")]
        public void WipeThreatTable()
        {
            if (_world == null || !_world.IsCreated) return;
            if (_entity == Entity.Null || !_entityManager.Exists(_entity)) return;
            if (!_entityManager.HasBuffer<ThreatEntry>(_entity)) return;
            
            var buffer = _entityManager.GetBuffer<ThreatEntry>(_entity);
            buffer.Clear();
            UnityEngine.Debug.Log("[AggroDebug] Threat table cleared");
        }
        
        [ContextMenu("Taunt (+1000 Threat)")]
        public void Taunt()
        {
            TestThreatAmount = 1000f;
            AddTestThreat();
        }
        
        void OnDrawGizmos()
        {
            if (!ShowGizmos || !Application.isPlaying) return;
            if (_world == null || !_world.IsCreated) return;
            if (_entity == Entity.Null || !_entityManager.Exists(_entity)) return;
            
            if (!_entityManager.HasComponent<Unity.Transforms.LocalTransform>(_entity)) return;
            
            var myPos = _entityManager.GetComponentData<Unity.Transforms.LocalTransform>(_entity).Position;
            
            if (!_entityManager.HasBuffer<ThreatEntry>(_entity)) return;
            
            var buffer = _entityManager.GetBuffer<ThreatEntry>(_entity, true);
            
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                float3 targetPos = entry.LastKnownPosition;
                
                // Draw line to threat source
                bool isCurrent = _entityManager.HasComponent<AggroState>(_entity) &&
                    _entityManager.GetComponentData<AggroState>(_entity).CurrentThreatLeader == entry.SourceEntity;
                
                Gizmos.color = isCurrent ? CurrentTargetColor : ThreatEntryColor;
                float lineWidth = math.clamp(entry.ThreatValue / 100f, 0.1f, 1f);
                
                Gizmos.DrawLine((Vector3)myPos, (Vector3)targetPos);
                
                // Draw sphere at target position
                Gizmos.DrawWireSphere((Vector3)targetPos, 0.3f);
            }
        }
    }
}
