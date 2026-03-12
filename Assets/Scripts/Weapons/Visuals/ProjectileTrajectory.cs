using System.Collections.Generic;
using UnityEngine;

namespace DIG.Weapons.Visuals
{
    /// <summary>
    /// Standalone trajectory visualizer that mirrors the ECS ProjectileMovement logic.
    /// Does not depend on Opsive.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ProjectileTrajectory : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Max number of points in the trajectory line")]
        public int maxPoints = 50;
        
        [Tooltip("Time step for each simulation point (higher = more accuracy, shorter distance)")]
        public float timeStep = 0.05f;

        [Tooltip("Layer mask for collision detection")]
        public LayerMask collisionMask = ~0;

        [Tooltip("Radius for sphere cast (0 = use Raycast)")]
        public float sphereRadius = 0.1f;

        private LineRenderer _lineRenderer;
        private List<Vector3> _points = new List<Vector3>();

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
        }

        /// <summary>
        /// Simulates and renders a trajectory path matching ECS ProjectileMovement logic.
        /// </summary>
        public void SimulateTrajectory(Vector3 startPos, Vector3 startVelocity, float gravity = 9.81f, float drag = 0.1f)
        {
            _points.Clear();
            _points.Add(startPos);

            Vector3 currentPos = startPos;
            Vector3 currentVel = startVelocity;

            for (int i = 0; i < maxPoints; i++)
            {
                // Mirror ECS logic:
                // 1. Gravity
                currentVel.y -= gravity * timeStep;

                // 2. Drag
                if (drag > 0)
                {
                    float speed = currentVel.magnitude;
                    if (speed > 0)
                    {
                        float dragForce = drag * speed * speed * timeStep;
                        float newSpeed = Mathf.Max(0, speed - dragForce);
                        currentVel = currentVel.normalized * newSpeed;
                    }
                }

                // 3. Move
                Vector3 nextPos = currentPos + currentVel * timeStep;

                // 4. Collision Check
                if (CheckCollision(currentPos, nextPos, out RaycastHit hit))
                {
                    _points.Add(hit.point);
                    break;
                }

                _points.Add(nextPos);
                currentPos = nextPos;
            }

            _lineRenderer.positionCount = _points.Count;
            _lineRenderer.SetPositions(_points.ToArray());
        }

        private bool CheckCollision(Vector3 start, Vector3 end, out RaycastHit hit)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            
            if (sphereRadius > 0)
            {
                return Physics.SphereCast(start, sphereRadius, direction, out hit, distance, collisionMask);
            }
            else
            {
                return Physics.Raycast(start, direction, out hit, distance, collisionMask);
            }
        }

        public void Clear()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }
        }
    }
}
