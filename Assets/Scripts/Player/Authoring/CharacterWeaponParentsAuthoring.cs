using Unity.Entities;
using UnityEngine;
using DIG.Items;
using Opsive.UltimateCharacterController.Objects;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Authoring component that registers all weapon parent transforms on the character.
    /// These are the specific attachment points (AssaultRifleParent, PistolParent, etc.)
    /// that Opsive uses for per-weapon-category positioning.
    ///
    /// Should be placed on the character prefab root. References the Items containers
    /// under each hand where weapon-specific parents are located.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterWeaponParentsAuthoring : MonoBehaviour
    {
        [Header("Items Containers")]
        [Tooltip("Reference to the Items container under the right hand (contains AssaultRifleParent, PistolParent, etc.)")]
        public Transform RightHandItemsContainer;

        [Tooltip("Reference to the Items container under the left hand (contains ShieldParent, BowParent, etc.)")]
        public Transform LeftHandItemsContainer;

        [Header("Debug")]
        [Tooltip("Log discovered weapon parents during baking")]
        public bool DebugLogging = false;

        public class Baker : Baker<CharacterWeaponParentsAuthoring>
        {
            public override void Bake(CharacterWeaponParentsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<WeaponParentElement>(entity);

                int count = 0;

                // Register all weapon parents from right hand
                if (authoring.RightHandItemsContainer != null)
                {
                    count += RegisterChildParents(authoring.RightHandItemsContainer, buffer, authoring.DebugLogging, "RightHand");
                }

                // Register all weapon parents from left hand
                if (authoring.LeftHandItemsContainer != null)
                {
                    count += RegisterChildParents(authoring.LeftHandItemsContainer, buffer, authoring.DebugLogging, "LeftHand");
                }

                // Add tag component if we found any parents
                if (count > 0)
                {
                    AddComponent<HasWeaponParents>(entity);
                    if (authoring.DebugLogging)
                    {
                        Debug.Log($"[CharacterWeaponParentsBaker] Registered {count} weapon parents on {authoring.name}");
                    }
                }
            }

            private int RegisterChildParents(Transform container, DynamicBuffer<WeaponParentElement> buffer, bool debugLogging, string handName)
            {
                int count = 0;

                foreach (Transform child in container)
                {
                    var identifier = child.GetComponent<ObjectIdentifier>();
                    if (identifier != null && identifier.ID != 0)
                    {
                        buffer.Add(new WeaponParentElement
                        {
                            ObjectIdentifierID = identifier.ID,
                            TransformInstanceID = child.GetInstanceID()
                        });

                        if (debugLogging)
                        {
                            Debug.Log($"[CharacterWeaponParentsBaker] [{handName}] Registered: {child.name} (ID: {identifier.ID})");
                        }

                        count++;
                    }
                }

                return count;
            }
        }
    }
}
