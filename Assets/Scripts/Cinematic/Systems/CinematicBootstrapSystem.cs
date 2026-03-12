using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Loads CinematicDatabaseSO from Resources/,
    /// creates CinematicState singleton, CinematicConfigSingleton,
    /// and CinematicRegistryManaged. Runs once at startup then self-disables.
    /// Follows AchievementBootstrapSystem / ProgressionBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CinematicBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var databaseSO = Resources.Load<CinematicDatabaseSO>("CinematicDatabase");

            if (databaseSO == null)
            {
                Debug.LogWarning("[Cinematic] No CinematicDatabaseSO found at Resources/CinematicDatabase. Cinematic system disabled.");
                _initialized = true;
                Enabled = false;
                return;
            }

            // Create singleton entity
            var entity = EntityManager.CreateEntity();

            // CinematicState singleton (idle)
            EntityManager.AddComponentData(entity, new CinematicState
            {
                IsPlaying = false,
                CurrentCinematicId = 0,
                ElapsedTime = 0f,
                CanSkip = false,
                SkipVotesReceived = 0,
                TotalPlayersInScene = 0,
                CinematicType = CinematicType.FullCinematic,
                Duration = 0f,
                BlendProgress = 0f
            });

            // Config singleton from database defaults
            EntityManager.AddComponentData(entity, new CinematicConfigSingleton
            {
                DefaultSkipPolicy = databaseSO.DefaultSkipPolicy,
                BlendInDuration = databaseSO.BlendInDuration,
                BlendOutDuration = databaseSO.BlendOutDuration,
                HUDFadeDuration = databaseSO.HUDFadeDuration,
                LetterboxHeight = databaseSO.LetterboxHeight
            });

            // Build managed registry
            var registry = new CinematicRegistryManaged
            {
                Definitions = new Dictionary<int, CinematicDefinitionSO>(),
                IsInitialized = true
            };

            for (int i = 0; i < databaseSO.Cinematics.Count; i++)
            {
                var def = databaseSO.Cinematics[i];
                if (def == null)
                {
                    Debug.LogWarning($"[Cinematic] Null entry at index {i} in CinematicDatabaseSO");
                    continue;
                }
                if (!registry.Definitions.TryAdd(def.CinematicId, def))
                {
                    Debug.LogWarning($"[Cinematic] Duplicate CinematicId {def.CinematicId} at index {i}: '{def.Name}'");
                }
            }

            EntityManager.AddComponentObject(entity, registry);

#if UNITY_EDITOR
            EntityManager.SetName(entity, "CinematicRegistry");
#endif

            // Initialize event queue
            CinematicAnimEventQueue.Initialize();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Cinematic] Registered {registry.Definitions.Count} cinematics");
#endif

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            CinematicAnimEventQueue.Dispose();
        }
    }
}
