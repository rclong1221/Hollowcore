using UnityEngine;

namespace Opsive.UltimateCharacterController.AddOns.Swimming.Demo
{
    /// <summary>
    /// Moves the water level up and down.
    /// </summary>
    public class MovingWaterLevel : MonoBehaviour
    {
        [Tooltip("The amount to move the water level by.")]
        [SerializeField] protected float m_Amount = 2;
        [Tooltip("The speed at which the water moves.")]
        [SerializeField] protected float m_MoveSpeed = .5f;

        private Transform m_Transform;
        private Vector3 m_Max;
        private Vector3 m_Min;
        private bool m_Rising;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Max = m_Transform.position;
            m_Min = m_Max + Vector3.down * m_Amount;
        }

        /// <summary>
        /// Updates the water level.
        /// </summary>
        private void FixedUpdate()
        {
            var prevPosition = m_Transform.position;
            var destination = m_Rising ? m_Max : m_Min;
            m_Transform.position = Vector3.MoveTowards(prevPosition, destination, m_MoveSpeed * Time.deltaTime);
            if ((m_Transform.position - destination).magnitude < 0.1f) {
                m_Rising = !m_Rising;
            }
        }
    }
}