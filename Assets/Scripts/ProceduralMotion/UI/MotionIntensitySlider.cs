using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using DIG.Core.Settings;

namespace DIG.ProceduralMotion.UI
{
    /// <summary>
    /// EPIC 15.25 Phase 5: Settings UI for motion intensity control.
    /// Writes to both the ECS ProceduralMotionIntensity singleton and the
    /// managed MotionIntensitySettings MonoBehaviour (shared with EPIC 15.24).
    /// </summary>
    public class MotionIntensitySlider : MonoBehaviour
    {
        [Header("UI Bindings")]
        [SerializeField] private Slider _globalSlider;
        [SerializeField] private Slider _cameraSlider;
        [SerializeField] private Slider _weaponSlider;

        [Header("Labels (optional)")]
        [SerializeField] private Text _globalLabel;
        [SerializeField] private Text _cameraLabel;
        [SerializeField] private Text _weaponLabel;

        private void OnEnable()
        {
            // Initialize sliders from current state
            if (MotionIntensitySettings.HasInstance)
            {
                if (_globalSlider != null)
                    _globalSlider.value = MotionIntensitySettings.Instance.GlobalIntensity;
            }

            // Bind listeners
            if (_globalSlider != null) _globalSlider.onValueChanged.AddListener(OnGlobalChanged);
            if (_cameraSlider != null) _cameraSlider.onValueChanged.AddListener(OnCameraChanged);
            if (_weaponSlider != null) _weaponSlider.onValueChanged.AddListener(OnWeaponChanged);
        }

        private void OnDisable()
        {
            if (_globalSlider != null) _globalSlider.onValueChanged.RemoveListener(OnGlobalChanged);
            if (_cameraSlider != null) _cameraSlider.onValueChanged.RemoveListener(OnCameraChanged);
            if (_weaponSlider != null) _weaponSlider.onValueChanged.RemoveListener(OnWeaponChanged);
        }

        private void OnGlobalChanged(float value)
        {
            // Update managed MonoBehaviour (EPIC 15.24)
            if (MotionIntensitySettings.HasInstance)
                MotionIntensitySettings.Instance.GlobalIntensity = value;

            // Update ECS singleton
            UpdateECSSingleton(globalIntensity: value);

            if (_globalLabel != null)
                _globalLabel.text = $"Motion: {value:P0}";
        }

        private void OnCameraChanged(float value)
        {
            UpdateECSSingleton(cameraScale: value);
            if (_cameraLabel != null)
                _cameraLabel.text = $"Camera: {value:P0}";
        }

        private void OnWeaponChanged(float value)
        {
            UpdateECSSingleton(weaponScale: value);
            if (_weaponLabel != null)
                _weaponLabel.text = $"Weapon: {value:P0}";
        }

        private void UpdateECSSingleton(float globalIntensity = -1f, float cameraScale = -1f, float weaponScale = -1f)
        {
            if (World.DefaultGameObjectInjectionWorld == null) return;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var query = em.CreateEntityQuery(ComponentType.ReadWrite<ProceduralMotionIntensity>());
            if (query.IsEmpty) return;

            var entity = query.GetSingletonEntity();
            var current = em.GetComponentData<ProceduralMotionIntensity>(entity);

            if (globalIntensity >= 0f) current.GlobalIntensity = globalIntensity;
            if (cameraScale >= 0f) current.CameraMotionScale = cameraScale;
            if (weaponScale >= 0f) current.WeaponMotionScale = weaponScale;

            em.SetComponentData(entity, current);
        }
    }
}
