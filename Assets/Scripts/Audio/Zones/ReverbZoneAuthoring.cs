using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Zones
{
    /// <summary>
    /// Defines a reverb zone with trigger collider. When the player enters,
    /// the AudioReverbZoneManager transitions the mixer to this zone's snapshot.
    /// EPIC 15.27 Phase 4.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ReverbZoneAuthoring : MonoBehaviour
    {
        [Header("Zone Settings")]
        [Tooltip("Display name for this zone")]
        public string ZoneName = "Unnamed Zone";

        [Tooltip("Reverb preset to apply when player is in this zone")]
        public ReverbPreset Preset = ReverbPreset.OpenField;

        [Tooltip("Transition duration when entering/exiting this zone (seconds)")]
        [Range(0.1f, 5f)]
        public float TransitionDuration = 1.5f;

        [Tooltip("Priority for overlapping zones (higher wins)")]
        public int Priority = 0;

        [Tooltip("Whether this is an interior zone (drives IndoorFactor)")]
        public bool IsInterior = false;

        [Header("Custom Snapshot (only for Preset=Custom)")]
        [Tooltip("Direct AudioMixer Snapshot reference for custom reverb")]
        public AudioMixerSnapshot CustomSnapshot;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            AudioReverbZoneManager.Instance?.EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            AudioReverbZoneManager.Instance?.ExitZone(this);
        }

        private bool IsPlayer(Collider col)
        {
            // Check for player tag or specific component
            return col.CompareTag("Player") || col.GetComponentInParent<AudioListener>() != null;
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Color zoneColor = IsInterior ? new Color(0.3f, 0.3f, 0.8f, 0.15f) : new Color(0.3f, 0.8f, 0.3f, 0.15f);
            Gizmos.color = zoneColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.5f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
    }
}
