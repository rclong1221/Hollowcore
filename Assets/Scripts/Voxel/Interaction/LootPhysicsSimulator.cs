using UnityEngine;

namespace DIG.Voxel.Interaction
{
    /// <summary>
    /// Manual physics simulation for loot when Unity's built-in physics is disabled
    /// (e.g., when Physics.simulationMode = Script due to DOTS/NetCode).
    /// </summary>
    public class LootPhysicsSimulator : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<LootPhysicsSimulator> ActiveSimulators = new System.Collections.Generic.List<LootPhysicsSimulator>();

        private Vector3 _velocity;
        private Vector3 _angularVelocity;
        private float _drag = 0.8f;
        private float _angularDrag = 0.6f;
        private bool _grounded;
        
        [Tooltip("Mass in kg. Affects resistance to impulses (player pushing) but not gravity.")]
        public float Mass = 5.0f; // Default to 5kg (heavier feel)
        
        private const float GRAVITY = 9.81f;
        private const float BOUNCE_DAMPING = 0.3f;
        
        public bool IsGrounded => _grounded;
        
        public void Initialize(Vector3 velocity, Vector3 angularVelocity, float drag, float angularDrag)
        {
            _velocity = velocity;
            _angularVelocity = angularVelocity;
            _drag = drag;
            _angularDrag = angularDrag;
            _grounded = false;
        }

        private void OnEnable()
        {
            ActiveSimulators.Add(this);
        }

        private void OnDisable()
        {
            ActiveSimulators.Remove(this);
        }
        
        public void ApplyImpulse(Vector3 impulse)
        {
            if (impulse.magnitude > 0.001f)
            {
                _velocity += impulse / Mass; 
                _grounded = false;
                enabled = true;
            }
        }

        public void ManualUpdate(float dt, bool raycastHit, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (dt <= 0) return;
            
            // Apply gravity
            if (!_grounded)
            {
                _velocity.y -= GRAVITY * dt;
            }
            
            // Apply drag
            _velocity *= Mathf.Pow(1f - _drag, dt);
            _angularVelocity *= Mathf.Pow(1f - _angularDrag, dt);
            
            // Move
            Vector3 currentPos = transform.position;
            Vector3 displacement = _velocity * dt;
            Vector3 newPos = currentPos + displacement;
            
            // Collision response
            if (raycastHit)
            {
                // Simply check if we crossed the ground plane defined by hitPoint
                // Assuming simple top-down gravity for now
                // groundY is the point where the object bottom should rest
                // origin is usually center, so we need an offset. 
                // Let's assume origin to bottom is 0.15f (visual approximation for loot)
                float bottomOffset = 0.15f; 
                float groundY = hitPoint.y + bottomOffset;
                
                if (newPos.y < groundY)
                {
                     // Clamped to ground
                     newPos.y = groundY;
                     
                     if (!_grounded)
                     {
                        // Bounce
                        _velocity.y = -_velocity.y * BOUNCE_DAMPING;
                        
                        // Stop bouncing when velocity is low
                        if (Mathf.Abs(_velocity.y) < 1.0f)
                        {
                            _velocity.y = 0;
                            _grounded = true;
                        }
                     }
                     else
                     {
                         // Reset vertical velocity if grounded
                         _velocity.y = 0;
                     }
                     
                    // Apply friction
                    _velocity.x *= 0.9f;
                    _velocity.z *= 0.9f;
                }
                else
                {
                    _grounded = false;
                }
            }
            else
            {
                _grounded = false;
            }
            
            transform.position = newPos;
            
            // Rotate
            if (_angularVelocity.sqrMagnitude > 0.01f)
            {
                transform.Rotate(_angularVelocity * dt * Mathf.Rad2Deg);
            }
            
            // Apply angular drag/stop
            if (_grounded)
            {
                 _angularVelocity *= 0.9f; // Extra damping on ground
            }

            // Stop if stationary
            if (_velocity.sqrMagnitude < 0.01f && _grounded && _angularVelocity.sqrMagnitude < 0.01f)
            {
                _velocity = Vector3.zero;
                _angularVelocity = Vector3.zero;
                // Don't disable enabled=false here, we might need to be pushed later
                // Just stop updating via proxy? No, proxy iterates list.
                // We can set enabled=false, ApplyImpulse will re-enable.
                enabled = false; 
            }
        }
    }
}
