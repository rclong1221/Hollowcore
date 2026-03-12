using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace DIG.Combat.Resources.UI
{
    /// <summary>
    /// EPIC 16.8 Phase 5: Static registry for resource bar UI providers.
    /// MonoBehaviours register on enable, unregister on disable.
    /// Same pattern as CombatUIRegistry.
    /// </summary>
    public class ResourceUIRegistry : MonoBehaviour
    {
        public static ResourceUIRegistry Instance { get; private set; }

        private readonly Dictionary<ResourceType, IResourceBarProvider> _bars = new();

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterBar(ResourceType type, IResourceBarProvider provider)
        {
            _bars[type] = provider;
        }

        public void UnregisterBar(ResourceType type)
        {
            _bars.Remove(type);
        }

        public void UpdateBars(ResourcePool pool)
        {
            UpdateBar(pool.Slot0);
            UpdateBar(pool.Slot1);
        }

        private void UpdateBar(ResourceSlot slot)
        {
            if (slot.Type == ResourceType.None) return;
            if (!_bars.TryGetValue(slot.Type, out var provider)) return;

            float percent = slot.Max > 0.001f ? slot.Current / slot.Max : 0f;
            provider.UpdateResourceBar(slot.Current, slot.Max, percent);

            if (slot.Current <= 0.001f)
                provider.OnResourceDepleted();
            else if (math.abs(slot.Current - slot.Max) < 0.001f)
                provider.OnResourceFull();
        }
    }
}
