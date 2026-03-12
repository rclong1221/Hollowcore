#if UNITY_EDITOR
using UnityEditor;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.7: Invalidates the shared RogueliteDataContext when roguelite SOs are
    /// created, deleted, moved, or reimported. The context rebuilds lazily on next access.
    /// </summary>
    public class RogueliteAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Shared context instance. Set by RunWorkstationWindow.
        /// If null, no invalidation needed (window not open).
        /// </summary>
        public static RogueliteDataContext SharedContext;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (SharedContext == null || !SharedContext.IsBuilt) return;

            // Check if any changed asset is in a roguelite-relevant path
            for (int i = 0; i < importedAssets.Length; i++)
                if (IsRogueliteAsset(importedAssets[i])) { SharedContext.Invalidate(); return; }
            for (int i = 0; i < deletedAssets.Length; i++)
                if (IsRogueliteAsset(deletedAssets[i])) { SharedContext.Invalidate(); return; }
            for (int i = 0; i < movedAssets.Length; i++)
                if (IsRogueliteAsset(movedAssets[i])) { SharedContext.Invalidate(); return; }
        }

        private static bool IsRogueliteAsset(string path)
        {
            // Match any .asset file in roguelite-related directories
            if (!path.EndsWith(".asset")) return false;
            return path.Contains("Roguelite") || path.Contains("Rewards") || path.Contains("Zones")
                || path.Contains("RunConfig") || path.Contains("Zone") || path.Contains("Encounter")
                || path.Contains("Reward") || path.Contains("Modifier") || path.Contains("Ascension")
                || path.Contains("MetaUnlock") || path.Contains("Interactable") || path.Contains("SpawnDirector");
        }
    }
}
#endif
