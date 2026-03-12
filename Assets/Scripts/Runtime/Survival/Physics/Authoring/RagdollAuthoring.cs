using UnityEngine;
using Unity.Entities;
using DIG.Survival.Physics;

namespace DIG.Survival.Physics.Authoring
{
    public class RagdollAuthoring : MonoBehaviour
    {
        [Tooltip("The root bone (Pelvis) of the ragdoll hierarchy.")]
        public GameObject Pelvis;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Pelvis != null)
            {
                AddBoneAuthoringRecursive(Pelvis.transform);
            }
        }

        private void AddBoneAuthoringRecursive(Transform t)
        {
            if (t.GetComponent<Rigidbody>() != null && t.GetComponent<RagdollBoneAuthoring>() == null)
            {
                t.gameObject.AddComponent<RagdollBoneAuthoring>();
            }
            foreach (Transform child in t)
            {
                AddBoneAuthoringRecursive(child);
            }
        }
#endif
    }

    public class RagdollBoneAuthoring : MonoBehaviour 
    {
    }

    public class RagdollAuthoringBaker : Baker<RagdollAuthoring>
    {
        public override void Bake(RagdollAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var pelvisEntity = GetEntity(authoring.Pelvis, TransformUsageFlags.Dynamic);

            AddComponent(entity, new RagdollController
            {
                IsRagdolled = false,
                Pelvis = pelvisEntity
            });

            // Ensure LinkedEntityGroup exists and includes Root + Bones
            var linkedGroup = AddBuffer<LinkedEntityGroup>(entity);
            // Root is automatically added if we add the buffer? 
            // Usually Unity manages LinkedEntityGroup if present.
            // But if we manually add it, we might need to populate it.
            // However, relying on standard Baking to populate LinkedEntityGroup for children might fail if not Prefab.
            // We'll manually ensure bones are in it.
            // Note: If Unity ALREADY added it, AddBuffer returns logic?
            // "AddBuffer" gets or creates.
            
            // Standard convention: Root is first.
            if (linkedGroup.Length == 0)
                linkedGroup.Add(new LinkedEntityGroup { Value = entity });

            // Recursive bake bones and add to LinkedEntityGroup
            // Note: We cannot AddComponent to bones here, but we can access their entities for the group.
            PopulateLinkedGroup(authoring.Pelvis, linkedGroup);
        }

        private void PopulateLinkedGroup(GameObject go, DynamicBuffer<LinkedEntityGroup> linkedGroup)
        {
            if (go == null) return;

            // If the GameObject has a Rigidbody (or RagdollBoneAuthoring), it is a bone of interest
            if (go.GetComponent<Rigidbody>() != null)
            {
                var boneEntity = GetEntity(go, TransformUsageFlags.Dynamic);
                
                // Add to LinkedEntityGroup if not already present (O(N) check but simple for authoring)
                // Actually, duplicate entries in LinkedEntityGroup are bad.
                // We'll assume DFS traversal adds once.
                
                // Check uniqueness?
                bool exists = false;
                for(int i=0; i<linkedGroup.Length; ++i) 
                    if (linkedGroup[i].Value == boneEntity) { exists = true; break; }
                
                if (!exists)
                    linkedGroup.Add(new LinkedEntityGroup { Value = boneEntity });
            }

            // Recurse
            foreach(Transform child in go.transform)
            {
                PopulateLinkedGroup(child.gameObject, linkedGroup);
            }
        }
    }

    public class RagdollBoneAuthoringBaker : Baker<RagdollBoneAuthoring>
    {
        public override void Bake(RagdollBoneAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Add RagdollBone component
            AddComponent(entity, new RagdollBone 
            { 
                IsActive = false, // Starts inactive until ragdoll triggers
                OriginalParent = Entity.Null // Will be set at runtime by RagdollTransitionSystem
            });
            
            // CRITICAL: Add PhysicsWorldIndex so bones collide with terrain chunks
            // Terrain chunks use PhysicsWorldIndex { Value = 0 }
            // PhysicsWorldIndex is a shared component
            AddSharedComponent(entity, new Unity.Physics.PhysicsWorldIndex { Value = 0 });
        }
    }
}
