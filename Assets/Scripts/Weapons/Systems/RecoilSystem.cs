using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Handles camera recoil application and recovery.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ShootableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RecoilSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (recoil, entity) in 
                     SystemAPI.Query<RefRW<RecoilState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var recoilRef = ref recoil.ValueRW;

                // Apply recoil velocity to current recoil
                recoilRef.CurrentRecoil += recoilRef.RecoilVelocity * deltaTime * 10f;

                // Dampen recoil velocity
                recoilRef.RecoilVelocity *= math.max(0, 1f - deltaTime * 15f);

                // Recover recoil toward zero
                float recoveryRate = recoilRef.RecoverySpeed * deltaTime;
                recoilRef.CurrentRecoil.x = math.lerp(recoilRef.CurrentRecoil.x, 0, recoveryRate);
                recoilRef.CurrentRecoil.y = math.lerp(recoilRef.CurrentRecoil.y, 0, recoveryRate);

                // Clamp max recoil
                recoilRef.CurrentRecoil.x = math.clamp(recoilRef.CurrentRecoil.x, -30f, 30f);
                recoilRef.CurrentRecoil.y = math.clamp(recoilRef.CurrentRecoil.y, -15f, 15f);
            }
        }
    }
}
