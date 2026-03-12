using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Defines a standard "Socket" on a character (e.g., MainHand, OffHand).
    /// Used by the WeaponEquipVisualBridge to find attachment points without relying on naming conventions.
    /// </summary>
    public class SocketAuthoring : MonoBehaviour
    {
        public enum SocketType
        {
            MainHand,
            OffHand,
            Back,
            Hips,
            ThighLeft,
            ThighRight
        }

        [Tooltip("Type of socket this transform represents")]
        public SocketType Type;

        private void OnDrawGizmos()
        {
            Gizmos.color = Type == SocketType.MainHand ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.05f);
        }
    }
}
