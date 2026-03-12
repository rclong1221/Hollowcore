using UnityEngine;

namespace Player.Bridges
{
    /// <summary>
    /// Attached to the foot/toe bones. Detects collision with ground and forwards to the character controller.
    /// Replaces Opsive's FootstepTrigger.cs for ECS compatibility.
    /// </summary>
    public class FootstepTrigger : MonoBehaviour
    {
        private CharacterFootEffects _effects;

        private void Awake()
        {
            _effects = GetComponentInParent<CharacterFootEffects>();
        }

        private float _lastTriggerTime;

        private void OnTriggerEnter(Collider other)
        {
            // Simple throttle to prevent physics jitter from spamming events
            if (Time.time - _lastTriggerTime < 0.2f) return;

            if (_effects != null)
            {
                _effects.OnFootstepHit(other, transform.position);
                _lastTriggerTime = Time.time;
            }
        }
    }
}
