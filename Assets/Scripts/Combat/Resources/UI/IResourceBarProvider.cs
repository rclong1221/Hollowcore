namespace DIG.Combat.Resources.UI
{
    /// <summary>
    /// EPIC 16.8 Phase 5: Interface for resource bar UI implementations.
    /// Registered via ResourceUIRegistry, updated by ResourceUIBridgeSystem.
    /// </summary>
    public interface IResourceBarProvider
    {
        void UpdateResourceBar(float current, float max, float percent);
        void SetDraining(bool isDraining);
        void SetRegenerating(bool isRegenerating);
        void OnResourceDepleted();
        void OnResourceFull();
    }
}
