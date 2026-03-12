using UnityEngine;
using System.Collections.Generic;

namespace Player.PhysicsSimulation
{
    /// <summary>
    /// Singleton coordinator for client-side ragdoll physics simulation.
    /// Addresses Bug 2.8.2: Prevents multiple ragdolls from calling Physics.Simulate() 
    /// multiple times per frame, which causes global physics issues.
    /// </summary>
    public class RagdollPhysicsSimulator : MonoBehaviour
    {
        private static RagdollPhysicsSimulator _instance;
        private static bool _shuttingDown = false;
        
        public static RagdollPhysicsSimulator Instance
        {
            get
            {
                // Don't create during shutdown
                if (_shuttingDown)
                {
                    return null;
                }
                
                if (_instance == null)
                {
                    var go = new GameObject("RagdollPhysicsSimulator");
                    _instance = go.AddComponent<RagdollPhysicsSimulator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        // Track active ragdolls that request simulation
        private HashSet<int> _activeRagdolls = new HashSet<int>();
        
        private void FixedUpdate()
        {
            // Only simulate if using Script mode (NetCode default) AND we have active ragdolls
            if (UnityEngine.Physics.simulationMode == SimulationMode.Script && _activeRagdolls.Count > 0)
            {
                UnityEngine.Physics.Simulate(Time.fixedDeltaTime);
            }
        }
        
        /// <summary>
        /// Register a ragdoll as active. Should be called when ragdoll enters state.
        /// </summary>
        public void RegisterRagdoll(int instanceId)
        {
            _activeRagdolls.Add(instanceId);
        }
        
        /// <summary>
        /// Unregister a ragdoll. Should be called when ragdoll exits state or is destroyed.
        /// </summary>
        public void UnregisterRagdoll(int instanceId)
        {
            _activeRagdolls.Remove(instanceId);
        }
        
        private void OnDestroy()
        {
            // Clear static reference when destroyed
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        private void OnApplicationQuit()
        {
            // Set flag FIRST to prevent recreation
            _shuttingDown = true;
            
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }
    }
}
