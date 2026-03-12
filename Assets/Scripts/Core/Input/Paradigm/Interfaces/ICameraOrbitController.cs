namespace DIG.Core.Input
{
    /// <summary>
    /// Controls camera orbit behavior based on paradigm.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public interface ICameraOrbitController
    {
        /// <summary>Current camera orbit mode.</summary>
        CameraOrbitMode CurrentOrbitMode { get; }

        /// <summary>Whether camera orbit is currently active (based on mode and button state).</summary>
        bool IsOrbitActive { get; }

        /// <summary>Whether Q/E key rotation is enabled.</summary>
        bool IsKeyRotationEnabled { get; }

        /// <summary>Whether edge-pan camera movement is enabled.</summary>
        bool IsEdgePanEnabled { get; }
    }
}
