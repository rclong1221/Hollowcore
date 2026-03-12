using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DIG.Voxel.Rendering
{
    /// <summary>
    /// OPTIMIZATION 10.9.19: Render Graph Integration.
    /// ScriptableRendererFeature that injects VoxelTerrainRenderPass into URP.
    /// Add this to your URP Renderer Asset to enable optimized voxel terrain rendering.
    /// </summary>
    public class VoxelTerrainRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("When to render voxel terrain in the pipeline")]
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            [Tooltip("Layer mask for voxel terrain objects")]
            public LayerMask LayerMask = -1; // Default: Everything
            
            [Tooltip("Optional override material for debugging")]
            public Material OverrideMaterial = null;
        }
        
        public Settings settings = new Settings();
        
        private VoxelTerrainRenderPass _renderPass;
        
        /// <summary>
        /// Called when the feature is created. Initialize resources here.
        /// </summary>
        public override void Create()
        {
            _renderPass = new VoxelTerrainRenderPass(
                settings.RenderPassEvent,
                settings.LayerMask,
                settings.OverrideMaterial
            );
        }
        
        /// <summary>
        /// Called every frame for each camera. Enqueue the render pass here.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Skip for preview cameras, reflection probes, etc.
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
            {
                return;
            }
            
            renderer.EnqueuePass(_renderPass);
        }
        
        /// <summary>
        /// Called when the feature is destroyed. Cleanup resources here.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            _renderPass = null;
        }
    }
}
