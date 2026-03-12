using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for weapon shell ejection.
    /// Add to any weapon prefab that ejects shells.
    /// References a shell type by ID (registered in ShellPrefabRegistry).
    /// </summary>
    public class WeaponShellAuthoring : MonoBehaviour
    {
        [Tooltip("Shell type ID matching an entry in ShellPrefabRegistryAuthoring (e.g., 'AssaultRifle', 'Pistol')")]
        public string ShellTypeID = "AssaultRifle";
        
        [Tooltip("Optional: Override shell prefab for runtime GameObject spawning (legacy fallback)")]
        public GameObject ShellPrefab;
        
        [Tooltip("Lifetime if using legacy spawning")]
        public float ShellLifetime = 5f;
    }
}
