using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Weapons.UI
{
    /// <summary>
    /// EPIC 14.20: HUD display for weapon ammo and status.
    /// </summary>
    public class WeaponHUD : MonoBehaviour
    {
        public static WeaponHUD Instance { get; private set; }

        [Header("Ammo Display")]
        [Tooltip("Text showing current clip ammo")]
        [SerializeField] private TextMeshProUGUI ammoCurrentText;

        [Tooltip("Text showing reserve ammo")]
        [SerializeField] private TextMeshProUGUI ammoReserveText;

        [Tooltip("Ammo display container")]
        [SerializeField] private GameObject ammoDisplay;

        [Header("Weapon Info")]
        [Tooltip("Text showing weapon name")]
        [SerializeField] private TextMeshProUGUI weaponNameText;

        [Tooltip("Image showing weapon icon")]
        [SerializeField] private Image weaponIcon;

        [Tooltip("Fire mode indicator text")]
        [SerializeField] private TextMeshProUGUI fireModeText;

        [Header("Reload Indicator")]
        [Tooltip("Reload progress bar")]
        [SerializeField] private Slider reloadProgressBar;

        [Tooltip("Reload text")]
        [SerializeField] private TextMeshProUGUI reloadText;

        [Header("Low Ammo Warning")]
        [Tooltip("Threshold for low ammo warning (percentage)")]
        [SerializeField] private float lowAmmoThreshold = 0.25f;

        [Tooltip("Color for normal ammo")]
        [SerializeField] private Color normalAmmoColor = Color.white;

        [Tooltip("Color for low ammo")]
        [SerializeField] private Color lowAmmoColor = Color.red;

        [Tooltip("Pulse low ammo warning")]
        [SerializeField] private bool pulseLowAmmo = true;

        [Header("Hit Marker")]
        [Tooltip("Hit marker image")]
        [SerializeField] private Image hitMarkerImage;

        [Tooltip("Hit marker duration")]
        [SerializeField] private float hitMarkerDuration = 0.2f;

        [Tooltip("Hit marker color")]
        [SerializeField] private Color hitMarkerColor = Color.white;

        [Tooltip("Headshot hit marker color")]
        [SerializeField] private Color headshotMarkerColor = Color.red;

        // State
        private float _hitMarkerTimer;
        private float _pulseTimer;
        private int _lastAmmo = -1;
        private int _lastReserve = -1;
        private bool _isReloading;
        private float _reloadProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize UI state
            if (hitMarkerImage != null)
            {
                hitMarkerImage.enabled = false;
            }

            if (reloadProgressBar != null)
            {
                reloadProgressBar.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Update hit marker
            if (_hitMarkerTimer > 0)
            {
                _hitMarkerTimer -= Time.deltaTime;
                if (_hitMarkerTimer <= 0 && hitMarkerImage != null)
                {
                    hitMarkerImage.enabled = false;
                }
            }

            // Update low ammo pulse
            if (pulseLowAmmo && ammoCurrentText != null)
            {
                _pulseTimer += Time.deltaTime;
            }

            // Update reload progress
            if (_isReloading && reloadProgressBar != null)
            {
                reloadProgressBar.value = _reloadProgress;
            }
        }

        /// <summary>
        /// Update the ammo display.
        /// </summary>
        public void UpdateAmmo(int current, int clipSize, int reserve)
        {
            if (ammoCurrentText != null)
            {
                if (current != _lastAmmo)
                {
                    ammoCurrentText.text = current.ToString();
                    _lastAmmo = current;

                    // Check for low ammo
                    float ratio = clipSize > 0 ? (float)current / clipSize : 1f;
                    if (ratio <= lowAmmoThreshold && current > 0)
                    {
                        if (pulseLowAmmo)
                        {
                            // Pulse effect
                            float pulse = Mathf.PingPong(_pulseTimer * 3f, 1f);
                            ammoCurrentText.color = Color.Lerp(lowAmmoColor, normalAmmoColor, pulse);
                        }
                        else
                        {
                            ammoCurrentText.color = lowAmmoColor;
                        }
                    }
                    else
                    {
                        ammoCurrentText.color = normalAmmoColor;
                    }
                }
            }

            if (ammoReserveText != null && reserve != _lastReserve)
            {
                ammoReserveText.text = reserve.ToString();
                _lastReserve = reserve;
            }
        }

        /// <summary>
        /// Update weapon info display.
        /// </summary>
        public void UpdateWeaponInfo(string weaponName, Sprite icon, string fireMode)
        {
            if (weaponNameText != null)
            {
                weaponNameText.text = weaponName;
            }

            if (weaponIcon != null && icon != null)
            {
                weaponIcon.sprite = icon;
                weaponIcon.enabled = true;
            }

            if (fireModeText != null)
            {
                fireModeText.text = fireMode;
            }
        }

        /// <summary>
        /// Show/hide the ammo display.
        /// </summary>
        public void SetAmmoDisplayVisible(bool visible)
        {
            if (ammoDisplay != null)
            {
                ammoDisplay.SetActive(visible);
            }
        }

        /// <summary>
        /// Start showing reload progress.
        /// </summary>
        public void StartReload(float duration)
        {
            _isReloading = true;
            _reloadProgress = 0f;

            if (reloadProgressBar != null)
            {
                reloadProgressBar.gameObject.SetActive(true);
                reloadProgressBar.value = 0f;
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Update reload progress.
        /// </summary>
        public void UpdateReloadProgress(float progress)
        {
            _reloadProgress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// End reload display.
        /// </summary>
        public void EndReload()
        {
            _isReloading = false;

            if (reloadProgressBar != null)
            {
                reloadProgressBar.gameObject.SetActive(false);
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show hit marker.
        /// </summary>
        public void ShowHitMarker(bool isHeadshot = false)
        {
            if (hitMarkerImage == null) return;

            hitMarkerImage.enabled = true;
            hitMarkerImage.color = isHeadshot ? headshotMarkerColor : hitMarkerColor;
            _hitMarkerTimer = hitMarkerDuration;
        }

        /// <summary>
        /// Clear all weapon display (when no weapon equipped).
        /// </summary>
        public void ClearDisplay()
        {
            SetAmmoDisplayVisible(false);

            if (weaponNameText != null)
                weaponNameText.text = "";

            if (weaponIcon != null)
                weaponIcon.enabled = false;

            if (fireModeText != null)
                fireModeText.text = "";

            EndReload();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
