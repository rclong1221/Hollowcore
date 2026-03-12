using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Provider interface for game-specific run HUD.
    /// Games implement this to display run phase, timer, score, etc.
    /// </summary>
    public interface IRunUIProvider
    {
        void OnPhaseChanged(RunPhase newPhase, RunPhase previousPhase);
        void OnRunStart(uint runId, uint seed, byte maxZones);
        void OnZoneChanged(byte zoneIndex, uint zoneSeed);
        void OnRunEnd(RunEndReason reason, int finalScore, int runCurrency, int zonesCleared);
        void UpdateHUD(float elapsedTime, int score, int runCurrency, byte currentZone, byte maxZones);
    }

    /// <summary>
    /// EPIC 23.1: Central registry for run UI providers.
    /// Follows CombatUIRegistry pattern: register/unregister with replacement warning.
    /// </summary>
    public static class RunUIRegistry
    {
        private static IRunUIProvider _provider;

        public static IRunUIProvider Provider => _provider;
        public static bool HasProvider => _provider != null;

        public static void Register(IRunUIProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[RunUIRegistry] Replacing existing run UI provider.");
            _provider = provider;
        }

        public static void Unregister(IRunUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _provider = null;
        }
    }
}
