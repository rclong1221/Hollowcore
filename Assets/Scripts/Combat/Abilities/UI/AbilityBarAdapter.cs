using UnityEngine;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Stub MonoBehaviour implementing IAbilityUIProvider.
    /// Logs ability state changes for testing. Replace with actual UI implementation.
    ///
    /// EPIC 18.19 - Phase 7
    /// </summary>
    public class AbilityBarAdapter : MonoBehaviour, IAbilityUIProvider
    {
        [Header("Debug")]
        [SerializeField] private bool _logUpdates = false;

        private void Awake()
        {
            AbilityUIBridgeSystem.RegisterProvider(this);
        }

        private void OnDestroy()
        {
            AbilityUIBridgeSystem.UnregisterProvider(this);
        }

        public void UpdateSlot(int slotIndex, int abilityId, float cooldownRemaining, float cooldownTotal,
            int chargesRemaining, int maxCharges)
        {
            if (_logUpdates && abilityId >= 0 && cooldownRemaining > 0f)
            {
                Debug.Log($"[AbilityBar] Slot {slotIndex}: ability={abilityId} cd={cooldownRemaining:F1}s " +
                    $"charges={chargesRemaining}/{maxCharges}");
            }
        }

        public void UpdateCastBar(bool visible, float progress, string phaseName)
        {
            if (_logUpdates && visible)
            {
                Debug.Log($"[AbilityBar] CastBar: {phaseName} {progress:P0}");
            }
        }

        public void UpdateGCD(float gcdRemaining, float gcdTotal)
        {
            // GCD overlay — stub
        }

        public void ShowError(string message)
        {
            if (_logUpdates)
            {
                Debug.Log($"[AbilityBar] Error: {message}");
            }
        }
    }
}
