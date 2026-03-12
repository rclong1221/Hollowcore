#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.7: Tracks which roguelite SOs reference which other SOs.
    /// Built by walking serialized fields of all indexed SOs.
    /// Used by: Content Coverage Analyzer (orphan detection),
    ///          Data Dependency Graph (visualization),
    ///          Delete/rename safety warnings.
    /// </summary>
    public class SODependencyGraph
    {
        /// <summary>Forward edges: source → set of targets it references.</summary>
        public Dictionary<ScriptableObject, HashSet<ScriptableObject>> References = new();

        /// <summary>Reverse edges: target → set of sources that reference it.</summary>
        public Dictionary<ScriptableObject, HashSet<ScriptableObject>> ReferencedBy = new();

        /// <summary>All tracked SOs.</summary>
        public HashSet<ScriptableObject> AllNodes = new();

        /// <summary>Walk all serialized fields of indexed SOs and build edges.</summary>
        public void Build(RogueliteDataContext context)
        {
            References.Clear();
            ReferencedBy.Clear();
            AllNodes.Clear();

            // Register all known SOs
            RegisterAll(context.RunConfigs);
            RegisterAll(context.ZoneDefinitions);
            RegisterAll(context.ZoneSequences);
            RegisterAll(context.EncounterPools);
            RegisterAll(context.SpawnDirectorConfigs);
            RegisterAll(context.InteractablePools);
            RegisterAll(context.RewardDefinitions);
            RegisterAll(context.RewardPools);
            RegisterAll(context.RunEvents);
            if (context.ModifierPool != null) AllNodes.Add(context.ModifierPool);
            if (context.AscensionDefinition != null) AllNodes.Add(context.AscensionDefinition);
            if (context.MetaUnlockTree != null) AllNodes.Add(context.MetaUnlockTree);

            // Walk serialized fields and build edges
            foreach (var node in AllNodes)
                WalkReferences(node);
        }

        /// <summary>All SOs that directly or transitively depend on the given SO.</summary>
        public List<ScriptableObject> GetDependents(ScriptableObject so)
        {
            var result = new List<ScriptableObject>();
            var visited = new HashSet<ScriptableObject>();
            CollectTransitive(so, ReferencedBy, visited, result);
            return result;
        }

        /// <summary>All SOs that the given SO directly or transitively references.</summary>
        public List<ScriptableObject> GetDependencies(ScriptableObject so)
        {
            var result = new List<ScriptableObject>();
            var visited = new HashSet<ScriptableObject>();
            CollectTransitive(so, References, visited, result);
            return result;
        }

        /// <summary>SOs that no other SO references (potential orphans).</summary>
        public List<ScriptableObject> GetOrphans()
        {
            var orphans = new List<ScriptableObject>();
            foreach (var node in AllNodes)
            {
                if (!ReferencedBy.TryGetValue(node, out var refs) || refs.Count == 0)
                    orphans.Add(node);
            }
            return orphans;
        }

        /// <summary>Check if source directly references target. O(1) with HashSet.</summary>
        public bool HasDirectReference(ScriptableObject source, ScriptableObject target)
        {
            return References.TryGetValue(source, out var refs) && refs.Contains(target);
        }

        /// <summary>Check if target is directly referenced by source. O(1) with HashSet.</summary>
        public bool IsDirectlyReferencedBy(ScriptableObject target, ScriptableObject source)
        {
            return ReferencedBy.TryGetValue(target, out var refs) && refs.Contains(source);
        }

        /// <summary>Checks what would break if 'so' were deleted.</summary>
        public List<ScriptableObject> GetImpactedByDeletion(ScriptableObject so)
        {
            return GetDependents(so);
        }

        private void RegisterAll<T>(T[] assets) where T : ScriptableObject
        {
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] != null) AllNodes.Add(assets[i]);
        }

        private void WalkReferences(ScriptableObject source)
        {
            var so = new SerializedObject(source);
            var iterator = so.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var target = iterator.objectReferenceValue as ScriptableObject;
                    if (target != null && target != source && AllNodes.Contains(target))
                        AddEdge(source, target);
                }
                else if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                {
                    // Walk array elements
                    for (int i = 0; i < iterator.arraySize; i++)
                    {
                        var element = iterator.GetArrayElementAtIndex(i);
                        ExtractSOReferences(element, source);
                    }
                    enterChildren = false; // Already walked children
                }
            }
        }

        private void ExtractSOReferences(SerializedProperty prop, ScriptableObject source)
        {
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                var target = prop.objectReferenceValue as ScriptableObject;
                if (target != null && target != source && AllNodes.Contains(target))
                    AddEdge(source, target);
                return;
            }

            // Walk children of struct/class elements
            var end = prop.GetEndProperty();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren) && !SerializedProperty.EqualContents(prop, end))
            {
                enterChildren = true;
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var target = prop.objectReferenceValue as ScriptableObject;
                    if (target != null && target != source && AllNodes.Contains(target))
                        AddEdge(source, target);
                }
            }
        }

        private void AddEdge(ScriptableObject source, ScriptableObject target)
        {
            if (!References.TryGetValue(source, out var fwd))
            {
                fwd = new HashSet<ScriptableObject>();
                References[source] = fwd;
            }
            fwd.Add(target);

            if (!ReferencedBy.TryGetValue(target, out var rev))
            {
                rev = new HashSet<ScriptableObject>();
                ReferencedBy[target] = rev;
            }
            rev.Add(source);
        }

        private static void CollectTransitive(
            ScriptableObject root,
            Dictionary<ScriptableObject, HashSet<ScriptableObject>> graph,
            HashSet<ScriptableObject> visited,
            List<ScriptableObject> result)
        {
            if (!graph.TryGetValue(root, out var neighbors)) return;
            foreach (var neighbor in neighbors)
            {
                if (visited.Add(neighbor))
                {
                    result.Add(neighbor);
                    CollectTransitive(neighbor, graph, visited, result);
                }
            }
        }
    }
}
#endif
