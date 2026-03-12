using DIG.Widgets.Config;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DIG.Accessibility.Visual
{
    /// <summary>
    /// EPIC 18.12: URP ScriptableRendererFeature for colorblind correction/simulation.
    /// Add to URP Renderer Asset. AccessibilityService sets mode/intensity at runtime.
    /// Follows VoxelTerrainRendererFeature pattern.
    /// </summary>
    public class ColorblindFilter : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Active colorblind correction/simulation mode.")]
            public ColorblindMode Mode = ColorblindMode.None;

            [Range(0f, 1f)]
            [Tooltip("Correction intensity (0 = no effect, 1 = full correction).")]
            public float Intensity = 1f;

            [Tooltip("Shader for colorblind correction. Assign ColorblindCorrection shader material.")]
            public Material CorrectionMaterial;
        }

        public Settings settings = new Settings();

        private ColorblindRenderPass _renderPass;

        // Static reference for runtime access from AccessibilityService
        private static ColorblindFilter _activeInstance;
        public static ColorblindFilter ActiveInstance => _activeInstance;

        public override void Create()
        {
            _renderPass = new ColorblindRenderPass(settings);
            _activeInstance = this;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.Mode == ColorblindMode.None) return;
            if (settings.CorrectionMaterial == null) return;

            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            _renderPass.UpdateSettings(settings);
            renderer.EnqueuePass(_renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            _renderPass = null;
            if (_activeInstance == this)
                _activeInstance = null;
        }

        /// <summary>Set colorblind mode at runtime.</summary>
        public void SetMode(ColorblindMode mode)
        {
            settings.Mode = mode;
        }

        /// <summary>Set correction intensity at runtime.</summary>
        public void SetIntensity(float intensity)
        {
            settings.Intensity = Mathf.Clamp01(intensity);
        }
    }
}
