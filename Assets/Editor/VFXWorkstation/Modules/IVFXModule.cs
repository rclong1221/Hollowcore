namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// Interface for VFX Workstation editor modules.
    /// Each module provides an OnGUI tab in the VFX Workstation window.
    /// </summary>
    public interface IVFXModule
    {
        void OnGUI();
    }
}
