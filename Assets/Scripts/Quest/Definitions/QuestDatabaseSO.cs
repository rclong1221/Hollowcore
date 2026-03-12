using System.Collections.Generic;
using UnityEngine;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Central registry of all quest definitions.
    /// Loaded from Resources/QuestDatabase by QuestRegistryBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Quest/Quest Database")]
    public class QuestDatabaseSO : ScriptableObject
    {
        public List<QuestDefinitionSO> Quests = new();

        [System.NonSerialized]
        private Dictionary<int, QuestDefinitionSO> _lookup;

        public void BuildLookupTable()
        {
            _lookup = new Dictionary<int, QuestDefinitionSO>(Quests.Count);
            foreach (var quest in Quests)
            {
                if (quest == null) continue;
                if (_lookup.ContainsKey(quest.QuestId))
                {
                    Debug.LogWarning($"[QuestDatabase] Duplicate QuestId {quest.QuestId}: '{quest.DisplayName}' conflicts with '{_lookup[quest.QuestId].DisplayName}'");
                    continue;
                }
                _lookup[quest.QuestId] = quest;
            }
        }

        public QuestDefinitionSO GetQuest(int questId)
        {
            if (_lookup == null) BuildLookupTable();
            return _lookup.TryGetValue(questId, out var quest) ? quest : null;
        }

        public bool HasQuest(int questId)
        {
            if (_lookup == null) BuildLookupTable();
            return _lookup.ContainsKey(questId);
        }

        public IReadOnlyDictionary<int, QuestDefinitionSO> GetLookup()
        {
            if (_lookup == null) BuildLookupTable();
            return _lookup;
        }
    }
}
