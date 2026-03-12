using UnityEngine;
using System.Collections.Generic;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Generic visual action controller for ANY weapon type.
    /// Handles show/hide parts, spawn objects, reparent transforms.
    /// Animation events send commands like "ShowPart:Magazine" or "Spawn:DropMag".
    /// </summary>
    public class WeaponVisualActionController : MonoBehaviour
    {
        [System.Serializable]
        public class VisualPart
        {
            [Tooltip("ID used in animation events (e.g., 'Magazine', 'Rocket', 'Bolt')")]
            public string PartID;
            
            [Tooltip("The GameObject to show/hide (drag any child here)")]
            public GameObject Part;
        }
        
        [System.Serializable]
        public class SpawnableObject
        {
            [Tooltip("ID used in animation events (e.g., 'DropMag', 'EmptyCasing')")]
            public string ObjectID;
            
            [Tooltip("Prefab to spawn (with Rigidbody for physics)")]
            public GameObject Prefab;
            
            [Tooltip("Where to spawn the object")]
            public Transform SpawnPoint;
            
            [Tooltip("Auto-destroy after this many seconds (0 = never)")]
            public float DestroyAfterSeconds = 5f;
        }
        
        [System.Serializable]
        public class ReparentTarget
        {
            [Tooltip("ID used in animation events (e.g., 'LeftHand', 'RightHand')")]
            public string TargetID;
            
            [Tooltip("The bone/transform to parent to")]
            public Transform Target;
        }
        
        [Header("Toggleable Parts")]
        [Tooltip("Parts that can be shown/hidden (magazines, projectiles, etc.)")]
        public List<VisualPart> Parts = new List<VisualPart>();
        
        [Header("Spawnable Objects")]
        [Tooltip("Objects that can be spawned (dropped mags, casings, etc.)")]
        public List<SpawnableObject> Spawnables = new List<SpawnableObject>();
        
        [Header("Reparent Targets")]
        [Tooltip("Bones that parts can be reparented to")]
        public List<ReparentTarget> ReparentTargets = new List<ReparentTarget>();
        
        [Header("Debug")]
        public bool DebugLogging = false;
        
        // Cached original transforms
        private Dictionary<string, Transform> _originalParents = new Dictionary<string, Transform>();
        private Dictionary<string, Vector3> _originalLocalPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, Quaternion> _originalLocalRotations = new Dictionary<string, Quaternion>();
        
        private void Awake()
        {
            // Cache original transform data for all parts
            foreach (var part in Parts)
            {
                if (part.Part != null)
                {
                    _originalParents[part.PartID] = part.Part.transform.parent;
                    _originalLocalPositions[part.PartID] = part.Part.transform.localPosition;
                    _originalLocalRotations[part.PartID] = part.Part.transform.localRotation;
                }
            }
        }
        
        /// <summary>
        /// Execute an action from an animation event.
        /// Supported formats:
        ///   ShowPart:PartID
        ///   HidePart:PartID
        ///   Spawn:ObjectID
        ///   Reparent:PartID:TargetID
        ///   Restore:PartID
        /// </summary>
        public bool ExecuteAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            
            var parts = action.Split(':');
            if (parts.Length < 2) return false;
            
            string command = parts[0];
            string arg1 = parts[1];
            string arg2 = parts.Length > 2 ? parts[2] : null;
            
            if (DebugLogging)
                Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] ExecuteAction: {action} (Parts Count: {Parts.Count}, Targets: {ReparentTargets.Count})");
            
            switch (command)
            {
                case "ShowPart": return ShowPart(arg1);
                case "HidePart": return HidePart(arg1);
                case "Spawn": return SpawnObject(arg1);
                case "Reparent": return arg2 != null && ReparentPart(arg1, arg2);
                case "Restore": return RestorePart(arg1);
                default:
                    if (DebugLogging) Debug.Log($"[VisualAction] Unknown command: {command}");
                    return false;
            }
        }
        
        public bool ShowPart(string partID)
        {
            var part = FindPart(partID);
            if (part?.Part == null) return false;
            part.Part.SetActive(true);
            if (DebugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] ShowPart: {partID} - Success");
            return true;
        }
        
        public bool HidePart(string partID)
        {
            var part = FindPart(partID);
            if (part?.Part == null) return false;
            part.Part.SetActive(false);
            if (DebugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] HidePart: {partID} - Success");
            return true;
        }
        
        public bool SpawnObject(string objectID)
        {
            var spawnable = FindSpawnable(objectID);
            if (spawnable?.Prefab == null) return false;
            
            var spawnPoint = spawnable.SpawnPoint != null ? spawnable.SpawnPoint : transform;
            var spawned = Instantiate(spawnable.Prefab, spawnPoint.position, spawnPoint.rotation);
            
            var rb = spawned.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = transform.forward * -0.5f + Vector3.down * 0.2f;
                rb.angularVelocity = Random.insideUnitSphere * 2f;
            }
            
            if (spawnable.DestroyAfterSeconds > 0)
                Destroy(spawned, spawnable.DestroyAfterSeconds);
            
            if (DebugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] Spawn: {objectID} at {spawnPoint.name}");
            return true;
        }
        
        public bool ReparentPart(string partID, string targetID)
        {
            var part = FindPart(partID);
            var target = FindTarget(targetID);
            if (part?.Part == null || target?.Target == null) return false;
            
            part.Part.transform.SetParent(target.Target);
            if (DebugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] Reparent: {partID} → {targetID} ({target.Target.name})");
            return true;
        }
        
        public bool RestorePart(string partID)
        {
            var part = FindPart(partID);
            if (part?.Part == null || !_originalParents.ContainsKey(partID)) return false;
            
            part.Part.transform.SetParent(_originalParents[partID]);
            part.Part.transform.localPosition = _originalLocalPositions[partID];
            part.Part.transform.localRotation = _originalLocalRotations[partID];
            part.Part.SetActive(true);
            
            if (DebugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Controller] Restore: {partID}");
            return true;
        }
        
        private VisualPart FindPart(string partID)
        {
            foreach (var p in Parts)
                if (p.PartID == partID) return p;
            if (DebugLogging) Debug.LogWarning($"[VisualAction] Part not found: {partID}");
            return null;
        }
        
        private SpawnableObject FindSpawnable(string objectID)
        {
            foreach (var s in Spawnables)
                if (s.ObjectID == objectID) return s;
            if (DebugLogging) Debug.LogWarning($"[VisualAction] Spawnable not found: {objectID}");
            return null;
        }
        
        private ReparentTarget FindTarget(string targetID)
        {
            foreach (var t in ReparentTargets)
                if (t.TargetID == targetID) return t;
            if (DebugLogging) Debug.LogWarning($"[VisualAction] Target not found: {targetID}");
            return null;
        }
        
        /// <summary>
        /// Runtime helper to set a reparent target (for bones that vary per character).
        /// </summary>
        public void SetReparentTarget(string targetID, Transform target)
        {
            var existing = FindTarget(targetID);
            if (existing != null)
            {
                existing.Target = target;
            }
            else
            {
                ReparentTargets.Add(new ReparentTarget { TargetID = targetID, Target = target });
            }
        }
    }
}
