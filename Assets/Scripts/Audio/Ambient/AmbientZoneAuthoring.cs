using UnityEngine;

namespace Audio.Ambient
{
    /// <summary>
    /// EPIC 18.8: Place on a GameObject with a trigger collider to define an ambient soundscape zone.
    /// When the player enters this zone, AmbientZoneManager crossfades to the assigned soundscape.
    /// Supports overlapping zones via priority.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AmbientZoneAuthoring : MonoBehaviour
    {
        [Header("Soundscape")]
        [Tooltip("The ambient soundscape to activate when a player enters this zone.")]
        public AmbientSoundscapeSO Soundscape;

        [Header("Debug")]
        [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.7f, 1f, 0.2f);

        public int Priority => Soundscape != null ? Soundscape.Priority : 0;

        private static int s_playerLayer = -1;

        // Cached collider refs for gizmo drawing
        private Collider _cachedCollider;
        private bool _colliderCached;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (AmbientZoneManager.Instance != null)
                AmbientZoneManager.Instance.OnZoneEnter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (AmbientZoneManager.Instance != null)
                AmbientZoneManager.Instance.OnZoneExit(this);
        }

        private static bool IsPlayer(Collider col)
        {
            if (col.CompareTag("Player"))
                return true;

            if (s_playerLayer < 0)
                s_playerLayer = LayerMask.NameToLayer("Player");

            return col.gameObject.layer == s_playerLayer;
        }

        private void OnDrawGizmos()
        {
            if (!_colliderCached)
            {
                _cachedCollider = GetComponent<Collider>();
                _colliderCached = true;
            }

            if (_cachedCollider == null) return;

            Gizmos.color = _gizmoColor;

            if (_cachedCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (_cachedCollider is SphereCollider sphere)
            {
                Vector3 center = transform.position + sphere.center;
                float radius = sphere.radius * transform.lossyScale.x;
                Gizmos.DrawSphere(center, radius);
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        private void OnValidate()
        {
            _colliderCached = false;
        }
    }
}
