using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19 + 15.33: Manages AlertState transitions for AI entities.
    ///
    /// 5-Level Alert Model:
    /// - IDLE (0): Normal state, standard detection
    /// - CURIOUS (1): Faint signal (decaying threats, distant hearing)
    /// - SUSPICIOUS (2): Strong signal (recent hearing, social aggro, proximity)
    /// - SEARCHING (3): Aggroed but lost sight of target, investigating last known position
    /// - COMBAT (4): Active engagement, target visible
    ///
    /// Escalation is immediate. De-escalation steps down one level at a time with timers.
    /// Alert state improves detection via AlertStateMultiplier in DetectionSystem.
    ///
    /// Runs as parallel IJobEntity — pure per-entity logic with no cross-entity dependencies.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Vision.Systems.DetectionSystem))]
    [BurstCompile]
    public partial struct AlertStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AlertStateJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct AlertStateJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref AlertState alertState, in AggroState aggroState, DynamicBuffer<ThreatEntry> threatBuffer)
        {
            int currentLevel = alertState.AlertLevel;

            // === Determine target alert level ===
            int targetLevel = AlertState.IDLE;
            Entity alertSource = Entity.Null;
            float3 alertPos = float3.zero;

            if (aggroState.IsAggroed && aggroState.CurrentThreatLeader != Entity.Null)
            {
                // Aggroed — determine if COMBAT or SEARCHING
                bool leaderVisible = false;
                alertSource = aggroState.CurrentThreatLeader;

                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].SourceEntity == alertSource)
                    {
                        alertPos = threatBuffer[t].LastKnownPosition;
                        leaderVisible = threatBuffer[t].IsCurrentlyVisible;
                        break;
                    }
                }

                targetLevel = leaderVisible ? AlertState.COMBAT : AlertState.SEARCHING;
            }
            else if (threatBuffer.Length > 0)
            {
                // Not aggroed but has threats — SUSPICIOUS or CURIOUS
                float highestThreat = 0f;
                bool hasRecentSignal = false;

                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].ThreatValue > highestThreat)
                    {
                        highestThreat = threatBuffer[t].ThreatValue;
                        alertSource = threatBuffer[t].SourceEntity;
                        alertPos = threatBuffer[t].LastKnownPosition;
                    }

                    // Recent hearing, social, or proximity = strong signal → SUSPICIOUS
                    var strongFlags = ThreatSourceFlags.Hearing | ThreatSourceFlags.Social | ThreatSourceFlags.Proximity;
                    if ((threatBuffer[t].SourceFlags & strongFlags) != 0 && threatBuffer[t].TimeSinceVisible < 5f)
                    {
                        hasRecentSignal = true;
                    }
                }

                targetLevel = hasRecentSignal ? AlertState.SUSPICIOUS : AlertState.CURIOUS;
            }

            // === Update alert level ===
            if (targetLevel > currentLevel)
            {
                // Escalate immediately
                alertState.AlertLevel = targetLevel;
                alertState.AlertTimer = GetAlertDuration(targetLevel);
                alertState.AlertSource = alertSource;
                alertState.AlertPosition = alertPos;

                // On entering SEARCHING, set investigate position
                if (targetLevel == AlertState.SEARCHING)
                {
                    alertState.InvestigatePosition = alertPos;
                    alertState.SearchTimer = 0f;
                    alertState.HasInvestigated = false;
                }
            }
            else if (targetLevel < currentLevel)
            {
                // Decay with timer — step down one level at a time
                alertState.AlertTimer -= DeltaTime;

                // Track search time in SEARCHING state
                if (currentLevel == AlertState.SEARCHING)
                {
                    alertState.SearchTimer += DeltaTime;
                }

                if (alertState.AlertTimer <= 0f)
                {
                    int newLevel = currentLevel - 1;
                    alertState.AlertLevel = newLevel;
                    alertState.AlertTimer = GetAlertDuration(newLevel);

                    if (newLevel == AlertState.IDLE)
                    {
                        alertState.AlertSource = Entity.Null;
                        alertState.AlertPosition = float3.zero;
                        alertState.InvestigatePosition = float3.zero;
                        alertState.SearchTimer = 0f;
                        alertState.HasInvestigated = false;
                    }

                    // On entering SEARCHING from COMBAT decay
                    if (newLevel == AlertState.SEARCHING)
                    {
                        alertState.InvestigatePosition = alertState.AlertPosition;
                        alertState.SearchTimer = 0f;
                        alertState.HasInvestigated = false;
                    }
                }
            }
            else
            {
                // Maintain current level — refresh timer if still valid
                if (targetLevel > AlertState.IDLE)
                {
                    alertState.AlertTimer = GetAlertDuration(targetLevel);
                    alertState.AlertSource = alertSource;
                    alertState.AlertPosition = alertPos;
                }

                // Track search time in SEARCHING state
                if (currentLevel == AlertState.SEARCHING)
                {
                    alertState.SearchTimer += DeltaTime;
                }
            }
        }

        /// <summary>Gets the duration before alert level decays one step down.</summary>
        private static float GetAlertDuration(int alertLevel)
        {
            return alertLevel switch
            {
                AlertState.COMBAT => 3.0f,       // 3s after losing sight before entering SEARCHING
                AlertState.SEARCHING => 10.0f,    // 10s searching at last known position
                AlertState.SUSPICIOUS => 8.0f,    // 8s of heightened awareness
                AlertState.CURIOUS => 5.0f,       // 5s of mild curiosity before returning to IDLE
                _ => 0f
            };
        }
    }
}
