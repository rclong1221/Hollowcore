using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DIG.Core.Input;
using DIG.CameraSystem;
using DIG.CameraSystem.Cinemachine;

namespace DIG.Settings
{
    /// <summary>
    /// Settings UI for camera and control scheme selection.
    /// Shows only paradigms compatible with current camera.
    /// 
    /// Attach to a panel with dropdowns for camera and controls.
    /// 
    /// EPIC 15.20 - Input Paradigm Settings UI
    /// </summary>
    public class InputParadigmSettingsUI : MonoBehaviour
    {
        // ============================================================
        // UI REFERENCES
        // ============================================================

        [Header("UI References")]
        [Tooltip("Dropdown for camera mode selection.")]
        [SerializeField] private TMP_Dropdown _cameraDropdown;

        [Tooltip("Dropdown for control scheme selection.")]
        [SerializeField] private TMP_Dropdown _controlsDropdown;

        [Tooltip("Text showing current camera mode.")]
        [SerializeField] private TextMeshProUGUI _cameraLabel;

        [Tooltip("Text showing current control scheme.")]
        [SerializeField] private TextMeshProUGUI _controlsLabel;

        [Tooltip("Description of selected control scheme.")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

        // ============================================================
        // STATE
        // ============================================================

        private List<CinemachineCameraMode> _availableCameras = new();
        private List<InputParadigmProfile> _availableProfiles = new();
        private bool _isUpdating; // Prevent recursive updates

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void OnEnable()
        {
            // Subscribe to changes
            if (CinemachineCameraController.HasInstance)
            {
                CinemachineCameraController.Instance.OnCameraModeChanged += OnCameraModeChanged;
            }

            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged += OnParadigmChanged;
            }

            // Initial population
            PopulateCameraDropdown();
            PopulateControlsDropdown();
            UpdateLabels();
        }

        private void OnDisable()
        {
            if (CinemachineCameraController.HasInstance)
            {
                CinemachineCameraController.Instance.OnCameraModeChanged -= OnCameraModeChanged;
            }

            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged -= OnParadigmChanged;
            }
        }

        // ============================================================
        // DROPDOWN POPULATION
        // ============================================================

        private void PopulateCameraDropdown()
        {
            if (_cameraDropdown == null) return;

            _cameraDropdown.ClearOptions();
            _availableCameras.Clear();

            if (!CinemachineCameraController.HasInstance) return;

            var options = new List<TMP_Dropdown.OptionData>();
            
            // Add available camera modes
            _availableCameras.Add(CinemachineCameraMode.ThirdPerson);
            options.Add(new TMP_Dropdown.OptionData("Third Person"));
            
            _availableCameras.Add(CinemachineCameraMode.Isometric);
            options.Add(new TMP_Dropdown.OptionData("Isometric"));
            
            _availableCameras.Add(CinemachineCameraMode.FirstPerson);
            options.Add(new TMP_Dropdown.OptionData("First Person"));

            _cameraDropdown.AddOptions(options);

            // Select current
            int currentIndex = _availableCameras.IndexOf(CinemachineCameraController.Instance.CurrentCameraMode);
            if (currentIndex >= 0)
            {
                _cameraDropdown.SetValueWithoutNotify(currentIndex);
            }
        }

        private void PopulateControlsDropdown()
        {
            if (_controlsDropdown == null) return;

            _controlsDropdown.ClearOptions();
            _availableProfiles.Clear();

            var stateMachine = ParadigmStateMachine.Instance;
            if (stateMachine == null) return;

            // Get only profiles compatible with current camera
            var compatibleProfiles = stateMachine.GetCompatibleProfiles();

            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var profile in compatibleProfiles)
            {
                _availableProfiles.Add(profile);
                options.Add(new TMP_Dropdown.OptionData(profile.displayName));
            }

            _controlsDropdown.AddOptions(options);

            // Select current
            int currentIndex = _availableProfiles.IndexOf(stateMachine.ActiveProfile);
            if (currentIndex >= 0)
            {
                _controlsDropdown.SetValueWithoutNotify(currentIndex);
            }

            // Update description
            UpdateDescription();
        }

        // ============================================================
        // UI CALLBACKS
        // ============================================================

        /// <summary>
        /// Called when camera dropdown changes.
        /// Wire this to TMP_Dropdown.OnValueChanged in Inspector.
        /// </summary>
        public void OnCameraDropdownChanged(int index)
        {
            if (_isUpdating) return;
            if (index < 0 || index >= _availableCameras.Count) return;

            var selectedMode = _availableCameras[index];
            CinemachineCameraController.Instance?.SetCameraMode(selectedMode);
        }

        /// <summary>
        /// Called when controls dropdown changes.
        /// Wire this to TMP_Dropdown.OnValueChanged in Inspector.
        /// </summary>
        public void OnControlsDropdownChanged(int index)
        {
            if (_isUpdating) return;
            if (index < 0 || index >= _availableProfiles.Count) return;

            var selectedProfile = _availableProfiles[index];
            ParadigmStateMachine.Instance?.TrySetParadigm(selectedProfile);
        }

        // ============================================================
        // EVENT HANDLERS
        // ============================================================

        private void OnCameraModeChanged(CinemachineCameraMode previous, CinemachineCameraMode current)
        {
            _isUpdating = true;

            // Update camera dropdown selection
            int index = _availableCameras.IndexOf(current);
            if (index >= 0 && _cameraDropdown != null)
            {
                _cameraDropdown.SetValueWithoutNotify(index);
            }

            // Repopulate controls dropdown (different camera = different compatible controls)
            PopulateControlsDropdown();
            UpdateLabels();

            _isUpdating = false;
        }

        private void OnParadigmChanged(InputParadigmProfile profile)
        {
            _isUpdating = true;

            // Update controls dropdown selection
            int index = _availableProfiles.IndexOf(profile);
            if (index >= 0 && _controlsDropdown != null)
            {
                _controlsDropdown.SetValueWithoutNotify(index);
            }

            UpdateLabels();
            UpdateDescription();

            _isUpdating = false;
        }

        // ============================================================
        // UI UPDATES
        // ============================================================

        private void UpdateLabels()
        {
            if (_cameraLabel != null && CinemachineCameraController.HasInstance)
            {
                _cameraLabel.text = GetCameraDisplayName(CinemachineCameraController.Instance.CurrentCameraMode);
            }

            if (_controlsLabel != null && ParadigmStateMachine.Instance != null)
            {
                var profile = ParadigmStateMachine.Instance.ActiveProfile;
                _controlsLabel.text = profile != null ? profile.displayName : "None";
            }
        }

        private void UpdateDescription()
        {
            if (_descriptionText == null) return;

            var stateMachine = ParadigmStateMachine.Instance;
            if (stateMachine == null || stateMachine.ActiveProfile == null)
            {
                _descriptionText.text = "";
                return;
            }

            _descriptionText.text = stateMachine.ActiveProfile.description;
        }

        private string GetCameraDisplayName(CinemachineCameraMode mode)
        {
            return mode switch
            {
                CinemachineCameraMode.ThirdPerson => "Third Person",
                CinemachineCameraMode.Isometric => "Isometric",
                CinemachineCameraMode.FirstPerson => "First Person",
                _ => mode.ToString()
            };
        }
    }
}
