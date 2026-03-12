using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Player.Components;
using DIG.Player.View;
using DIG.Player.IK;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Player.Systems.IK
{
    /// <summary>
    /// Links PlayerIKBridge MonoBehaviour on the presentation GameObject to its ECS entity.
    /// This allows PlayerIKBridge.OnAnimatorIK to read LookAtIKState and FootIKSettings from ECS components.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LookAtIKSystem))]
    public partial class PlayerIKBridgeLinkSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;
        private HashSet<int> _linkedInstances = new HashSet<int>();
        
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }
        
        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null) return;
            }
            
            foreach (var (playerIKLink, entity) in 
                SystemAPI.Query<RefRO<PlayerIKBridgeLink>>()
                    .WithAll<PlayerTag, LookAtIKSettings>()
                    .WithEntityAccess())
            {
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null) continue;
                
                int instanceId = presentation.GetInstanceID();
                
                // Only link once per instance
                if (_linkedInstances.Contains(instanceId)) continue;
                
                // Find PlayerIKBridge on the presentation GameObject
                var playerIKBridge = presentation.GetComponentInChildren<PlayerIKBridge>(true);
                if (playerIKBridge != null)
                {
                    playerIKBridge.LinkState(entity, EntityManager);
                    _linkedInstances.Add(instanceId);
                    Debug.Log($"[PlayerIK] Linked bridge on {presentation.name} to entity {entity.Index}");
                }
                else
                {
                    // Auto-add PlayerIKBridge if missing
                    var animator = presentation.GetComponentInChildren<Animator>(true);
                    if (animator != null)
                    {
                        playerIKBridge = animator.gameObject.AddComponent<PlayerIKBridge>();
                        playerIKBridge.LinkState(entity, EntityManager);
                        _linkedInstances.Add(instanceId);
                        Debug.LogWarning($"[PlayerIK] Auto-added bridge to {animator.gameObject.name}, linked to entity {entity.Index}");
                    }
                    else
                    {
                        Debug.LogError($"[PlayerIK] No Animator found on {presentation.name} - cannot add bridge!");
                    }
                }
            }
        }
        
        protected override void OnDestroy()
        {
            _linkedInstances.Clear();
        }
    }
}
