using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// Debug system to log animation state values for all players.
/// Remove this after debugging is complete.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GhostSimulationSystemGroup))]
public partial class DebugAnimationStateSystem : SystemBase
{
    private float _lastLogTime;
    private const float LogInterval = 1.0f; // Log every second

    protected override void OnCreate()
    {
        RequireForUpdate<NetworkStreamInGame>();
    }

    protected override void OnUpdate()
    {
        float time = (float)SystemAPI.Time.ElapsedTime;
        if (time - _lastLogTime < LogInterval)
            return;
        _lastLogTime = time;

        int playerIndex = 0;
        foreach (var (animState, playerState, entity) in 
                 SystemAPI.Query<RefRO<PlayerAnimationState>, RefRO<PlayerState>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
        {
            var anim = animState.ValueRO;
            var ps = playerState.ValueRO;
            bool hasSimulate = EntityManager.HasComponent<Simulate>(entity);
            
            // Debug.Log($"[DebugAnim] Player {playerIndex} (Entity {entity.Index}): " +
            //           $"Simulate={hasSimulate}, " +
            //           $"MovementState={ps.MovementState}, " +
            //           $"AnimIsSprinting={anim.IsSprinting}, " +
            //           $"MoveSpeed={anim.MoveSpeed:F2}");
            playerIndex++;
        }
    }
}
