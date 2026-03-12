using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for Item Visual Effects (VFX).
    /// Acts as the "Receiver" in the Driver-Receiver pattern.
    /// 
    /// Usage:
    /// 1. Add to Weapon Prefab.
    /// 2. Define VFX entries (e.g., ID="Fire", AttachedParticle=MuzzleFlash).
    /// 3. Called by WeaponAnimationEventRelay via WeaponEquipVisualBridge.
    /// </summary>
    public class ItemVFXAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct VFXDefinition
        {
            [Tooltip("Event ID sent by Animation (e.g., 'Fire', 'ShellEject').")]
            public string ID;

            [Header("Asset")]
            [Tooltip("Attached Object to Enable/Play (e.g. Muzzle Flash). Can be a GameObject or ParticleSystem.")]
            public GameObject AttachedObject;

            [Tooltip("Prefab to Spawn (e.g. Shell Casing, Smoke).")]
            public GameObject DetachedPrefab;

            [Header("Settings")]
            [Tooltip("If true, DetachedPrefab spawns in World Space (doesn't move with gun). Critical for smoke.")]
            public bool SpawnDetachedInWorld;

            [Tooltip("If true, treats AttachedObject as a GameObject and toggles SetActive(true). If false, looks for ParticleSystem.")]
            public bool UseSetActiveForAttached;

            [Tooltip("Randomize Z Rotation (0-360) to avoid repetition.")]
            public bool RandomizeRotation;

            [Tooltip("Randomize Scale (e.g. 0.8 to 1.2). Set to 0 to disable.")]
            public Vector2 ScaleVariation;

            [Tooltip("Where to spawn. If null, uses this transform.")]
            public Transform SpawnSocket;
            
            [Tooltip("Destroy detached instance after time (0 = never auto destroy)")]
            public float AutoDestroyTime;
            
            [Tooltip("Duration to keep AttachedObject active (if using SetActive). 0 = Manual/OneShot.")]
            public float AttachedDuration;

            [Tooltip("Sample Duration specifically for detached prefabs (override AutoDestroyTime if > 0).")]
            public float DetachedDuration;
            
            [Header("ECS Integration")]
            [Tooltip("If true, spawns shells via ECS Entity system (requires ShellSpawnerAuthoring in SubScene). More performant with physics.")]
            public bool UseECSSpawning;
        }

        [SerializeField]
        [Tooltip("List of VFX Definitions. Runtime looked up by ID.")]
        private List<VFXDefinition> _definitions = new List<VFXDefinition>();

        // Runtime lookup for performance (ID -> Definition)
        private Dictionary<string, VFXDefinition> _vfxCache;
        private bool _initialized = false;
        
        // Coroutine references could be added here for disabling headers, but simple timer in Update is better or coroutine.
        // For ECS purity we avoid Coroutines, but this is Mono Authoring.
        // Let's stick to simple immediate actions for now.

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;

            _vfxCache = new Dictionary<string, VFXDefinition>();
            foreach (var def in _definitions)
            {
                if (!string.IsNullOrEmpty(def.ID) && !_vfxCache.ContainsKey(def.ID))
                {
                    _vfxCache.Add(def.ID, def);
                    
                    // Ensure it starts disabled if we are controlling logic
                    if (def.AttachedObject != null && def.UseSetActiveForAttached)
                    {
                        def.AttachedObject.SetActive(false);
                    }
                }
            }
            _initialized = true;
        }

        /// <summary>
        /// Plays the visual effect matching the given ID.
        /// </summary>
        /// <param name="vfxId">The ID of the effect (e.g., "Fire")</param>
        public void PlayVFX(string vfxId)
        {
            if (!_initialized) Initialize();

            if (_vfxCache.TryGetValue(vfxId, out var def))
            {
                Debug.Log($"[ItemVFX] PlayVFX '{vfxId}' found. Attached={def.AttachedObject}, Detached={def.DetachedPrefab}, UseSetActive={def.UseSetActiveForAttached}");
                
                // 1. Play Attached Object
                if (def.AttachedObject != null)
                {
                    Debug.Log($"[ItemVFX] Playing attached VFX: {def.AttachedObject.name}");
                    // Option A: Generic SetActive Toggle (e.g. Light + Mesh)
                    if (def.UseSetActiveForAttached)
                    {
                        // Must use coroutine - same-frame SetActive(false)→SetActive(true) gets optimized away
                        StartCoroutine(FlashSetActive(def.AttachedObject, def.AttachedDuration));
                    }
                    // Option B: Particle System (Standard)
                    else
                    {
                        // MUST enable the object AND all parents - activeInHierarchy requires full chain
                        Transform current = def.AttachedObject.transform;
                        while (current != null)
                        {
                            if (!current.gameObject.activeSelf)
                            {
                                current.gameObject.SetActive(true);
                                Debug.Log($"[ItemVFX] Enabling parent: {current.name}");
                            }
                            current = current.parent;
                        }
                        
                        // Search in children too, in case user parented a VFX prefab under the socket
                        var ps = def.AttachedObject.GetComponentInChildren<ParticleSystem>(true);
                        Debug.Log($"[ItemVFX] ParticleSystem path. Found PS: {ps != null}, Object Active: {def.AttachedObject.activeInHierarchy}, activeSelf: {def.AttachedObject.activeSelf}");
                        if (ps != null)
                        {
                            // Also enable the PS game object itself
                            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                            
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            
                            if (def.RandomizeRotation)
                            {
                                Vector3 rot = def.AttachedObject.transform.localEulerAngles;
                                rot.z = UnityEngine.Random.Range(0f, 360f);
                                def.AttachedObject.transform.localEulerAngles = rot;
                            }
                            
                            Debug.Log($"[ItemVFX] Playing ParticleSystem: {ps.name}, isPlaying before: {ps.isPlaying}");
                            ps.Play(true);
                            
                            // Fallback: if Play didn't start, force emit
                            if (!ps.isPlaying && !ps.isEmitting)
                            {
                                Debug.LogWarning($"[ItemVFX] Play() failed, forcing Emit(1)");
                                ps.Emit(1);
                            }
                            Debug.Log($"[ItemVFX] After Play(): isPlaying: {ps.isPlaying}, isEmitting: {ps.isEmitting}");
                        }
                        else
                        {
                            Debug.LogWarning($"[ItemVFX] NO ParticleSystem found on '{def.AttachedObject.name}'. Fallback to SetActive toggle.");
                            // FALLBACK: Just toggle it on/off (supports mesh-based flashes without checkbox)
                            def.AttachedObject.SetActive(false);
                            def.AttachedObject.SetActive(true);
                            if (def.AttachedDuration > 0)
                            {
                                StartCoroutine(DisableAfterTime(def.AttachedObject, def.AttachedDuration));
                            }
                        }
                    }
                }

                // 2. Spawn Detached Prefab
                if (def.DetachedPrefab != null)
                {
                    Transform spawnPoint = def.SpawnSocket != null ? def.SpawnSocket : this.transform;
                    
                    // ECS Entity Spawning (recommended for physics shells)
                    if (def.UseECSSpawning)
                    {
                        // Get shell type ID from local WeaponShellAuthoring component
                        var shellAuthoring = GetComponent<WeaponShellAuthoring>();
                        if (shellAuthoring != null && !string.IsNullOrEmpty(shellAuthoring.ShellTypeID))
                        {
                            Vector3 ejectionVelocity = (spawnPoint.right * UnityEngine.Random.Range(3f, 5f)) 
                                                     + (spawnPoint.up * UnityEngine.Random.Range(1f, 2f))
                                                     + (spawnPoint.forward * UnityEngine.Random.Range(-0.5f, 0.5f));
                            
                            // Spawn via ECS using registry lookup
                            DIG.Items.Bridges.ShellSpawnBridge.RequestShellSpawn(
                                shellAuthoring.ShellTypeID,
                                spawnPoint.position,
                                spawnPoint.rotation,
                                ejectionVelocity
                            );
                        }
                        else
                        {
                            Debug.LogWarning($"[ItemVFX] UseECSSpawning enabled but no WeaponShellAuthoring.ShellTypeID on {gameObject.name}");
                        }
                        // Skip classic Instantiate path
                    }
                    else
                    {
                        // Classic GameObject.Instantiate path
                        Transform parent = def.SpawnDetachedInWorld ? null : spawnPoint;
                        var instance = Instantiate(def.DetachedPrefab, spawnPoint.position, spawnPoint.rotation, parent);
                    
                    // Apply Randomization
                    if (def.RandomizeRotation)
                    {
                        instance.transform.Rotate(0, 0, UnityEngine.Random.Range(0f, 360f), Space.Self);
                    }
                    
                    if (def.ScaleVariation != Vector2.zero)
                    {
                        float scale = UnityEngine.Random.Range(def.ScaleVariation.x, def.ScaleVariation.y);
                        instance.transform.localScale *= scale;
                    }
                    
                    if (instance != null)
                    {
                        // FIXED: Apply physics if it's a shell/rigid object
                        // Support Opsive TrajectoryObject (Shells) which controls its own physics
                        var trajectoryObj = instance.GetComponent<Opsive.UltimateCharacterController.Objects.TrajectoryObject>();
                        if (trajectoryObj != null)
                        {
                            // Calculate Ejection Forces
                            Vector3 ejectionForce = (spawnPoint.right * UnityEngine.Random.Range(3f, 5f)) + (spawnPoint.up * UnityEngine.Random.Range(1f, 2f));
                            Vector3 torque = UnityEngine.Random.insideUnitSphere * 10f;
                            
                            // Initialize Opsive Trajectory (Velocity, Torque, Owner)
                            // We pass null for owner as we don't have easy access to the Character GameObject here without passing it down
                            trajectoryObj.Initialize(ejectionForce, torque, (GameObject)null);
                        }
                        else
                        {
                            // Fallback to standard Unity Rigidbody physics
                            var rb = instance.GetComponent<Rigidbody>();
                            Debug.Log($"[ItemVFX] Shell physics check. Has Rigidbody: {rb != null}");
                            if (rb != null)
                            {
                                Debug.Log($"[ItemVFX] Before: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, velocity={rb.linearVelocity}");
                                rb.isKinematic = false; // Override Opsive's default kinematic state
                                rb.useGravity = true;
                                
                                Vector3 ejectionForce = (spawnPoint.right * UnityEngine.Random.Range(3f, 5f)) + (spawnPoint.up * UnityEngine.Random.Range(1f, 2f));
                                rb.AddForce(ejectionForce, ForceMode.Impulse);
                                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
                                Debug.Log($"[ItemVFX] After: velocity={rb.linearVelocity}, Applied force={ejectionForce}");
                            }
                            else
                            {
                                Debug.LogWarning($"[ItemVFX] Shell has NO Rigidbody! Object: {instance.name}");
                            }
                        }

                        // Auto-destroy detached
                        Destroy(instance, def.DetachedDuration > 0 ? def.DetachedDuration : def.AutoDestroyTime > 0 ? def.AutoDestroyTime : 5f);
                    }
                    } // end else (classic Instantiate path)
                }
            }
            else
            {
                Debug.LogWarning($"[ItemVFX] PlayVFX FAILED: ID '{vfxId}' not found in definition list!");
            }
        }
        private System.Collections.IEnumerator FlashSetActive(GameObject obj, float duration)
        {
            if (obj == null) yield break;
            
            // Disable first
            obj.SetActive(false);
            
            // Wait 1 frame so Unity registers the state change
            yield return null;
            
            // Re-enable
            obj.SetActive(true);
            
            // Auto-disable after duration (if set)
            if (duration > 0)
            {
                yield return new WaitForSeconds(duration);
                if (obj != null) obj.SetActive(false);
            }
        }
        
        private System.Collections.IEnumerator DisableAfterTime(GameObject obj, float time)
        {
            yield return new WaitForSeconds(time);
            if (obj != null) obj.SetActive(false);
        }
    }
}
