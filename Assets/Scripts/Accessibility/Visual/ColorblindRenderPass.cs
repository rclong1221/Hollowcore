using DIG.Widgets.Config;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DIG.Accessibility.Visual
{
    /// <summary>
    /// EPIC 18.12: Fullscreen blit pass applying Daltonization color matrix.
    /// Runs after post-processing. Zero cost when mode == None.
    /// Uses Render Graph API (Unity 6).
    /// </summary>
    public class ColorblindRenderPass : ScriptableRenderPass
    {
        private const string PASS_NAME = "Colorblind Correction Pass";

        private readonly ColorblindFilter.Settings _settings;
        private Material _material;

        // Daltonization correction matrices (row-major float3x3)
        // Source: Machado et al. (2009) colorblind simulation model
        private static readonly Matrix4x4 ProtanopiaCorrect = new Matrix4x4(
            new Vector4(0.567f, 0.558f, 0f, 0f),
            new Vector4(0.433f, 0.442f, 0.242f, 0f),
            new Vector4(0f, 0f, 0.758f, 0f),
            new Vector4(0f, 0f, 0f, 1f)
        );

        private static readonly Matrix4x4 DeuteranopiaCorrect = new Matrix4x4(
            new Vector4(0.625f, 0.7f, 0f, 0f),
            new Vector4(0.375f, 0.3f, 0.3f, 0f),
            new Vector4(0f, 0f, 0.7f, 0f),
            new Vector4(0f, 0f, 0f, 1f)
        );

        private static readonly Matrix4x4 TritanopiaCorrect = new Matrix4x4(
            new Vector4(0.95f, 0f, 0f, 0f),
            new Vector4(0.05f, 0.433f, 0.475f, 0f),
            new Vector4(0f, 0.567f, 0.525f, 0f),
            new Vector4(0f, 0f, 0f, 1f)
        );

        private static readonly int ColorMatrixId = Shader.PropertyToID("_ColorMatrix");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public ColorblindRenderPass(ColorblindFilter.Settings settings)
        {
            _settings = settings;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public void UpdateSettings(ColorblindFilter.Settings settings)
        {
            _material = settings.CorrectionMaterial;
        }

        private class PassData
        {
            public Material Material;
            public TextureHandle Source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _settings.Mode == ColorblindMode.None) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Set shader properties
            Matrix4x4 matrix = GetMatrixForMode(_settings.Mode);
            _material.SetMatrix(ColorMatrixId, matrix);
            _material.SetFloat(IntensityId, _settings.Intensity);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out var passData))
            {
                passData.Material = _material;
                passData.Source = resourceData.activeColorTexture;

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                });
            }
        }

        private static Matrix4x4 GetMatrixForMode(ColorblindMode mode)
        {
            return mode switch
            {
                ColorblindMode.Protanopia => ProtanopiaCorrect,
                ColorblindMode.Deuteranopia => DeuteranopiaCorrect,
                ColorblindMode.Tritanopia => TritanopiaCorrect,
                _ => Matrix4x4.identity
            };
        }
    }
}
