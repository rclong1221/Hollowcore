using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Authoring component for NPCs that can participate in dialogue.
    /// Place alongside InteractableAuthoring with InteractionVerb.Talk.
    /// Baker adds DialogueSpeakerData, DialogueSessionState, DialogueFlag buffer,
    /// and optionally BarkEmitter.
    /// </summary>
    [AddComponentMenu("DIG/Dialogue/Dialogue Speaker")]
    public class DialogueSpeakerAuthoring : MonoBehaviour
    {
        [Header("Dialogue")]
        [Tooltip("Default dialogue tree used when no context rules match.")]
        public DialogueTreeSO DefaultTree;

        [Tooltip("Localization key for proximity greeting text.")]
        public string GreetingLocKey;

        [Tooltip("Conditional overrides — first matching rule selects the tree.")]
        public List<DialogueContextRule> ContextRules = new();

        [Header("Barks")]
        [Tooltip("Ambient bark collection. Leave empty if NPC has no barks.")]
        public BarkCollectionSO BarkCollection;
    }

    public class DialogueSpeakerBaker : Baker<DialogueSpeakerAuthoring>
    {
        public override void Bake(DialogueSpeakerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Build context rules blob
            BlobAssetReference<BlobArray<DialogueContextEntry>> contextBlob = default;
            if (authoring.ContextRules != null && authoring.ContextRules.Count > 0)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BlobArray<DialogueContextEntry>>();
                var entries = builder.Allocate(ref root, authoring.ContextRules.Count);
                for (int i = 0; i < authoring.ContextRules.Count; i++)
                {
                    var rule = authoring.ContextRules[i];
                    entries[i] = new DialogueContextEntry
                    {
                        ConditionType = (byte)rule.ConditionType,
                        ConditionValue = rule.ConditionValue,
                        TreeId = rule.Tree != null ? rule.Tree.TreeId : 0
                    };
                }
                contextBlob = builder.CreateBlobAssetReference<BlobArray<DialogueContextEntry>>(
                    Allocator.Persistent);
                builder.Dispose();
                AddBlobAsset(ref contextBlob, out _);
            }

            // Speaker data
            var greeting = new FixedString64Bytes();
            if (!string.IsNullOrEmpty(authoring.GreetingLocKey))
                greeting = new FixedString64Bytes(authoring.GreetingLocKey);

            AddComponent(entity, new DialogueSpeakerData
            {
                DefaultTreeId = authoring.DefaultTree != null ? authoring.DefaultTree.TreeId : 0,
                GreetingText = greeting,
                ContextRules = contextBlob,
                BarkCollectionId = authoring.BarkCollection != null ? authoring.BarkCollection.BarkId : 0
            });

            // Session state (inactive by default)
            AddComponent(entity, new DialogueSessionState());

            // Dialogue flags buffer
            AddBuffer<DialogueFlag>(entity);

            // Bark emitter (if bark collection assigned)
            if (authoring.BarkCollection != null)
            {
                AddComponent(entity, new BarkEmitter
                {
                    BarkCollectionId = authoring.BarkCollection.BarkId,
                    LastBarkTime = 0f,
                    BarkCooldown = authoring.BarkCollection.Cooldown
                });
            }
        }
    }
}
