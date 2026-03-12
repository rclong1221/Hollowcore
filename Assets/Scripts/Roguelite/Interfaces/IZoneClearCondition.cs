using DIG.Roguelite;
using Unity.Entities;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Pluggable: when is a zone "cleared"?
    /// Framework provides built-in implementations. Games can implement custom conditions.
    /// Multiple conditions composed via CompositeClearCondition.
    /// </summary>
    public interface IZoneClearCondition
    {
        /// <summary>Called once when the zone becomes Active. Reset internal state.</summary>
        void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition);

        /// <summary>Polled each frame during Active/BossEncounter phases.</summary>
        bool IsCleared(SystemBase system);
    }

    /// <summary>All enemies spawned and all dead. Classic corridor roguelite.</summary>
    public class AllEnemiesDeadCondition : IZoneClearCondition
    {
        private EntityQuery _zoneQuery;
        private bool _initialized;

        public void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition)
        {
            _zoneQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneState>());
            _initialized = true;
        }

        public bool IsCleared(SystemBase system)
        {
            if (!_initialized || _zoneQuery.IsEmpty) return false;
            var zoneState = _zoneQuery.GetSingleton<ZoneState>();
            return zoneState.EnemiesSpawned > 0 && zoneState.EnemiesAlive <= 0;
        }
    }

    /// <summary>
    /// Player interacts with an exit object (teleporter, portal, door).
    /// Used for open-world zones where the player chooses when to leave.
    /// Reads ZoneExitActivated enableable component on RunState entity.
    /// </summary>
    public class PlayerTriggeredCondition : IZoneClearCondition
    {
        private EntityQuery _runQuery;
        private bool _initialized;

        public void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition)
        {
            _runQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RunState>());
            _initialized = true;
        }

        public bool IsCleared(SystemBase system)
        {
            if (!_initialized || _runQuery.IsEmpty) return false;
            var runEntity = _runQuery.GetSingletonEntity();
            return system.EntityManager.IsComponentEnabled<ZoneExitActivated>(runEntity);
        }
    }

    /// <summary>Survive for N seconds. Timer survival zones.</summary>
    public class TimerExpiredCondition : IZoneClearCondition
    {
        private EntityQuery _zoneQuery;
        private float _duration;
        private bool _initialized;

        public void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition)
        {
            _duration = definition != null ? definition.SurvivalTimer : 0f;
            _zoneQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneState>());
            _initialized = true;
        }

        public bool IsCleared(SystemBase system)
        {
            if (!_initialized || _zoneQuery.IsEmpty) return false;
            var zoneState = _zoneQuery.GetSingleton<ZoneState>();
            return zoneState.TimeInZone >= _duration;
        }
    }

    /// <summary>
    /// Player-triggered boss kill: player activates exit -> boss spawns ->
    /// boss dies -> zone clears. Two-phase (Risk of Rain 2 teleporter pattern).
    /// </summary>
    public class TriggerThenBossCondition : IZoneClearCondition
    {
        private EntityQuery _runQuery;
        private EntityQuery _zoneQuery;
        private bool _initialized;

        public void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition)
        {
            _runQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RunState>());
            _zoneQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneState>());
            _initialized = true;
        }

        public bool IsCleared(SystemBase system)
        {
            if (!_initialized || _runQuery.IsEmpty || _zoneQuery.IsEmpty) return false;

            var runEntity = _runQuery.GetSingletonEntity();
            var zoneState = _zoneQuery.GetSingleton<ZoneState>();

            if (!system.EntityManager.IsComponentEnabled<ZoneExitActivated>(runEntity))
                return false;

            return zoneState.BossSpawned && zoneState.EnemiesAlive <= 0;
        }
    }

    /// <summary>
    /// Combine multiple conditions with AND/OR logic.
    /// Example: (AllEnemiesDead OR TimerExpired) AND PlayerAtExit
    /// </summary>
    public class CompositeClearCondition : IZoneClearCondition
    {
        public enum Logic { And, Or }

        public Logic Mode;
        public IZoneClearCondition[] Conditions;

        public CompositeClearCondition(Logic mode, params IZoneClearCondition[] conditions)
        {
            Mode = mode;
            Conditions = conditions;
        }

        public void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition)
        {
            if (Conditions == null) return;
            for (int i = 0; i < Conditions.Length; i++)
                Conditions[i].OnZoneActivated(system, definition);
        }

        public bool IsCleared(SystemBase system)
        {
            if (Conditions == null || Conditions.Length == 0) return false;

            if (Mode == Logic.And)
            {
                for (int i = 0; i < Conditions.Length; i++)
                    if (!Conditions[i].IsCleared(system)) return false;
                return true;
            }
            else
            {
                for (int i = 0; i < Conditions.Length; i++)
                    if (Conditions[i].IsCleared(system)) return true;
                return false;
            }
        }
    }
}
