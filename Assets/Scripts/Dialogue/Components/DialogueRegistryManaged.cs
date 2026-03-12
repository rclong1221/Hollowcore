using System.Collections.Generic;
using Unity.Entities;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Managed singleton holding dialogue database references.
    /// Created by DialogueBootstrapSystem. Used by managed systems for
    /// SO-based lookups (localization keys, audio paths) that blobs cannot store.
    /// </summary>
    public class DialogueRegistryManaged : IComponentData
    {
        public DialogueDatabaseSO Database;
        public Dictionary<int, DialogueTreeSO> TreeLookup;
        public Dictionary<int, BarkCollectionSO> BarkLookup;

        // EPIC 18.5: Speaker profile lookup by name hash
        public Dictionary<int, DialogueSpeakerProfileSO> SpeakerProfileLookup;

        public DialogueTreeSO GetTree(int treeId)
        {
            if (TreeLookup != null && TreeLookup.TryGetValue(treeId, out var tree))
                return tree;
            return null;
        }

        public BarkCollectionSO GetBarkCollection(int barkId)
        {
            if (BarkLookup != null && BarkLookup.TryGetValue(barkId, out var bark))
                return bark;
            return null;
        }

        /// <summary>
        /// EPIC 18.5: Returns the speaker profile matching the given speaker name, or null.
        /// </summary>
        public DialogueSpeakerProfileSO GetSpeakerProfile(string speakerName)
        {
            if (SpeakerProfileLookup == null || string.IsNullOrEmpty(speakerName))
                return null;
            int hash = speakerName.GetHashCode();
            SpeakerProfileLookup.TryGetValue(hash, out var profile);
            return profile;
        }
    }
}
