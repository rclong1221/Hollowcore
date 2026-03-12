using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Handles shield blocking, parry windows, and damage reduction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UsableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShieldActionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (shield, shieldState, request, entity) in 
                     SystemAPI.Query<RefRO<ShieldAction>, RefRW<ShieldState>, RefRO<UseRequest>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var stateRef = ref shieldState.ValueRW;
                var config = shield.ValueRO;

                // Handle block input
                if (request.ValueRO.StartUse && !stateRef.IsBlocking)
                {
                    // Start blocking
                    stateRef.IsBlocking = true;
                    stateRef.BlockStartTime = currentTime;
                    stateRef.BlocksThisHold = 0;

                    // Activate parry window
                    stateRef.ParryActive = true;
                    stateRef.ParryEndTime = currentTime + config.ParryWindow;
                }
                else if (request.ValueRO.StopUse && stateRef.IsBlocking)
                {
                    // Stop blocking
                    stateRef.IsBlocking = false;
                    stateRef.ParryActive = false;
                }

                // Check parry window expiration
                if (stateRef.ParryActive && currentTime > stateRef.ParryEndTime)
                {
                    stateRef.ParryActive = false;
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for shield damage calculation.
    /// Called by damage systems when applying damage to a blocking entity.
    /// </summary>
    public static class ShieldDamageHelper
    {
        /// <summary>
        /// Calculate modified damage when blocking.
        /// </summary>
        /// <param name="baseDamage">Original damage amount</param>
        /// <param name="shield">Shield configuration</param>
        /// <param name="shieldState">Current shield state</param>
        /// <param name="attackDirection">Direction of incoming attack</param>
        /// <param name="defenderForward">Forward direction of defender</param>
        /// <returns>Modified damage after block reduction</returns>
        public static float CalculateBlockedDamage(float baseDamage, ShieldAction shield, ShieldState shieldState,
                                                   float3 attackDirection, float3 defenderForward)
        {
            if (!shieldState.IsBlocking)
                return baseDamage;

            // Check if attack is within block angle
            float dotProduct = math.dot(math.normalize(-attackDirection), math.normalize(defenderForward));
            float attackAngle = math.degrees(math.acos(dotProduct));
            
            if (attackAngle > shield.BlockAngle * 0.5f)
            {
                // Attack from outside block angle
                return baseDamage;
            }

            // Check for perfect parry
            if (shieldState.ParryActive)
            {
                // Perfect parry - no damage
                return 0f;
            }

            // Normal block - reduce damage
            return baseDamage * (1f - shield.BlockDamageReduction);
        }
    }
}
