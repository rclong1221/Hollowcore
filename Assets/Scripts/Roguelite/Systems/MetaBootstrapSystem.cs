using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Loads MetaUnlockTreeSO from Resources/, builds blob,
    /// creates MetaBank singleton entity with unlock buffer and run history.
    /// Runs once at startup, then self-disables. Follows RunConfigBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MetaBootstrapSystem : SystemBase
    {
        private bool _initialized;
        private BlobAssetReference<MetaUnlockBlob> _treeBlob;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            // Load unlock tree from Resources/
            var treeSO = Resources.Load<MetaUnlockTreeSO>("MetaUnlockTree");
            bool hasTree = treeSO != null;

            if (!hasTree)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[MetaBootstrap] No MetaUnlockTreeSO found at Resources/MetaUnlockTree. MetaBank created with empty unlock tree.");
#endif
                treeSO = ScriptableObject.CreateInstance<MetaUnlockTreeSO>();
                treeSO.TreeName = "Empty";
            }

            // Build blob for Burst-readable unlock definitions
            _treeBlob = MetaUnlockBlobBuilder.Build(treeSO);

            // Create unlock tree singleton
            var treeEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(treeEntity, new MetaUnlockTreeSingleton { Tree = _treeBlob });
#if UNITY_EDITOR
            EntityManager.SetName(treeEntity, "MetaUnlockTree");
#endif

            // Create MetaBank singleton entity
            var bankEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(bankEntity, new MetaBank());

            // Add unlock entry buffer — populated from SO definitions
            var unlockBuffer = EntityManager.AddBuffer<MetaUnlockEntry>(bankEntity);
            if (treeSO.Unlocks != null)
            {
                foreach (var def in treeSO.Unlocks)
                {
                    unlockBuffer.Add(new MetaUnlockEntry
                    {
                        UnlockId = def.UnlockId,
                        Category = def.Category,
                        Cost = def.Cost,
                        PrerequisiteId = def.PrerequisiteId,
                        IsUnlocked = false,
                        FloatValue = def.FloatValue,
                        IntValue = def.IntValue
                    });
                }
            }

            // Add run history buffer (empty, populated as runs complete)
            EntityManager.AddBuffer<RunHistoryEntry>(bankEntity);

            // Add unlock request signal (baked disabled)
            EntityManager.AddComponentData(bankEntity, default(MetaUnlockRequest));
            EntityManager.SetComponentEnabled<MetaUnlockRequest>(bankEntity, false);

#if UNITY_EDITOR
            EntityManager.SetName(bankEntity, "MetaBank");
#endif

            Debug.Log($"[MetaBootstrap] MetaBank created with {unlockBuffer.Length} unlocks from '{treeSO.TreeName}'.");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (_treeBlob.IsCreated)
                _treeBlob.Dispose();
        }
    }
}
