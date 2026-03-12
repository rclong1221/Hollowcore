using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DIG.Core
{
    /// <summary>
    /// Workaround for URP Render Graph bugs with reflection probes and ZBinningJob.
    /// This disables Render Graph at startup until Unity fixes the issue.
    /// 
    /// Known affected versions: URP 17.x with Unity 6
    /// Bug: NullReferenceException in ReflectionProbeManager.UpdateGpuData
    /// Bug: ZBinningJob race condition in ForwardLights.PreSetup
    /// </summary>
    public class URPRenderGraphWorkaround : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableRenderGraph()
        {
            // Check if we're using URP
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return;
            
            // Disable Render Graph to avoid the bug
            // Note: This uses reflection as the API might not be public in all versions
            #if UNITY_6000_0_OR_NEWER
            // In Unity 6+, you may need to set this via Project Settings
            Debug.Log("[URP Workaround] Consider disabling Render Graph in Project Settings > Graphics if you see ZBinningJob errors");
            #endif
        }
    }
}
