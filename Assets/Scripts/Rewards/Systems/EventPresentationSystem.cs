using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: On Event zone ZoneTransition, selects event from pool using event seed.
    /// Stores on managed registry for UI to display. Runs once per zone transition.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EventPresentationSystem : SystemBase
    {
        private bool _eventPresented;
        private RunPhase _lastPhase;
        private readonly System.Collections.Generic.List<RunEventDefinitionSO> _validEvents = new();

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<RewardRegistryManaged>())
                return;

            var run = SystemAPI.GetSingleton<RunState>();

            // Reset guard when leaving ZoneTransition
            if (run.Phase != _lastPhase)
            {
                if (_lastPhase == RunPhase.ZoneTransition)
                    _eventPresented = false;
                _lastPhase = run.Phase;
            }

            if (run.Phase != RunPhase.ZoneTransition || _eventPresented)
                return;

            // Check if this is an event zone
            bool isEventZone;
            if (SystemAPI.HasSingleton<ZoneContextSingleton>())
            {
                isEventZone = SystemAPI.GetSingleton<ZoneContextSingleton>().CurrentType == ZoneTransitionType.Event;
            }
            else
            {
                isEventZone = (run.CurrentZoneIndex % 3) == 2;
            }

            if (!isEventZone)
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<RewardRegistryManaged>();
            var eventPool = registry.DefaultEventPool;
            if (eventPool == null || eventPool.Events == null || eventPool.Events.Count == 0)
                return;

            // Build valid events for current zone
            _validEvents.Clear();
            foreach (var evt in eventPool.Events)
            {
                if (evt == null) continue;
                if (evt.MinZoneIndex > 0 && run.CurrentZoneIndex < evt.MinZoneIndex) continue;
                if (evt.MaxZoneIndex > 0 && run.CurrentZoneIndex > evt.MaxZoneIndex) continue;
                _validEvents.Add(evt);
            }

            if (_validEvents.Count == 0)
                return;

            // Weighted selection using event seed
            float totalWeight = 0f;
            foreach (var e in _validEvents)
                totalWeight += e.Weight;

            var rng = new Unity.Mathematics.Random(RunSeedUtility.DeriveEventSeed(run.ZoneSeed));
            float roll = rng.NextFloat() * totalWeight;

            RunEventDefinitionSO selected = null;
            foreach (var e in _validEvents)
            {
                roll -= e.Weight;
                if (roll <= 0f) { selected = e; break; }
            }
            if (selected == null) selected = _validEvents[0];

            RewardUIRegistry.SetActiveEvent(selected);
            _eventPresented = true;
        }
    }
}
