using UnityEngine;

namespace DIG.Testing
{
    /// <summary>
    /// Simple MonoBehaviour to animate test platforms.
    /// Moves objects back and forth for testing MovingPlatformSystem.
    /// Add to any object with MovingPlatformAuthoring for automatic motion.
    /// </summary>
    public class TestPlatformMover : MonoBehaviour
    {
        public enum MoveType
        {
            Horizontal,
            Vertical,
            Rotating,
            Path
        }
        
        [Header("Motion Type")]
        public MoveType Motion = MoveType.Horizontal;
        
        [Header("Linear Motion")]
        [Tooltip("How far the platform moves from center")]
        public float MoveDistance = 3f;
        
        [Tooltip("How fast the platform moves (cycles per second)")]
        public float MoveSpeed = 0.5f;
        
        [Header("Rotation")]
        [Tooltip("Degrees per second for rotation")]
        public float RotationSpeed = 30f;
        
        [Tooltip("Axis to rotate around")]
        public Vector3 RotationAxis = Vector3.up;
        
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _time;
        
        private void Start()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }
        
        private void Update()
        {
            _time += Time.deltaTime;
            
            switch (Motion)
            {
                case MoveType.Horizontal:
                    // Move left/right on X axis
                    float offsetX = Mathf.Sin(_time * MoveSpeed * Mathf.PI * 2f) * MoveDistance;
                    transform.position = _startPosition + new Vector3(offsetX, 0, 0);
                    break;
                    
                case MoveType.Vertical:
                    // Move up/down on Y axis
                    float offsetY = (Mathf.Sin(_time * MoveSpeed * Mathf.PI * 2f) + 1f) * 0.5f * MoveDistance;
                    transform.position = _startPosition + new Vector3(0, offsetY, 0);
                    break;
                    
                case MoveType.Rotating:
                    // Rotate around specified axis
                    transform.rotation = _startRotation * Quaternion.AngleAxis(_time * RotationSpeed, RotationAxis);
                    break;
                    
                case MoveType.Path:
                    // Figure-8 pattern
                    float px = Mathf.Sin(_time * MoveSpeed * Mathf.PI * 2f) * MoveDistance;
                    float py = Mathf.Sin(_time * MoveSpeed * Mathf.PI * 4f) * MoveDistance * 0.5f;
                    transform.position = _startPosition + new Vector3(px, py, 0);
                    break;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? _startPosition : transform.position;
            
            Gizmos.color = Color.yellow;
            
            switch (Motion)
            {
                case MoveType.Horizontal:
                    Gizmos.DrawLine(center - Vector3.right * MoveDistance, center + Vector3.right * MoveDistance);
                    break;
                    
                case MoveType.Vertical:
                    Gizmos.DrawLine(center, center + Vector3.up * MoveDistance);
                    break;
                    
                case MoveType.Rotating:
                    Gizmos.DrawWireSphere(center, 0.5f);
                    Gizmos.DrawRay(center, RotationAxis.normalized * 2f);
                    break;
                    
                case MoveType.Path:
                    // Draw figure-8
                    for (int i = 0; i < 32; i++)
                    {
                        float t1 = i / 32f * Mathf.PI * 2f;
                        float t2 = (i + 1) / 32f * Mathf.PI * 2f;
                        Vector3 p1 = center + new Vector3(Mathf.Sin(t1) * MoveDistance, Mathf.Sin(t1 * 2f) * MoveDistance * 0.5f, 0);
                        Vector3 p2 = center + new Vector3(Mathf.Sin(t2) * MoveDistance, Mathf.Sin(t2 * 2f) * MoveDistance * 0.5f, 0);
                        Gizmos.DrawLine(p1, p2);
                    }
                    break;
            }
        }
    }
}
