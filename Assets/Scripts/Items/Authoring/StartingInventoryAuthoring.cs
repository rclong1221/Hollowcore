#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Items
{
    /// <summary>
    /// Authoring component to define starting weapons for a player.
    /// Add this to the Server prefab (e.g., Warrok_Server) and assign weapon prefabs.
    /// The Baker will populate the ItemSetEntry buffer.
    /// </summary>
    public class StartingInventoryAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class StartingWeapon
        {
            [Tooltip("The weapon prefab to add to inventory.")]
            public GameObject WeaponPrefab;

            [Tooltip("Set name for grouping (e.g., 'Primary', 'Secondary').")]
            public string SetName = "Primary";

            [Tooltip("Quick slot for number key switching (1-9). Set to 0 to use Item's default.")]
            [Range(0, 9)]
            public int QuickSlot = 0; // Default to 0 (Auto)

            [Tooltip("Order within the set for cycling.")]
            public int Order = 0;

            [Tooltip("If true, this is the default weapon for its set.")]
            public bool IsDefault = false;
        }

        [Header("Starting Weapons")]
        [Tooltip("List of weapons to add to the player's inventory on spawn.")]
        public List<StartingWeapon> StartingWeapons = new List<StartingWeapon>();

        public class Baker : Baker<StartingInventoryAuthoring>
        {
            private const bool DebugEnabled = false;

            public override void Bake(StartingInventoryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Get or add the buffer
                var buffer = AddBuffer<ItemSetEntry>(entity);

                for (int i = 0; i < authoring.StartingWeapons.Count; i++)
                {
                    var weapon = authoring.StartingWeapons[i];
                    if (weapon.WeaponPrefab == null)
                    {
                        if (DebugEnabled)
                        {
                            Debug.LogWarning($"[DIG.Weapons] StartingInventory: Weapon at index {i} has no prefab assigned.");
                        }
                        continue;
                    }

                    // Convert prefab to entity
                    var weaponEntity = GetEntity(weapon.WeaponPrefab, TransformUsageFlags.Dynamic);

                    // Determine QuickSlot
                    int quickSlot = weapon.QuickSlot;
                    
                    // If 0, try to read from ItemAuthoring on the prefab
                    if (quickSlot == 0)
                    {
                         var itemAuth = weapon.WeaponPrefab.GetComponent<DIG.Items.Authoring.ItemAuthoring>();
                         if (itemAuth != null)
                         {
                             quickSlot = itemAuth.DefaultQuickSlot;
                         }
                    }

                    // Validate and fix QuickSlot (must be 1-9)
                    if (quickSlot < 1 || quickSlot > 9)
                    {
                        quickSlot = i + 1; // Auto-assign based on index (1, 2, 3...)
                        if (DebugEnabled)
                        {
                            Debug.LogWarning($"[DIG.Weapons] StartingInventory: Invalid QuickSlot {weapon.QuickSlot} for '{weapon.WeaponPrefab.name}', auto-assigned to {quickSlot}");
                        }
                    }

                    buffer.Add(new ItemSetEntry
                    {
                        SetName = new FixedString32Bytes(weapon.SetName),
                        ItemEntity = weaponEntity,
                        Order = weapon.Order,
                        QuickSlot = quickSlot,
                        IsDefault = weapon.IsDefault
                    });

                    if (DebugEnabled)
                    {
                        Debug.Log($"[DIG.Weapons] StartingInventory: Baked weapon '{weapon.WeaponPrefab.name}' to QuickSlot {quickSlot}");
                    }
                }
            }
        }
    }
}
