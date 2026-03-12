using Unity.Entities;
using Unity.NetCode;
using DIG.Ship.LocalSpace;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Ship.Power
{
    /// <summary>
    /// Debug system that logs power and life support state when P key is pressed.
    /// Also allows toggling power producer on/off with O key.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PowerDebugSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // P key: Log power status
            if (keyboard.pKey.wasPressedThisFrame)
            {
                LogPowerStatus();
            }

            // O key: Toggle first power producer (for testing brownout)
            if (keyboard.oKey.wasPressedThisFrame)
            {
                ToggleFirstProducer();
            }
#endif
        }

        private void LogPowerStatus()
        {
            UnityEngine.Debug.Log("========== SHIP POWER STATUS ==========");

            int shipCount = 0;
            foreach (var (powerState, shipRoot, entity) in
                     SystemAPI.Query<RefRO<ShipPowerState>, RefRO<ShipRoot>>()
                     .WithEntityAccess())
            {
                shipCount++;
                var state = powerState.ValueRO;
                
                UnityEngine.Debug.Log($"Ship {entity.Index}:");
                UnityEngine.Debug.Log($"  Production: {state.TotalProduced:F1}W");
                UnityEngine.Debug.Log($"  Demand:     {state.TotalDemand:F1}W");
                UnityEngine.Debug.Log($"  Consumed:   {state.TotalConsumed:F1}W");
                UnityEngine.Debug.Log($"  Balance:    {state.PowerBalance:F1}W");
                UnityEngine.Debug.Log($"  Efficiency: {state.PowerEfficiency * 100:F0}%");
                UnityEngine.Debug.Log($"  Brownout:   {(state.IsBrownout ? "YES!" : "No")}");
            }

            if (shipCount == 0)
            {
                UnityEngine.Debug.Log("  [No ships with power state found]");
            }

            // Log life support status
            UnityEngine.Debug.Log("---------- LIFE SUPPORT ----------");
            int lsCount = 0;
            foreach (var (lifeSupport, consumer, entity) in
                     SystemAPI.Query<RefRO<LifeSupport>, RefRO<ShipPowerConsumer>>()
                     .WithEntityAccess())
            {
                lsCount++;
                var ls = lifeSupport.ValueRO;
                var pwr = consumer.ValueRO;

                UnityEngine.Debug.Log($"LifeSupport {entity.Index}:");
                UnityEngine.Debug.Log($"  Status:     {(ls.IsOnline ? "ONLINE" : "OFFLINE")}");
                UnityEngine.Debug.Log($"  Power:      {pwr.CurrentPower:F1} / {pwr.RequiredPower:F1}W");
                UnityEngine.Debug.Log($"  Damaged:    {ls.IsDamaged}");
                UnityEngine.Debug.Log($"  Zone:       {(ls.InteriorZoneEntity != Entity.Null ? ls.InteriorZoneEntity.Index.ToString() : "None")}");
            }

            if (lsCount == 0)
            {
                UnityEngine.Debug.Log("  [No life support systems found]");
            }

            // Log all power consumers
            UnityEngine.Debug.Log("---------- POWER CONSUMERS ----------");
            foreach (var (consumer, entity) in
                     SystemAPI.Query<RefRO<ShipPowerConsumer>>()
                     .WithEntityAccess())
            {
                var c = consumer.ValueRO;
                string status = c.IsFullyPowered ? "[OK]" : (c.IsStarved ? "[STARVED]" : "[LOW]");
                UnityEngine.Debug.Log($"  Consumer {entity.Index}: {c.CurrentPower:F1}/{c.RequiredPower:F1}W (Pri:{c.Priority}) {status}");
            }

            UnityEngine.Debug.Log("====================================");
        }

        private void ToggleFirstProducer()
        {
            foreach (var producer in SystemAPI.Query<RefRW<ShipPowerProducer>>())
            {
                producer.ValueRW.IsOnline = !producer.ValueRO.IsOnline;
                producer.ValueRW.CurrentOutput = producer.ValueRO.IsOnline ? producer.ValueRO.MaxOutput : 0f;
                
                UnityEngine.Debug.Log($"[Power Debug] Toggled producer: {(producer.ValueRO.IsOnline ? "ON" : "OFF")}");
                break; // Only toggle first one
            }
        }
    }
}
