using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Attach to player GameObjects with an Animator. Add animation events that call
    /// `OnFootstep()` and `OnLanding()` on this component. The bridge performs a
    /// lightweight raycast to resolve a `SurfaceMaterial` (via `SurfaceMaterialAuthoring`),
    /// then forwards the call to an `AudioManager` instance for immediate local playback.
    /// </summary>
    public class AnimatorEventBridge : MonoBehaviour
    {
        [Tooltip("Optional transform placed at the foot bone. If null, the GameObject position is used.")]
        public Transform FootTransform;

        [Header("Network")]
        [Tooltip("If true the bridge will publish a compact network audio event so remote clients can play this footstep.")]
        public bool PublishNetworkEvent = false;

        [Tooltip("Designer hint for stance (0=Walk,1=Crouch,2=Prone,3=Run). Animator events can set this per-clip.")]
        public int Stance = 0;
        [Header("Animator Integration")]
        [Tooltip("Optional Animator trigger parameter to fire when a footstep occurs. Leave blank to skip.")]
        public string AnimatorFootstepTrigger = "";
        [Tooltip("Optional Animator trigger parameter to fire when a landing occurs. Leave blank to skip.")]
        public string AnimatorLandingTrigger = "";

        // cached animator and hashes
        Player.Bridges.AnimatorRigBridge _animRigBridge;
        Animator _animator;
        int h_FootstepTrigger;
        int h_LandingTrigger;

        void Reset()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        void Awake()
        {
            _animRigBridge = GetComponentInChildren<Player.Bridges.AnimatorRigBridge>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        void OnValidate()
        {
            CacheHashes();
        }

        void CacheHashes()
        {
            h_FootstepTrigger = !string.IsNullOrEmpty(AnimatorFootstepTrigger) ? Animator.StringToHash(AnimatorFootstepTrigger) : 0;
            h_LandingTrigger = !string.IsNullOrEmpty(AnimatorLandingTrigger) ? Animator.StringToHash(AnimatorLandingTrigger) : 0;
        }

        public void OnFootstep()
        {
            var pos = FootTransform != null ? FootTransform.position : transform.position;
            int matId = ResolveMaterialIdAt(pos);
            var mgr = FindAudioManager();
            if (mgr != null)
            {
                mgr.PlayFootstep(matId, pos, Stance);
                if (PublishNetworkEvent)
                {
                    NetworkedAudioPublisher.Publish(matId, pos, Stance, 0, 1f);
                }
            }
            else
            {
                Debug.LogWarning("AnimatorEventBridge: No AudioManager found in scene.", this);
            }

            // Optionally set animator trigger for visual/IK sync
            if (h_FootstepTrigger != 0 && _animator != null)
            {
                _animator.SetTrigger(h_FootstepTrigger);
            }
        }

        public void OnLanding()
        {
            // Play landing audio (reuse footstep semantics by default)
            OnFootstep();

            // Fire animator landing trigger if configured
            if (h_LandingTrigger != 0 && _animator != null)
            {
                _animator.SetTrigger(h_LandingTrigger);
            }

            // Notify AnimatorRigBridge (if present) so it can run rig/IK landing responses
            if (_animRigBridge != null)
            {
                _animRigBridge.TriggerLanding();
            }
        }

        // Optional animator event overloads that accept an int or string parameter
        // (Animator events can pass a parameter).
        public void OnFootstep_Int(int materialId)
        {
            var pos = FootTransform != null ? FootTransform.position : transform.position;
            var mgr = FindAudioManager();
            if (mgr != null)
            {
                mgr.PlayFootstep(materialId, pos, Stance);
                if (PublishNetworkEvent) NetworkedAudioPublisher.Publish(materialId, pos, Stance, 0, 1f);
            }
        }

        public void OnFootstep_String(string unused)
        {
            // designers could pass a material name; for now we ignore and raycast.
            OnFootstep();
        }

        private AudioManager FindAudioManager()
        {
            // Use the newer API to find a scene AudioManager instance.
            // Prefer FindFirstObjectByType which returns the first matching object or null.
            var mgr = Object.FindFirstObjectByType<AudioManager>();
            return mgr;
        }

        private int ResolveMaterialIdAt(Vector3 worldPos)
        {
            // Raycast a short distance downward and attempt to find a SurfaceMaterialAuthoring
            // on the hit GameObject or its parents. If not found, return 0 and let AudioManager
            // fallback to its default material.
            var down = Vector3.down;
            if (Physics.Raycast(worldPos + Vector3.up * 0.05f, down, out var hit, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var go = hit.collider.gameObject;
                var auth = go.GetComponentInParent<SurfaceMaterialAuthoring>();
                if (auth != null && auth.Material != null)
                {
                    return auth.Material.Id;
                }
            }
            return 0;
        }
    }
}
