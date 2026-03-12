using UnityEngine;
using System.Collections.Generic;

namespace DIG.Weapons.Data
{
    /// <summary>
    /// EPIC 15.5: Registry for recoil patterns.
    /// Maps pattern indices to RecoilPatternAsset ScriptableObjects.
    /// </summary>
    public class RecoilPatternRegistry : MonoBehaviour
    {
        public static RecoilPatternRegistry Instance { get; private set; }

        [Header("Patterns")]
        [Tooltip("List of recoil patterns indexed by their position")]
        [SerializeField] private List<RecoilPatternAsset> patterns = new List<RecoilPatternAsset>();

        [Header("Defaults")]
        [Tooltip("Default pattern used when index is invalid")]
        [SerializeField] private RecoilPatternAsset defaultPattern;

        // Quick lookup by name
        private Dictionary<string, int> _patternNameToIndex = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildNameIndex();
            RegisterWithSystem();
        }

        private void BuildNameIndex()
        {
            _patternNameToIndex.Clear();

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i] != null)
                {
                    _patternNameToIndex[patterns[i].name] = i;
                }
            }
        }

        private void RegisterWithSystem()
        {
            // Register with the ECS system
            DIG.Weapons.Systems.PatternRecoilSystem.SetRegistry(this);
        }

        /// <summary>
        /// Get a recoil pattern by index.
        /// </summary>
        public RecoilPatternAsset GetPattern(int index)
        {
            if (index >= 0 && index < patterns.Count && patterns[index] != null)
            {
                return patterns[index];
            }

            return defaultPattern;
        }

        /// <summary>
        /// Get a recoil pattern by name.
        /// </summary>
        public RecoilPatternAsset GetPatternByName(string name)
        {
            if (_patternNameToIndex.TryGetValue(name, out int index))
            {
                return patterns[index];
            }

            return defaultPattern;
        }

        /// <summary>
        /// Get the index for a pattern name.
        /// </summary>
        public int GetPatternIndex(string name)
        {
            if (_patternNameToIndex.TryGetValue(name, out int index))
            {
                return index;
            }

            return -1;
        }

        /// <summary>
        /// Register a new pattern at runtime.
        /// </summary>
        public int RegisterPattern(RecoilPatternAsset pattern)
        {
            if (pattern == null) return -1;

            // Check if already registered
            if (_patternNameToIndex.TryGetValue(pattern.name, out int existingIndex))
            {
                return existingIndex;
            }

            int index = patterns.Count;
            patterns.Add(pattern);
            _patternNameToIndex[pattern.name] = index;

            return index;
        }

        /// <summary>
        /// Get total number of registered patterns.
        /// </summary>
        public int PatternCount => patterns.Count;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                DIG.Weapons.Systems.PatternRecoilSystem.SetRegistry(null);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Name Index")]
        private void EditorRebuildIndex()
        {
            BuildNameIndex();
            Debug.Log($"[RecoilPatternRegistry] Rebuilt index with {_patternNameToIndex.Count} patterns");
        }

        [ContextMenu("List All Patterns")]
        private void EditorListPatterns()
        {
            Debug.Log($"=== Registered Recoil Patterns ({patterns.Count}) ===");
            for (int i = 0; i < patterns.Count; i++)
            {
                var p = patterns[i];
                if (p != null)
                {
                    Debug.Log($"  [{i}] {p.name} (Difficulty: {p.CalculatePatternDifficulty():F2})");
                }
                else
                {
                    Debug.Log($"  [{i}] <null>");
                }
            }
        }
#endif
    }
}
