#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;
using Unity.Collections;

namespace DIG.DemoTools
{
    /// <summary>
    /// Runtime collision debug visualizer using Unity Gizmos.
    /// Displays collision events, contact points, and normals for debugging purposes.
    /// Epic 7.3.2: Leverages Unity Physics BVH visualization capabilities.
    /// </summary>
    public class CollisionDebugVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool _enableVisualization = true;
        [SerializeField] private float _contactPointRadius = 0.1f;
        [SerializeField] private float _normalArrowLength = 0.5f;
        [SerializeField] private float _displayDuration = 0.5f;
        
        [Header("Colors")]
        [SerializeField] private Color _contactPointColor = Color.red;
        [SerializeField] private Color _normalColor = Color.green;
        [SerializeField] private Color _velocityColor = Color.blue;
        [SerializeField] private Color _broadphaseQueryColor = Color.yellow;
        
        [Header("Layer Filtering")]
        [SerializeField] private bool _showPlayerCollisions = true;
        [SerializeField] private bool _showEnvironmentCollisions = true;
        
        // Debug data structures
        private struct CollisionDebugData
        {
            public float3 ContactPoint;
            public float3 Normal;
            public float3 Velocity;
            public float Timestamp;
            public CollisionType Type;
        }
        
        private enum CollisionType
        {
            Player,
            Environment,
            Trigger
        }
        
        private readonly List<CollisionDebugData> _activeCollisions = new List<CollisionDebugData>();
        private readonly object _collisionLock = new object();
        
        // Singleton instance for easy access
        public static CollisionDebugVisualizer Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        private void Update()
        {
            // Clean up expired collision data
            CleanupExpiredCollisions();
        }
        
        /// <summary>
        /// Register a collision event for visualization.
        /// Thread-safe for use from jobs.
        /// </summary>
        public void RegisterCollision(float3 contactPoint, float3 normal, float3 velocity, bool isPlayer)
        {
            if (!_enableVisualization) return;
            
            var data = new CollisionDebugData
            {
                ContactPoint = contactPoint,
                Normal = normal,
                Velocity = velocity,
                Timestamp = Time.time,
                Type = isPlayer ? CollisionType.Player : CollisionType.Environment
            };
            
            lock (_collisionLock)
            {
                _activeCollisions.Add(data);
            }
        }
        
        /// <summary>
        /// Register a trigger enter event for visualization.
        /// </summary>
        public void RegisterTrigger(float3 position)
        {
            if (!_enableVisualization) return;
            
            var data = new CollisionDebugData
            {
                ContactPoint = position,
                Normal = float3.zero,
                Velocity = float3.zero,
                Timestamp = Time.time,
                Type = CollisionType.Trigger
            };
            
            lock (_collisionLock)
            {
                _activeCollisions.Add(data);
            }
        }
        
        /// <summary>
        /// Visualize a broadphase query AABB.
        /// </summary>
        public void VisualizeBroadphaseQuery(float3 center, float3 halfExtents, float duration = -1f)
        {
            if (!_enableVisualization) return;
            
            float actualDuration = duration > 0 ? duration : _displayDuration;
            UnityEngine.Debug.DrawLine(
                (Vector3)(center + new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z)),
                (Vector3)(center + new float3(halfExtents.x, -halfExtents.y, -halfExtents.z)),
                _broadphaseQueryColor, actualDuration);
            UnityEngine.Debug.DrawLine(
                (Vector3)(center + new float3(halfExtents.x, -halfExtents.y, -halfExtents.z)),
                (Vector3)(center + new float3(halfExtents.x, -halfExtents.y, halfExtents.z)),
                _broadphaseQueryColor, actualDuration);
            UnityEngine.Debug.DrawLine(
                (Vector3)(center + new float3(halfExtents.x, -halfExtents.y, halfExtents.z)),
                (Vector3)(center + new float3(-halfExtents.x, -halfExtents.y, halfExtents.z)),
                _broadphaseQueryColor, actualDuration);
            UnityEngine.Debug.DrawLine(
                (Vector3)(center + new float3(-halfExtents.x, -halfExtents.y, halfExtents.z)),
                (Vector3)(center + new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z)),
                _broadphaseQueryColor, actualDuration);
        }
        
        private void CleanupExpiredCollisions()
        {
            float currentTime = Time.time;
            lock (_collisionLock)
            {
                _activeCollisions.RemoveAll(c => currentTime - c.Timestamp > _displayDuration);
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!_enableVisualization || !Application.isPlaying) return;
            
            List<CollisionDebugData> collisionsCopy;
            lock (_collisionLock)
            {
                collisionsCopy = new List<CollisionDebugData>(_activeCollisions);
            }
            
            foreach (var collision in collisionsCopy)
            {
                // Filter by type
                if (collision.Type == CollisionType.Player && !_showPlayerCollisions) continue;
                if (collision.Type == CollisionType.Environment && !_showEnvironmentCollisions) continue;
                
                // Draw contact point
                Gizmos.color = collision.Type == CollisionType.Trigger ? Color.magenta : _contactPointColor;
                Gizmos.DrawSphere((Vector3)collision.ContactPoint, _contactPointRadius);
                
                // Draw normal
                if (math.lengthsq(collision.Normal) > 0.001f)
                {
                    Gizmos.color = _normalColor;
                    Vector3 start = (Vector3)collision.ContactPoint;
                    Vector3 end = start + (Vector3)(collision.Normal * _normalArrowLength);
                    Gizmos.DrawLine(start, end);
                    
                    // Draw arrowhead
                    DrawArrowHead(start, end, 0.1f);
                }
                
                // Draw velocity
                if (math.lengthsq(collision.Velocity) > 0.001f)
                {
                    Gizmos.color = _velocityColor;
                    Vector3 start = (Vector3)collision.ContactPoint;
                    Vector3 end = start + (Vector3)(math.normalize(collision.Velocity) * 0.3f);
                    Gizmos.DrawLine(start, end);
                }
            }
        }
        
        private void DrawArrowHead(Vector3 start, Vector3 end, float size)
        {
            Vector3 direction = (end - start).normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.magnitude < 0.001f)
            {
                right = Vector3.Cross(direction, Vector3.right).normalized;
            }
            
            Vector3 arrowPoint1 = end - direction * size + right * size * 0.5f;
            Vector3 arrowPoint2 = end - direction * size - right * size * 0.5f;
            
            Gizmos.DrawLine(end, arrowPoint1);
            Gizmos.DrawLine(end, arrowPoint2);
        }
        
        /// <summary>
        /// Clear all visualization data.
        /// </summary>
        public void ClearVisualization()
        {
            lock (_collisionLock)
            {
                _activeCollisions.Clear();
            }
        }
        
        /// <summary>
        /// Toggle visualization on/off.
        /// </summary>
        public void SetVisualizationEnabled(bool enabled)
        {
            _enableVisualization = enabled;
            if (!enabled)
            {
                ClearVisualization();
            }
        }
    }
}
#endif
