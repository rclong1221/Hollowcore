using System.Collections.Generic;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Central registry of all dialogue trees.
    /// Loaded from Resources/DialogueDatabase by DialogueBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "DIG/Dialogue/Dialogue Database", order = 1)]
    public class DialogueDatabaseSO : ScriptableObject
    {
        public List<DialogueTreeSO> Trees = new();

        private Dictionary<int, DialogueTreeSO> _lookup;

        public DialogueTreeSO GetTree(int treeId)
        {
            BuildLookup();
            return _lookup.TryGetValue(treeId, out var tree) ? tree : null;
        }

        public bool HasTree(int treeId)
        {
            BuildLookup();
            return _lookup.ContainsKey(treeId);
        }

        private void BuildLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<int, DialogueTreeSO>(Trees.Count);
            foreach (var tree in Trees)
            {
                if (tree == null) continue;
                if (_lookup.ContainsKey(tree.TreeId))
                {
                    Debug.LogWarning($"[DialogueDatabase] Duplicate TreeId {tree.TreeId}: {tree.name}");
                    continue;
                }
                _lookup[tree.TreeId] = tree;
            }
        }

        private void OnValidate()
        {
            _lookup = null;
        }
    }
}
