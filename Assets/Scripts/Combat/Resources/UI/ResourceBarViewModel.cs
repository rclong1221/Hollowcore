using UnityEngine;

namespace DIG.Combat.Resources.UI
{
    /// <summary>
    /// EPIC 16.8 Phase 5: Generic resource bar view model (same pattern as StaminaViewModel).
    /// Configurable ResourceType in inspector. Registers with ResourceUIRegistry.
    /// Exposes UI-friendly properties and fires OnChanged event.
    /// </summary>
    public class ResourceBarViewModel : MonoBehaviour, IResourceBarProvider
    {
        [SerializeField] private ResourceType _resourceType = ResourceType.Mana;
        [SerializeField] private float _lowThreshold = 0.2f;
        [SerializeField] private float _emptyThreshold = 0.05f;

        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }
        public bool IsDraining { get; private set; }
        public bool IsRecovering { get; private set; }
        public bool IsLow { get; private set; }
        public bool IsEmpty { get; private set; }
        public ResourceType ResourceType => _resourceType;

        public event System.Action<ResourceBarViewModel> OnChanged;

        private void OnEnable()
        {
            if (ResourceUIRegistry.Instance != null)
                ResourceUIRegistry.Instance.RegisterBar(_resourceType, this);
        }

        private void OnDisable()
        {
            if (ResourceUIRegistry.Instance != null)
                ResourceUIRegistry.Instance.UnregisterBar(_resourceType);
        }

        public void UpdateResourceBar(float current, float max, float percent)
        {
            Current = current;
            Max = max;
            Percent = percent;
            IsLow = percent <= _lowThreshold && percent > _emptyThreshold;
            IsEmpty = percent <= _emptyThreshold;
            OnChanged?.Invoke(this);
        }

        public void SetDraining(bool isDraining) => IsDraining = isDraining;
        public void SetRegenerating(bool isRegenerating) => IsRecovering = isRegenerating;
        public void OnResourceDepleted() { IsEmpty = true; }
        public void OnResourceFull() { IsEmpty = false; IsLow = false; }
    }
}
