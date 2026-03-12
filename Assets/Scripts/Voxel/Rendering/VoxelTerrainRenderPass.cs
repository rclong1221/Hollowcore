using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DIG.Voxel.Rendering
{
    /// <summary>
    /// OPTIMIZATION 10.9.19: Render Graph Integration.
    /// Custom URP Render Pass for voxel terrain rendering.
    /// Integrates with Unity 6's Render Graph system for optimal batching.
    /// </summary>
    public class VoxelTerrainRenderPass : ScriptableRenderPass
    {
        private const string PASS_NAME = "Voxel Terrain Pass";
        private readonly LayerMask _layerMask;
        private readonly Material _overrideMaterial;
        
        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        
        public VoxelTerrainRenderPass(RenderPassEvent renderPassEvent, LayerMask layerMask, Material overrideMaterial = null)
        {
            this.renderPassEvent = renderPassEvent;
            _layerMask = layerMask;
            _overrideMaterial = overrideMaterial;
            
            // Setup filtering to only render voxel terrain layer
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            
            // Default render state (no overrides)
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }
        
        // Unity 6 Render Graph API
        private class PassData
        {
            public RendererListHandle RendererListHandle;
        }
        
        /// <summary>
        /// RecordRenderGraph - Unity 6 Render Graph entry point.
        /// Called during render graph configuration to register this pass.
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out var passData))
            {
                // Get frame resources
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                
                // Setup renderer list descriptor
                var sortingCriteria = SortingCriteria.CommonOpaque;
                var drawingSettings = RenderingUtils.CreateDrawingSettings(
                    new ShaderTagId("UniversalForward"),
                    renderingData,
                    cameraData,
                    lightData,
                    sortingCriteria
                );
                
                // Apply override material if specified
                if (_overrideMaterial != null)
                {
                    drawingSettings.overrideMaterial = _overrideMaterial;
                    drawingSettings.overrideMaterialPassIndex = 0;
                }
                
                // Create renderer list for voxel terrain
                var rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    _filteringSettings
                );
                
                passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParams);
                
                // Set render target (active color target)
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                
                // Use renderer list
                builder.UseRendererList(passData.RendererListHandle);
                
                // Set render function
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererListHandle);
                });
            }
        }
    }
}
