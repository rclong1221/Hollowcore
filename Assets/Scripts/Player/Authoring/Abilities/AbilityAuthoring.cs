using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player.Abilities;

namespace DIG.Player.Authoring.Abilities
{
    [DisallowMultipleComponent]
    public class AbilityAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct AbilityConfig
        {
            public string Name;
            public int AbilityTypeId;
            [Tooltip("Higher number = higher priority")]
            public int Priority;
            public bool StartActive;
            
            public AbilityStartType StartType;
            public AbilityStopType StopType;
            
            [Header("Conflict Masks")]
            [Tooltip("Bitmask of ability priorities that block this ability")]
            public int BlockedByMask;
            [Tooltip("Bitmask of ability priorities this ability blocks")]
            public int BlocksMask;
            
            [Header("Input")]
            public int InputActionId; // Placeholder for input system mapping
        }

        public List<AbilityConfig> Abilities = new List<AbilityConfig>();

        class Baker : Baker<AbilityAuthoring>
        {
            public override void Bake(AbilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Add AbilityState
                AddComponent(entity, new AbilityState
                {
                    ActiveAbilityIndex = -1,
                    PendingAbilityIndex = -1,
                    AbilityStartTime = 0,
                    AbilityElapsedTime = 0
                });
                
                // Add AbilitySystemTag
                AddComponent<AbilitySystemTag>(entity);
                
                // Add AbilityDefinition buffer
                var buffer = AddBuffer<AbilityDefinition>(entity);
                
                foreach (var config in authoring.Abilities)
                {
                    buffer.Add(new AbilityDefinition
                    {
                        AbilityTypeId = config.AbilityTypeId,
                        Priority = config.Priority,
                        IsActive = config.StartActive,
                        CanStart = false,
                        CanStop = false,
                        HasStarted = false,
                        StartType = config.StartType,
                        StopType = config.StopType,
                        BlockedByMask = config.BlockedByMask,
                        BlocksMask = config.BlocksMask,
                        InputActionId = config.InputActionId
                    });
                }
            }
        }
    }
}
