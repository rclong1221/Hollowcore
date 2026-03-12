using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Loads AbilityLoadoutSO from Resources/ and bakes it into a BlobAsset.
    /// Creates the AbilityDatabaseRef singleton on startup.
    ///
    /// Gracefully falls back to an empty database if no loadout is found.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation
                     | WorldSystemFilterFlags.ClientSimulation
                     | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AbilityDatabaseBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnCreate()
        {
            _initialized = false;
        }

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            // Try to load from Resources
            var loadout = UnityEngine.Resources.Load<AbilityLoadoutSO>("AbilityLoadout");

            BlobAssetReference<AbilityDatabaseBlob> blobRef;

            if (loadout != null && loadout.abilities != null && loadout.abilities.Length > 0)
            {
                blobRef = loadout.BakeToBlob();
                Debug.Log($"[AbilityDatabase] Loaded {loadout.abilities.Length} ability definitions from Resources/AbilityLoadout");
            }
            else
            {
                // Create empty database
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<AbilityDatabaseBlob>();
                builder.Allocate(ref root.Abilities, 0);
                blobRef = builder.CreateBlobAssetReference<AbilityDatabaseBlob>(Allocator.Persistent);
                builder.Dispose();

                if (loadout == null)
                    Debug.Log("[AbilityDatabase] No AbilityLoadout found in Resources/. Using empty database.");
                else
                    Debug.Log("[AbilityDatabase] AbilityLoadout found but empty. Using empty database.");
            }

            // Create singleton
            var entity = EntityManager.CreateEntity(typeof(AbilityDatabaseRef));
            EntityManager.SetName(entity, "AbilityDatabaseRef");
            EntityManager.SetComponentData(entity, new AbilityDatabaseRef { Value = blobRef });

            // Self-disable to avoid running again
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            // Cleanup blob
            if (SystemAPI.TryGetSingleton<AbilityDatabaseRef>(out var dbRef) && dbRef.Value.IsCreated)
            {
                dbRef.Value.Dispose();
            }
        }
    }
}
