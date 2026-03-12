using UnityEngine;
using System.Collections.Generic;

namespace DIG.Environment.Platforms
{
    /// <summary>
    /// Moves a platform between a list of child waypoints, replacing Opsive's MovingPlatform logic.
    /// Works with MovingPlatformAuthoring to drive ECS physics.
    /// </summary>
    public class WaypointPlatform : MonoBehaviour
    {
        [Header("Movement")]
        public float Speed = 2f;
        public float WaitTime = 1f;
        public bool Loop = true;
        
        [Header("Waypoints")]
        [Tooltip("Assign specific transforms, or leave empty to use child objects sorted by name.")]
        public List<Transform> Waypoints = new List<Transform>();
        
        private int _currentIndex = 0;
        private float _waitTimer = 0f;
        private bool _isWaiting = false;
        private Vector3 _targetPos;
        private Quaternion _targetRot;

        private void Start()
        {
            // Auto-populate if empty
            if (Waypoints.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    // Ignore visualization meshes if they aren't waypoints (Opsive prefabs usually call them WaypointX)
                    if (child.name.Contains("Waypoint"))
                    {
                        Waypoints.Add(child);
                    }
                }
                // Sort by name to ensure order (Waypoint1, Waypoint2...)
                Waypoints.Sort((a, b) => string.Compare(a.name, b.name));
            }

            if (Waypoints.Count > 0)
            {
                // Detach waypoints so they don't move with the platform
                foreach(var wp in Waypoints)
                {
                    wp.SetParent(transform.parent);
                }
                
                // Snap to first
                transform.position = Waypoints[0].position;
                transform.rotation = Waypoints[0].rotation;
                SetTarget(1);
            }
        }

        private void FixedUpdate()
        {
            if (Waypoints.Count < 2) return;

            if (_isWaiting)
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= WaitTime)
                {
                    _isWaiting = false;
                    NextWaypoint();
                }
                return;
            }

            // Move
            float step = Speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, step);
            
            // Rotate
            // Simple rotation toward target rotation
            float angle = Quaternion.Angle(transform.rotation, _targetRot);
            if (angle > 0.1f)
            {
                 float rotStep = (Speed * 10f) * Time.deltaTime; // Rotate faster than move
                 transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRot, rotStep);
            }

            // Check reached
            if (Vector3.Distance(transform.position, _targetPos) < 0.01f)
            {
                transform.position = _targetPos;
                _isWaiting = true;
                _waitTimer = 0f;
            }
        }

        private void SetTarget(int index)
        {
            _currentIndex = index;
            if (_currentIndex >= Waypoints.Count) _currentIndex = 0;
            if (_currentIndex < 0) _currentIndex = Waypoints.Count - 1;

            _targetPos = Waypoints[_currentIndex].position;
            _targetRot = Waypoints[_currentIndex].rotation;
        }

        private void NextWaypoint()
        {
            int next = _currentIndex + 1;
            if (next >= Waypoints.Count)
            {
                if (Loop) next = 0;
                else
                {
                    // Ping pong? Or stop? For now loop/stop.
                    next = Waypoints.Count - 1;
                }
            }
            SetTarget(next);
        }
        
        private void OnDrawGizmos()
        {
            if (Waypoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < Waypoints.Count - 1; i++)
                {
                    if (Waypoints[i] != null && Waypoints[i+1] != null)
                        Gizmos.DrawLine(Waypoints[i].position, Waypoints[i+1].position);
                }
                if (Loop && Waypoints.Count > 1 && Waypoints[0] != null && Waypoints[Waypoints.Count-1] != null)
                {
                    Gizmos.DrawLine(Waypoints[Waypoints.Count-1].position, Waypoints[0].position);
                }
            }
        }
    }
}
