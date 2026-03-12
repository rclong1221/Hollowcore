using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// Debug system to log player input for testing
/// Remove or disable this in production builds
/// </summary>
using Player.Systems;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
[UpdateAfter(typeof(PlayerInputSystem))]
public partial struct PlayerInputDebugSystem : ISystem
{
    private float _lastLogTime;
    private const float LOG_INTERVAL = 2.0f; // Log every 2 seconds to avoid spam
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _lastLogTime = 0;
    }

    public void OnUpdate(ref SystemState state)
    {
        // Only log periodically to avoid console spam
        if (SystemAPI.Time.ElapsedTime - _lastLogTime < LOG_INTERVAL)
            return;
        
        _lastLogTime = (float)SystemAPI.Time.ElapsedTime;
        
        // Log input for local player only
        foreach (var input in SystemAPI.Query<RefRO<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
        {
            var inp = input.ValueRO;
            
            // Only log if there's actual input
            bool hasInput = inp.Horizontal != 0 || inp.Vertical != 0 ||
                           inp.Jump.IsSet || inp.Crouch.IsSet || inp.Sprint.IsSet ||
                           inp.LookDelta.x != 0 || inp.LookDelta.y != 0;
            
            if (hasInput)
            {
                DebugLog.LogInput($"Move: ({inp.Horizontal}, {inp.Vertical}) | " +
                                 $"Look: ({inp.LookDelta.x:F2}, {inp.LookDelta.y:F2}) | " +
                                 $"Jump: {inp.Jump.IsSet} | Sprint: {inp.Sprint.IsSet} | " +
                                 $"Crouch: {inp.Crouch.IsSet} | Interact: {inp.Interact.IsSet}");
            }
        }
    }
}

