using Unity.Entities;
using UnityEngine;
using Player.Components;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player.Authoring
{
    // T1: Hitbox Dummy Helper (Editor Tool)
    // Use the Context Menu "Setup Hitboxes" to automatically attach HitboxAuthoring components
    public class HitboxDummyAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct HitboxMapping
        {
            public string BoneName; // e.g., "Head"
            public float Multiplier;
            public HitboxRegion Region;
        }

        public HitboxMapping[] Mappings = new HitboxMapping[]
        {
            new HitboxMapping { BoneName = "Head", Multiplier = 2.0f, Region = HitboxRegion.Head },
            new HitboxMapping { BoneName = "Spine", Multiplier = 1.0f, Region = HitboxRegion.Torso },
            new HitboxMapping { BoneName = "Arm", Multiplier = 0.75f, Region = HitboxRegion.Arms },
            new HitboxMapping { BoneName = "Leg", Multiplier = 0.5f, Region = HitboxRegion.Legs },
        };

        [ContextMenu("Setup Hitboxes")]
        public void SetupHitboxes()
        {
#if UNITY_EDITOR
            // Ensure we have a HitboxOwnerMarker on root
            if (GetComponent<HitboxOwnerMarker>() == null)
            {
                Undo.AddComponent<HitboxOwnerMarker>(gameObject);
            }

            foreach (var mapping in Mappings)
            {
                Transform found = FindRecursive(transform, mapping.BoneName);
                if (found != null)
                {
                    // Check if it has a collider
                    if (found.GetComponent<Collider>() == null)
                    {
                        // Add a Capsule Collider if missing (simple guess)
                        // Or just assume user will add it. User T1 says "Hitbox components to child bones".
                        // Usually requires collider. Let's strictly add HitboxAuthoring.
                    }

                    HitboxAuthoring hitboxAuth = found.GetComponent<HitboxAuthoring>();
                    if (hitboxAuth == null)
                    {
                        hitboxAuth = Undo.AddComponent<HitboxAuthoring>(found.gameObject);
                    }

                    hitboxAuth.DamageMultiplier = mapping.Multiplier;
                    hitboxAuth.Region = mapping.Region;
                    EditorUtility.SetDirty(found.gameObject);
                }
            }
            Debug.Log($"Setup Hitboxes for {name}");
#endif
        }

        Transform FindRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) return child;
                var result = FindRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
