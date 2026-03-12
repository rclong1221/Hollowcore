using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Controls magazine detach/attach/drop during reload animations.
    /// Works with WeaponReloadAuthoring for configuration.
    /// Supports both first-person and third-person perspectives.
    /// Called by animation events via WeaponAnimationEventRelay.
    /// </summary>
    public class MagazineReloadController : MonoBehaviour
    {
        private WeaponReloadAuthoring _config;
        
        // Cached original transform data for first person
        private Transform _firstPersonOriginalParent;
        private Vector3 _firstPersonOriginalLocalPosition;
        private Quaternion _firstPersonOriginalLocalRotation;
        
        // Cached original transform data for third person
        private Transform _thirdPersonOriginalParent;
        private Vector3 _thirdPersonOriginalLocalPosition;
        private Quaternion _thirdPersonOriginalLocalRotation;
        
        // State
        private bool _isReloading;
        private GameObject _firstPersonFreshMagInstance;
        private GameObject _thirdPersonFreshMagInstance;
        
        private void Awake()
        {
            _config = GetComponent<WeaponReloadAuthoring>();
            if (_config == null)
            {
                Debug.LogWarning($"[MagazineReloadController] No WeaponReloadAuthoring found on {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Start reload sequence. Called when reload animation begins.
        /// </summary>
        public void StartReload()
        {
            if (_config == null) return;
            
            _isReloading = true;
            
            // Cache original transforms for both perspectives
            CacheOriginalTransform(true);  // First person
            CacheOriginalTransform(false); // Third person
            
            Debug.Log($"[MagazineReloadController] StartReload on {gameObject.name}");
        }
        
        private void CacheOriginalTransform(bool firstPerson)
        {
            var clip = _config.GetMagazineClip(firstPerson);
            if (clip == null) return;
            
            if (firstPerson && _firstPersonOriginalParent == null)
            {
                _firstPersonOriginalParent = clip.parent;
                _firstPersonOriginalLocalPosition = clip.localPosition;
                _firstPersonOriginalLocalRotation = clip.localRotation;
            }
            else if (!firstPerson && _thirdPersonOriginalParent == null)
            {
                _thirdPersonOriginalParent = clip.parent;
                _thirdPersonOriginalLocalPosition = clip.localPosition;
                _thirdPersonOriginalLocalRotation = clip.localRotation;
            }
        }
        
        /// <summary>
        /// Detach magazine from weapon and attach to hand.
        /// Called by OnAnimatorItemReloadDetachClip animation event.
        /// Applies to both first-person and third-person.
        /// </summary>
        public void DetachMagazine()
        {
            if (_config == null || !_isReloading) return;
            if (!_config.DetachAttachClip) return;
            
            // Apply to both perspectives
            DetachMagazineForPerspective(true);  // First person
            DetachMagazineForPerspective(false); // Third person
            
            Debug.Log($"[MagazineReloadController] DetachMagazine - reparented to hand (both perspectives)");
        }
        
        private void DetachMagazineForPerspective(bool firstPerson)
        {
            var clip = _config.GetMagazineClip(firstPerson);
            var attachment = _config.GetClipAttachment(firstPerson);
            
            if (clip == null || attachment == null) return;
            
            // Cache original transform
            CacheOriginalTransform(firstPerson);
            
            // Reparent to hand
            clip.SetParent(attachment);
            
            if (_config.ResetClipTransformOnDetach)
            {
                clip.localPosition = Vector3.zero;
                clip.localRotation = Quaternion.identity;
            }
        }
        
        /// <summary>
        /// Drop the old magazine as a physics object.
        /// Called by OnAnimatorItemReloadDropClip animation event.
        /// </summary>
        public void DropMagazine()
        {
            if (_config == null || !_isReloading) return;
            
            // Hide both perspective magazines
            HideMagazine(true);  // First person
            HideMagazine(false); // Third person
            
            // Spawn physics drop prefab (only once, using first person position)
            var clip = _config.GetMagazineClip(true) ?? _config.GetMagazineClip(false);
            if (clip == null) return;
            
            if (_config.DropMagazinePrefab != null)
            {
                var dropMag = Instantiate(
                    _config.DropMagazinePrefab,
                    clip.position,
                    clip.rotation
                );
                
                // Apply small random velocity for natural drop
                var rb = dropMag.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = transform.forward * -0.5f + Vector3.down * 0.2f;
                    rb.angularVelocity = Random.insideUnitSphere * 2f;
                }
                
                // Auto-destroy after configurable time
                Destroy(dropMag, 5f);
                
                Debug.Log($"[MagazineReloadController] DropMagazine - spawned physics prefab");
            }
            else if (!string.IsNullOrEmpty(_config.MagazineDropTypeID))
            {
                // Try ECS spawning via bridge
                DIG.Items.Bridges.ShellSpawnBridge.RequestShellSpawn(
                    _config.MagazineDropTypeID,
                    clip.position,
                    clip.rotation,
                    transform.forward * -0.5f + Vector3.down * 0.2f
                );
                Debug.Log($"[MagazineReloadController] DropMagazine - ECS spawn requested");
            }
        }
        
        private void HideMagazine(bool firstPerson)
        {
            var clip = _config.GetMagazineClip(firstPerson);
            if (clip != null)
            {
                clip.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Show fresh magazine (either reactivate original or show prefab).
        /// Called by OnAnimatorItemReactivateClip animation event.
        /// </summary>
        public void ShowFreshMagazine()
        {
            if (_config == null || !_isReloading) return;
            
            ShowFreshMagazineForPerspective(true);  // First person
            ShowFreshMagazineForPerspective(false); // Third person
            
            Debug.Log($"[MagazineReloadController] ShowFreshMagazine");
        }
        
        private void ShowFreshMagazineForPerspective(bool firstPerson)
        {
            var clip = _config.GetMagazineClip(firstPerson);
            var attachment = _config.GetClipAttachment(firstPerson);
            
            if (_config.FreshMagazinePrefab != null && attachment != null)
            {
                // Spawn fresh magazine prefab in hand
                var freshMag = Instantiate(
                    _config.FreshMagazinePrefab,
                    attachment.position,
                    attachment.rotation,
                    attachment
                );
                
                if (firstPerson)
                    _firstPersonFreshMagInstance = freshMag;
                else
                    _thirdPersonFreshMagInstance = freshMag;
            }
            else if (clip != null)
            {
                // Just reactivate the original magazine
                clip.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// Attach magazine back to weapon from hand.
        /// Called by OnAnimatorItemReloadAttachClip animation event.
        /// </summary>
        public void AttachMagazine()
        {
            if (_config == null || !_isReloading) return;
            
            AttachMagazineForPerspective(true);  // First person
            AttachMagazineForPerspective(false); // Third person
            
            Debug.Log($"[MagazineReloadController] AttachMagazine - restored to weapon (both perspectives)");
        }
        
        private void AttachMagazineForPerspective(bool firstPerson)
        {
            var clip = _config.GetMagazineClip(firstPerson);
            if (clip == null) return;
            
            // Destroy fresh mag instance if exists
            if (firstPerson && _firstPersonFreshMagInstance != null)
            {
                Destroy(_firstPersonFreshMagInstance);
                _firstPersonFreshMagInstance = null;
            }
            else if (!firstPerson && _thirdPersonFreshMagInstance != null)
            {
                Destroy(_thirdPersonFreshMagInstance);
                _thirdPersonFreshMagInstance = null;
            }
            
            // Reparent back to weapon
            Transform originalParent = firstPerson ? _firstPersonOriginalParent : _thirdPersonOriginalParent;
            Vector3 originalPos = firstPerson ? _firstPersonOriginalLocalPosition : _thirdPersonOriginalLocalPosition;
            Quaternion originalRot = firstPerson ? _firstPersonOriginalLocalRotation : _thirdPersonOriginalLocalRotation;
            
            if (originalParent != null)
            {
                clip.SetParent(originalParent);
                clip.localPosition = originalPos;
                clip.localRotation = originalRot;
            }
            
            // Ensure magazine is visible
            clip.gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Complete reload sequence. Called when reload animation ends.
        /// </summary>
        public void CompleteReload()
        {
            _isReloading = false;
            
            // Ensure clean state for both perspectives
            if (_config != null)
            {
                AttachMagazineForPerspective(true);
                AttachMagazineForPerspective(false);
            }
            
            Debug.Log($"[MagazineReloadController] CompleteReload");
        }
        
        /// <summary>
        /// Cancel reload. Restores magazine to original state.
        /// </summary>
        public void CancelReload()
        {
            if (!_isReloading) return;
            
            // Clean up fresh mag instances
            if (_firstPersonFreshMagInstance != null)
            {
                Destroy(_firstPersonFreshMagInstance);
                _firstPersonFreshMagInstance = null;
            }
            if (_thirdPersonFreshMagInstance != null)
            {
                Destroy(_thirdPersonFreshMagInstance);
                _thirdPersonFreshMagInstance = null;
            }
            
            // Restore magazines
            AttachMagazineForPerspective(true);
            AttachMagazineForPerspective(false);
            
            _isReloading = false;
            Debug.Log($"[MagazineReloadController] CancelReload");
        }
    }
}
