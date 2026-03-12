using UnityEngine;
using Audio.Systems;

namespace DIG.Surface.Config
{
    /// <summary>
    /// EPIC 15.24 Phase 9: Maps GroundEffectType to visual assets (decal + VFX prefab).
    /// Create via: Assets > Create > DIG/Surface/Ground Effect Library
    /// </summary>
    [CreateAssetMenu(fileName = "GroundEffectLibrary", menuName = "DIG/Surface/Ground Effect Library")]
    public class GroundEffectLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct GroundEffectEntry
        {
            public GroundEffectType EffectType;

            [Tooltip("Decal spawned on the ground (scorch mark, ice patch, etc.).")]
            public DecalData Decal;

            [Tooltip("Lingering VFX prefab (fire embers, frost crystals, poison bubbles). Optional.")]
            public GameObject LingeringVFXPrefab;

            [Tooltip("Default duration in seconds if not overridden by ability.")]
            public float DefaultDuration;

            [Tooltip("Fade-out duration in seconds at end of lifetime.")]
            public float FadeOutDuration;

            [Tooltip("Minimum decal radius.")]
            public float MinRadius;

            [Tooltip("Maximum decal radius.")]
            public float MaxRadius;
        }

        [Header("Ground Effect Entries")]
        public GroundEffectEntry[] Entries;

        public bool TryGetEntry(GroundEffectType type, out GroundEffectEntry entry)
        {
            if (Entries != null)
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].EffectType == type)
                    {
                        entry = Entries[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }
    }
}
