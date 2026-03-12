using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Loads RunModifierPoolSO and AscensionDefinitionSO from Resources/,
    /// builds blobs, creates ModifierRegistrySingleton, AscensionSingleton, and
    /// RuntimeDifficultyScale entities. Runs once at startup, then self-disables.
    /// Follows RunConfigBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ModifierBootstrapSystem : SystemBase
    {
        private bool _initialized;
        private BlobAssetReference<ModifierRegistryBlob> _registryBlob;
        private BlobAssetReference<AscensionBlob> _ascensionBlob;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            // Load modifier pool from Resources/
            var poolSO = Resources.Load<RunModifierPoolSO>("RunModifierPool");
            bool hasPool = poolSO != null;

            if (!hasPool)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[ModifierBootstrap] No RunModifierPoolSO found at Resources/RunModifierPool. Registry created empty.");
#endif
                // Build empty blob without creating a leaked ScriptableObject
                var emptyPool = ScriptableObject.CreateInstance<RunModifierPoolSO>();
                emptyPool.PoolName = "Empty";
                _registryBlob = ModifierRegistryBlobBuilder.Build(emptyPool);
                Object.DestroyImmediate(emptyPool);
            }
            else
            {
                _registryBlob = ModifierRegistryBlobBuilder.Build(poolSO);
            }

            var registryEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(registryEntity, new ModifierRegistrySingleton { Registry = _registryBlob });
#if UNITY_EDITOR
            EntityManager.SetName(registryEntity, "ModifierRegistry");
#endif

            // Load ascension definition from Resources/
            var ascensionSO = Resources.Load<AscensionDefinitionSO>("AscensionDefinition");
            bool hasAscension = ascensionSO != null;

            if (!hasAscension)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[ModifierBootstrap] No AscensionDefinitionSO found at Resources/AscensionDefinition. Ascension disabled.");
#endif
                var emptyAscension = ScriptableObject.CreateInstance<AscensionDefinitionSO>();
                emptyAscension.DefinitionName = "Empty";
                _ascensionBlob = ModifierRegistryBlobBuilder.BuildAscension(emptyAscension);
                Object.DestroyImmediate(emptyAscension);
            }
            else
            {
                _ascensionBlob = ModifierRegistryBlobBuilder.BuildAscension(ascensionSO);
            }

            var ascensionEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ascensionEntity, new AscensionSingleton { Ascension = _ascensionBlob });
#if UNITY_EDITOR
            EntityManager.SetName(ascensionEntity, "AscensionDefinition");
#endif

            // Create RuntimeDifficultyScale singleton
            var difficultyEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(difficultyEntity, RuntimeDifficultyScale.Default);
#if UNITY_EDITOR
            EntityManager.SetName(difficultyEntity, "RuntimeDifficultyScale");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int modCount = hasPool ? poolSO.Modifiers?.Count ?? 0 : 0;
            int tierCount = hasAscension ? ascensionSO.Tiers?.Count ?? 0 : 0;
            string poolName = hasPool ? poolSO.PoolName : "Empty";
            Debug.Log($"[ModifierBootstrap] Registry: {modCount} modifiers from '{poolName}'. Ascension: {tierCount} tiers.");
#endif

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (_registryBlob.IsCreated)
                _registryBlob.Dispose();
            if (_ascensionBlob.IsCreated)
                _ascensionBlob.Dispose();
        }
    }
}
