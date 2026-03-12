using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;
using Player.Systems;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Applies recoil impulses to the CameraSpringState when a weapon fires.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CameraSpringSolverSystem))] // Apply impulse before solving spring
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponRecoilSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            // Query entities with WeaponRecoil and FireState, AND CameraSpringState (assumed on same entity or fetched?)
            // Usually CameraSpringState is on the Player, and Weapon is on the Player or child.
            // If Weapon is a child entity, we need to find the Player.
            // For now, assuming embedded components (simplest Opsive port) or using specific linkage.
            // In WeaponAuthoring, they are added to 'entity'. Is 'entity' the Player?
            // In PlayerAuthoring, we didn't add weapons.
            // Usually weapons are separate entities attached to holders.
            // If so, we need to find the CameraSpringState owner (the Player).
            // But 'WeaponAuthoring' suggests it's a standalone weapon entity.
            
            // Re-evaluating: 'RecoilState' was likely local to weapon (for visual?). 
            // Camera recoil needs to affect Camera.
            // If Weapon is separate entity, how do we get CameraSpringState?
            // We likely need 'Owner' component on Weapon to find Player.
            // 'GhostOwnerIsLocal' suggests networking.
            // If Weapon is 'held', it should have an 'Owner' reference.
            // 'WeaponActionComponents.cs' doesn't show an 'Owner' component in 'UsableAction'.
            // However, 'Projectile' has 'Owner'.
            
            // Safe bet: Assume Weapon is on the Player Entity for this phase (Composite approach) 
            // OR Weapon has a 'Parent' or 'Owner'.
            // Let's check 'ShootableActionSystem': 
            // `SystemAPI.Query<RefRW<UsableAction>... RefRO<LocalTransform>>`
            // It uses `transform.ValueRO.Position` for ray start.
            // Be careful: if Weapon is a child entity, Transform is local to parent? No, LocalTransform is relative to parent.
            // If Weapon is on Player, Transform is Player transform.
            // The Raycast used `transform.ValueRO.Position + math.up() * 1.5f`.
            // This strongly implies 'transform' is the PLAYER (Root), because +1.5f is Eye Height.
            // A Weapon entity would be at the hand/eye.
            // Conclusion: The Weapon Components are currently on the PLAYER Entity.
            
            foreach (var (recoil, fireState, spring) in 
                     SystemAPI.Query<RefRO<WeaponRecoilComponent>, RefRO<WeaponFireState>, RefRW<CameraSpringState>>()
                     .WithAll<Simulate>())
            {
                var fire = fireState.ValueRO;
                var config = recoil.ValueRO;

                // Check if just fired (TimeSinceLastShot ~ 0)
                // We use a small epsilon because TimeSinceLastShot is reset to 0 in FireSystem
                if (fire.IsFiring && fire.TimeSinceLastShot < deltaTime * 1.5f)
                {
                    // Apply Impulse
                    var random = Unity.Mathematics.Random.CreateFromIndex((uint)(state.GlobalSystemVersion + 1000));
                    
                    float pitchImpulse = config.RecoilAmount; 
                    float yawImpulse = random.NextFloat(-config.Randomness.x, config.Randomness.x) * config.RecoilAmount;
                    
                    // Apply to Rotation Velocity (x = Pitch, y = Yaw)
                    // Note: CameraSpringSolver uses spring.RotationVelocity
                    spring.ValueRW.RotationVelocity.x += pitchImpulse; 
                    spring.ValueRW.RotationVelocity.y += yawImpulse;
                    
                    // Also separate visual recoil for the weapon itself?
                    // Optional: 'WeaponRecoilState' could track weapon model recoil.
                    // For now, focusing on Camera Feel parity.
                }
            }
        }
    }
}
