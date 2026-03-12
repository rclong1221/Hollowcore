using UnityEngine;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// MonoBehaviour controller for station camera effects.
    /// Handles camera transitions, HUD changes, and UI overlays for different station types.
    /// </summary>
    public class StationCameraController : MonoBehaviour
    {
        public static StationCameraController Instance { get; private set; }

        [Header("Helm/Piloting UI")]
        [Tooltip("UI panel shown when piloting")]
        public GameObject PilotingHUD;

        [Tooltip("Ship status display")]
        public GameObject ShipStatusPanel;

        [Header("Weapon Station UI")]
        [Tooltip("Targeting reticle for weapon stations")]
        public GameObject TargetingReticle;

        [Tooltip("Weapon status panel")]
        public GameObject WeaponStatusPanel;

        [Header("Drill Station UI")]
        [Tooltip("Mining HUD overlay")]
        public GameObject MiningHUD;

        [Tooltip("Resource display")]
        public GameObject ResourcePanel;

        [Header("Systems Panel UI")]
        [Tooltip("Ship systems interface")]
        public GameObject SystemsInterface;

        [Header("Camera Effects")]
        [Tooltip("Camera shake intensity when operating stations")]
        public float OperatingShakeIntensity = 0.1f;

        [Tooltip("FOV adjustment when piloting")]
        public float PilotingFOV = 70f;

        [Tooltip("FOV adjustment for targeting")]
        public float TargetingFOV = 50f;

        [Header("Audio")]
        [Tooltip("Sound when entering a station")]
        public AudioClip EnterStationSound;

        [Tooltip("Sound when exiting a station")]
        public AudioClip ExitStationSound;

        [Tooltip("Ambient sound for piloting")]
        public AudioClip PilotingAmbience;

        private AudioSource _audioSource;
        private AudioSource _ambienceSource;
        private StationType _currentStationType;
        private bool _isOperating;

        void Awake()
        {
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }

            // Create separate source for ambience
            var ambienceObj = new GameObject("StationAmbience");
            ambienceObj.transform.SetParent(transform);
            _ambienceSource = ambienceObj.AddComponent<AudioSource>();
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.volume = 0.3f;

            // Hide all UI by default
            HideAllStationUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called when player enters a station.
        /// </summary>
        public void OnEnterStation(StationType stationType, Unity.Entities.Entity stationEntity)
        {
            _currentStationType = stationType;
            _isOperating = true;

            // Play enter sound
            if (_audioSource != null && EnterStationSound != null)
            {
                _audioSource.PlayOneShot(EnterStationSound);
            }

            // Show appropriate UI
            HideAllStationUI();
            
            switch (stationType)
            {
                case StationType.Helm:
                    ShowPilotingUI();
                    StartPilotingAmbience();
                    break;

                case StationType.WeaponStation:
                    ShowWeaponUI();
                    break;

                case StationType.DrillControl:
                    ShowMiningUI();
                    break;

                case StationType.SystemsPanel:
                    ShowSystemsUI();
                    break;

                case StationType.Engineering:
                case StationType.Communications:
                    // Generic station UI (could add more specific later)
                    break;
            }
        }

        /// <summary>
        /// Called when player exits a station.
        /// </summary>
        public void OnExitStation(StationType stationType)
        {
            _isOperating = false;

            // Play exit sound
            if (_audioSource != null && ExitStationSound != null)
            {
                _audioSource.PlayOneShot(ExitStationSound);
            }

            // Stop ambience
            if (_ambienceSource != null && _ambienceSource.isPlaying)
            {
                _ambienceSource.Stop();
            }

            // Hide all station UI
            HideAllStationUI();
        }

        private void HideAllStationUI()
        {
            if (PilotingHUD != null) PilotingHUD.SetActive(false);
            if (ShipStatusPanel != null) ShipStatusPanel.SetActive(false);
            if (TargetingReticle != null) TargetingReticle.SetActive(false);
            if (WeaponStatusPanel != null) WeaponStatusPanel.SetActive(false);
            if (MiningHUD != null) MiningHUD.SetActive(false);
            if (ResourcePanel != null) ResourcePanel.SetActive(false);
            if (SystemsInterface != null) SystemsInterface.SetActive(false);
        }

        private void ShowPilotingUI()
        {
            if (PilotingHUD != null) PilotingHUD.SetActive(true);
            if (ShipStatusPanel != null) ShipStatusPanel.SetActive(true);
        }

        private void ShowWeaponUI()
        {
            if (TargetingReticle != null) TargetingReticle.SetActive(true);
            if (WeaponStatusPanel != null) WeaponStatusPanel.SetActive(true);
        }

        private void ShowMiningUI()
        {
            if (MiningHUD != null) MiningHUD.SetActive(true);
            if (ResourcePanel != null) ResourcePanel.SetActive(true);
        }

        private void ShowSystemsUI()
        {
            if (SystemsInterface != null) SystemsInterface.SetActive(true);
        }

        private void StartPilotingAmbience()
        {
            if (_ambienceSource != null && PilotingAmbience != null)
            {
                _ambienceSource.clip = PilotingAmbience;
                _ambienceSource.Play();
            }
        }

        /// <summary>
        /// Get recommended FOV for current station type.
        /// </summary>
        public float GetStationFOV()
        {
            if (!_isOperating) return 60f;

            return _currentStationType switch
            {
                StationType.Helm => PilotingFOV,
                StationType.WeaponStation => TargetingFOV,
                _ => 60f
            };
        }

        /// <summary>
        /// Whether camera shake should be applied.
        /// </summary>
        public bool ShouldApplyCameraShake()
        {
            return _isOperating && _currentStationType == StationType.Helm;
        }
    }
}
