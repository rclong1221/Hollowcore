using Hollowcore.Chassis.Definitions;
using Unity.Entities;
using UnityEngine;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// Loads all LimbDefinitionSO from Resources and bakes them into a BlobAsset database.
    /// Creates a singleton entity with LimbDatabaseReference for Burst-safe lookups.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(ChassisBootstrapSystem))]
    public partial class LimbDatabaseBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var allDefinitions = Resources.LoadAll<LimbDefinitionSO>("Chassis/Limbs");

            if (allDefinitions == null || allDefinitions.Length == 0)
            {
                Debug.LogWarning("[LimbDatabaseBootstrap] No LimbDefinitionSO found in Resources/Chassis/Limbs/. " +
                                 "Chassis system will operate with no starting limbs.");
                Enabled = false;
                return;
            }

            Debug.Log($"[LimbDatabaseBootstrap] Baking {allDefinitions.Length} limb definitions to BlobAsset.");

            var blobRef = LimbDefinitionSO.BakeDatabaseToBlob(allDefinitions);

            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(singletonEntity, new LimbDatabaseReference { Value = blobRef });
            EntityManager.SetName(singletonEntity, "LimbDatabase");

            // Create runtime config singleton with defaults
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, ChassisRuntimeConfig.Default);
            EntityManager.AddComponent<ChassisConfigDirty>(configEntity);
            EntityManager.SetComponentEnabled<ChassisConfigDirty>(configEntity, false);
            EntityManager.SetName(configEntity, "ChassisRuntimeConfig");

            Enabled = false;
        }

        protected override void OnDestroy()
        {
            // Clean up blob asset
            if (SystemAPI.TryGetSingleton<LimbDatabaseReference>(out var dbRef))
            {
                if (dbRef.Value.IsCreated)
                    dbRef.Value.Dispose();
            }
        }
    }
}
