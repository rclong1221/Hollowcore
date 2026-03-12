using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Loads DialogueDatabaseSO and BarkCollectionSOs from Resources,
    /// creates managed singleton and DialogueConfig. Disables self after first update.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DialogueBootstrapSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Load database
            var database = Resources.Load<DialogueDatabaseSO>("DialogueDatabase");
            if (database == null)
            {
                Debug.LogWarning("[DialogueBootstrap] No DialogueDatabase found in Resources/. Dialogue system disabled.");
                Enabled = false;
                return;
            }

            // Build tree lookup
            var treeLookup = new Dictionary<int, DialogueTreeSO>(database.Trees.Count);
            foreach (var tree in database.Trees)
            {
                if (tree == null) continue;
                treeLookup[tree.TreeId] = tree;
            }

            // Load bark collections
            var barkAssets = Resources.LoadAll<BarkCollectionSO>("BarkCollections");
            var barkLookup = new Dictionary<int, BarkCollectionSO>(barkAssets.Length);
            foreach (var bark in barkAssets)
            {
                if (bark == null) continue;
                barkLookup[bark.BarkId] = bark;
            }

            // EPIC 18.5: Load speaker profiles
            var speakerAssets = Resources.LoadAll<DialogueSpeakerProfileSO>("SpeakerProfiles");
            var speakerLookup = new Dictionary<int, DialogueSpeakerProfileSO>(speakerAssets.Length);
            foreach (var sp in speakerAssets)
            {
                if (sp == null) continue;
                // Guard against zero hash (uninitialized SpeakerName) — recompute defensively
                int hash = sp.SpeakerNameHash;
                if (hash == 0 && !string.IsNullOrEmpty(sp.SpeakerName))
                    hash = Animator.StringToHash(sp.SpeakerName);
                if (hash == 0) continue; // Skip profiles with no valid speaker name
                speakerLookup[hash] = sp;
            }

            // Create managed singleton
            var registry = new DialogueRegistryManaged
            {
                Database = database,
                TreeLookup = treeLookup,
                BarkLookup = barkLookup,
                SpeakerProfileLookup = speakerLookup
            };
            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(singletonEntity, registry);
            EntityManager.SetName(singletonEntity, "DialogueRegistryManaged");

            // Load config
            var configSO = Resources.Load<DialogueConfigSO>("DialogueConfig");
            var config = new DialogueConfig
            {
                MaxSessionDurationTicks = configSO != null ? configSO.MaxSessionDurationTicks : 1800,
                MaxFlagsPerNpc = configSO != null ? configSO.MaxFlagsPerNpc : (byte)16,
                AutoAdvanceEnabled = configSO == null || configSO.AutoAdvanceEnabled,
                BarkProximityRange = configSO != null ? configSO.BarkProximityRange : 8f,
                BarkCheckInterval = configSO != null ? configSO.BarkCheckInterval : 2f,
                BarkCheckFrameSpread = configSO != null ? configSO.BarkCheckFrameSpread : 10,
                // EPIC 18.5
                TypewriterCharsPerSecond = configSO != null ? configSO.TypewriterCharsPerSecond : 40f,
                PausePeriod = configSO != null ? configSO.PausePeriod : 0.3f,
                PauseComma = configSO != null ? configSO.PauseComma : 0.15f,
                PauseExclamation = configSO != null ? configSO.PauseExclamation : 0.25f,
                HistoryCapacity = configSO != null ? configSO.HistoryCapacity : 50
            };
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, config);
            EntityManager.SetName(configEntity, "DialogueConfig");

            Debug.Log($"[DialogueBootstrap] Loaded {treeLookup.Count} dialogue trees, {barkLookup.Count} bark collections, {speakerLookup.Count} speaker profiles.");
            Enabled = false;
        }
    }
}
