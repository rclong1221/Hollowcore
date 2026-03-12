using UnityEngine;

namespace Player.Settings
{
    [CreateAssetMenu(menuName = "DIG/Player/LeanSettings", fileName = "LeanSettings")]
    public class LeanSettings : ScriptableObject
    {
        [Header("Lean Parameters")]
        [Tooltip("Max camera lean angle in degrees (visual only).")]
        public float MaxLeanAngle = 30f;

        [Tooltip("Lateral camera offset (meters) when fully leaned.")]
        public float LeanDistance = 0.5f;

        [Tooltip("Controls how fast the lean moves to its target (units/sec).")]
        public float LeanSpeed = 5f;

        [Tooltip("Allow leaning while moving. If false, leaning is disabled when move input magnitude > DeadMoveThreshold.")]
        public bool CanLeanWhileMoving = false;

        [Tooltip("Movement magnitude threshold above which leaning is blocked when CanLeanWhileMoving == false.")]
        public float DeadMoveThreshold = 0.15f;
    }
}
