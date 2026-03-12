#if DIG_DEV_CONSOLE
using Unity.Entities;
using UnityEngine;
using DIG.Weather;

namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: World/environment console commands.
    /// </summary>
    public static class WorldCommands
    {
        [ConCommand("time", "Set or query time of day", "time [hours (0-24)]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdTime(ConCommandArgs args)
        {
            var world = DevConsoleService.FindAuthoritativeWorld();
            if (world == null || !world.IsCreated) { DevConsoleService.Instance.LogWarning("No authoritative world."); return; }

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WorldTimeState>());
            if (query.IsEmpty) { DevConsoleService.Instance.LogWarning("WorldTimeState singleton not found."); return; }

            var state = query.GetSingleton<WorldTimeState>();

            if (args.Count == 0)
            {
                int hours = (int)state.TimeOfDay;
                int minutes = (int)((state.TimeOfDay - hours) * 60);
                DevConsoleService.Instance.Log($"Time: {hours:D2}:{minutes:D2} (Day {state.DayCount}, {state.Season})");
                return;
            }

            float newTime = Mathf.Clamp(args.GetFloat(0, state.TimeOfDay), 0f, 23.99f);
            state.TimeOfDay = newTime;
            query.SetSingleton(state);

            int h = (int)newTime;
            int m = (int)((newTime - h) * 60);
            DevConsoleService.Instance.Log($"Time set to {h:D2}:{m:D2}");
        }

        [ConCommand("weather", "Set weather type", "weather <Clear|PartlyCloudy|Cloudy|LightRain|HeavyRain|Thunderstorm|LightSnow|HeavySnow|Fog|Sandstorm>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdWeather(ConCommandArgs args)
        {
            if (args.Count == 0)
            {
                // Query current weather
                var world = DevConsoleService.FindAuthoritativeWorld();
                if (world == null) { DevConsoleService.Instance.LogWarning("No authoritative world."); return; }

                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherState>());
                if (query.IsEmpty) { DevConsoleService.Instance.LogWarning("WeatherState singleton not found."); return; }

                var ws = query.GetSingleton<WeatherState>();
                DevConsoleService.Instance.Log($"Weather: {ws.CurrentWeather} (Wind: {ws.WindSpeed:F1}, Temp: {ws.Temperature:F1}C)");
                return;
            }

            var weatherType = args.GetEnum<WeatherType>(0, WeatherType.Clear);

            var w = DevConsoleService.FindAuthoritativeWorld();
            if (w == null) { DevConsoleService.Instance.LogWarning("No authoritative world."); return; }

            using var q = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<WeatherState>());
            if (q.IsEmpty) { DevConsoleService.Instance.LogWarning("WeatherState singleton not found."); return; }

            var state = q.GetSingleton<WeatherState>();
            state.CurrentWeather = weatherType;
            state.TransitionProgress = 1f; // Instant transition
            q.SetSingleton(state);

            DevConsoleService.Instance.Log($"Weather set to: {weatherType}");
        }

        [ConCommand("fog", "Set fog density (0-1)", "fog <density>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdFog(ConCommandArgs args)
        {
            if (args.Count == 0) { DevConsoleService.Instance.LogWarning("Usage: fog <density 0-1>"); return; }

            var world = DevConsoleService.FindAuthoritativeWorld();
            if (world == null) { DevConsoleService.Instance.LogWarning("No authoritative world."); return; }

            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<WeatherState>());
            if (q.IsEmpty) { DevConsoleService.Instance.LogWarning("WeatherState singleton not found."); return; }

            var state = q.GetSingleton<WeatherState>();
            state.FogDensity = Mathf.Clamp01(args.GetFloat(0));
            q.SetSingleton(state);

            DevConsoleService.Instance.Log($"Fog density set to: {state.FogDensity:F2}");
        }
    }
}
#endif
