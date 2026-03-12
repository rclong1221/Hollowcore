#if UNITY_EDITOR
using System;
using UnityEngine;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.7: Serializable run configuration template.
    /// Used by TemplateLibraryModule for one-click creation of complete run configurations.
    /// Custom templates saved as SO assets in Assets/Data/Roguelite/Templates/.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Config Template", order = 100)]
    public class RunConfigTemplate : ScriptableObject
    {
        public string TemplateName;
        [TextArea(2, 4)] public string Description;

        [Header("Zone Structure")]
        public int ZoneCount = 5;
        public ZoneTemplateEntry[] Zones;
        public bool EnableLooping;
        public int LoopStartIndex;
        public float LoopDifficultyMultiplier = 1.5f;

        [Header("Difficulty Curve")]
        public Keyframe[] DifficultyCurve = new[] { new Keyframe(0, 1), new Keyframe(1, 3) };

        [Header("Economy")]
        public int StartingCurrency;
        public int CurrencyPerZoneClear = 10;

        [Header("Director Defaults")]
        public float DefaultInitialBudget = 100f;
        public float DefaultCreditsPerSecond;
        public float DefaultAcceleration;
        public int DefaultMaxAliveEnemies = 40;
        public float DefaultEliteChance = 0.05f;
        public float DefaultEliteMinDifficulty = 2f;
    }

    [Serializable]
    public struct ZoneTemplateEntry
    {
        public ZoneType Type;
        public ZoneClearMode ClearMode;
        public float DifficultyMultiplier;
        public ZoneSelectionMode SelectionMode;
        public int ChoiceCount; // For WeightedRandom/PlayerChoice
    }

    /// <summary>
    /// Built-in template definitions — created in code, not as assets.
    /// </summary>
    public static class BuiltInTemplates
    {
        public static RunConfigTemplate[] GetAll()
        {
            return new[]
            {
                CreateCorridorRun(),
                CreateArenaEscalation(),
                CreateExplorationRun(),
                CreateBranchingPaths(),
                CreateEndlessLoop(),
                CreateBossRush(),
                CreateTutorial()
            };
        }

        private static RunConfigTemplate CreateCorridorRun()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Corridor Run";
            t.Description = "Classic Hades-style: clear room, move on. 5 zones with burst spawning.";
            t.ZoneCount = 5;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Elite, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.5f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 1), new Keyframe(1, 3) };
            t.DefaultInitialBudget = 200f;
            t.DefaultCreditsPerSecond = 0f;
            t.DefaultMaxAliveEnemies = 30;
            t.CurrencyPerZoneClear = 15;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateArenaEscalation()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Arena Escalation";
            t.Description = "Vampire Survivors-style: survive escalating waves in arenas.";
            t.ZoneCount = 3;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Arena, ClearMode = ZoneClearMode.TimerSurvival, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Arena, ClearMode = ZoneClearMode.TimerSurvival, DifficultyMultiplier = 1.5f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2.5f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 1), new Keyframe(1, 5) };
            t.DefaultInitialBudget = 50f;
            t.DefaultCreditsPerSecond = 10f;
            t.DefaultAcceleration = 0.5f;
            t.DefaultMaxAliveEnemies = 60;
            t.CurrencyPerZoneClear = 20;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateExplorationRun()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Exploration Run";
            t.Description = "Risk of Rain-style: explore, shop, events across 7 zones.";
            t.ZoneCount = 7;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Exploration, ClearMode = ZoneClearMode.PlayerTriggered, DifficultyMultiplier = 0.8f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Shop, ClearMode = ZoneClearMode.PlayerTriggered, DifficultyMultiplier = 0.5f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.2f },
                new ZoneTemplateEntry { Type = ZoneType.Event, ClearMode = ZoneClearMode.PlayerTriggered, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Elite, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.8f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 0.8f), new Keyframe(0.5f, 1.5f), new Keyframe(1, 3f) };
            t.DefaultInitialBudget = 80f;
            t.DefaultCreditsPerSecond = 5f;
            t.DefaultMaxAliveEnemies = 25;
            t.CurrencyPerZoneClear = 10;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateBranchingPaths()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Branching Paths";
            t.Description = "Slay the Spire-style: choose your route. WeightedRandom per layer.";
            t.ZoneCount = 5;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f, SelectionMode = ZoneSelectionMode.WeightedRandom, ChoiceCount = 3 },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.2f, SelectionMode = ZoneSelectionMode.WeightedRandom, ChoiceCount = 3 },
                new ZoneTemplateEntry { Type = ZoneType.Elite, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.5f, SelectionMode = ZoneSelectionMode.WeightedRandom, ChoiceCount = 2 },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.5f, SelectionMode = ZoneSelectionMode.WeightedRandom, ChoiceCount = 2 },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2f, SelectionMode = ZoneSelectionMode.Fixed },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 1), new Keyframe(1, 3.5f) };
            t.DefaultInitialBudget = 150f;
            t.DefaultMaxAliveEnemies = 30;
            t.CurrencyPerZoneClear = 12;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateEndlessLoop()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Endless Loop";
            t.Description = "Infinite run with escalating difficulty. Loops from index 1 at 1.5x.";
            t.ZoneCount = 4;
            t.EnableLooping = true;
            t.LoopStartIndex = 1;
            t.LoopDifficultyMultiplier = 1.5f;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 0.8f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Elite, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 1.5f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 1), new Keyframe(1, 2.5f) };
            t.DefaultInitialBudget = 120f;
            t.DefaultCreditsPerSecond = 3f;
            t.DefaultMaxAliveEnemies = 35;
            t.CurrencyPerZoneClear = 10;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateBossRush()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Boss Rush";
            t.Description = "All bosses, no filler. Massive budgets and high elite chance.";
            t.ZoneCount = 5;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 1f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 1.5f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 2.5f },
                new ZoneTemplateEntry { Type = ZoneType.Boss, ClearMode = ZoneClearMode.BossKill, DifficultyMultiplier = 3f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 2), new Keyframe(1, 6) };
            t.DefaultInitialBudget = 500f;
            t.DefaultMaxAliveEnemies = 20;
            t.DefaultEliteChance = 0.25f;
            t.DefaultEliteMinDifficulty = 1f;
            t.CurrencyPerZoneClear = 25;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static RunConfigTemplate CreateTutorial()
        {
            var t = ScriptableObject.CreateInstance<RunConfigTemplate>();
            t.TemplateName = "Tutorial";
            t.Description = "Onboarding run with low stakes. 3 gentle zones.";
            t.ZoneCount = 3;
            t.Zones = new[]
            {
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 0.5f },
                new ZoneTemplateEntry { Type = ZoneType.Shop, ClearMode = ZoneClearMode.PlayerTriggered, DifficultyMultiplier = 0.3f },
                new ZoneTemplateEntry { Type = ZoneType.Combat, ClearMode = ZoneClearMode.AllEnemiesDead, DifficultyMultiplier = 0.7f },
            };
            t.DifficultyCurve = new[] { new Keyframe(0, 0.5f), new Keyframe(1, 1f) };
            t.DefaultInitialBudget = 60f;
            t.DefaultCreditsPerSecond = 0f;
            t.DefaultMaxAliveEnemies = 10;
            t.DefaultEliteChance = 0f;
            t.CurrencyPerZoneClear = 20;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }
    }
}
#endif
