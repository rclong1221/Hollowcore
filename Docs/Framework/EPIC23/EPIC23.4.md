# EPIC 23.4: Run Modifiers & Difficulty Scaling

**Status:** PLANNED
**Priority:** High (replayability driver — makes each run feel different)
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase)
- `DifficultyDefinitionSO` (existing — `Assets/Scripts/Lobby/Config/DifficultyDefinitionSO.cs`, EPIC 17.4)
- `EquippedStatsSystem` (existing — `Assets/Scripts/Progression/Systems/`, EPIC 16.14)

**Feature:** Stackable modifiers that alter run parameters — positive (blessings), negative (curses), and neutral (trade-offs). Includes an ascension/heat system for escalating difficulty with increased rewards. All scaling flows through a `RuntimeDifficultyScale` singleton that existing systems can read.

---

## Problem

Without modifiers, every run at the same difficulty feels the same. Rogue-lites need per-run variation: "this run I have double damage but enemies explode on death." The ascension system provides long-term challenge scaling beyond the base difficulty levels.

---

## Core Types

```csharp
// File: Assets/Scripts/Roguelite/Definitions/RunModifierDefinitionSO.cs
namespace DIG.Roguelite.Modifiers
{
    public enum ModifierPolarity : byte
    {
        Positive = 0,     // Blessings, boons
        Negative = 1,     // Curses, malfunctions
        Neutral = 2       // Trade-offs
    }

    public enum ModifierTarget : byte
    {
        PlayerStat = 0,   // Modify CharacterAttributes
        EnemyStat = 1,    // Modify difficulty scales
        RunMechanic = 2,  // Toggle behaviors (no healing, exploding enemies)
        Economy = 3,      // Modify currency/loot rates
        Encounter = 4     // More elites, extra phases
    }

    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Modifier")]
    public class RunModifierDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ModifierId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public ModifierPolarity Polarity;
        public ModifierTarget Target;

        [Header("Effect")]
        public int StatId;
        public float FloatValue;
        public bool IsMultiplicative;
        public int IntValue;

        [Header("Stacking")]
        public bool Stackable;
        public int MaxStacks;                // 0 = unlimited

        [Header("Ascension")]
        public int RequiredAscensionLevel;   // 0 = always available
        public float HeatCost;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/RunModifierStack.cs
namespace DIG.Roguelite.Modifiers
{
    [InternalBufferCapacity(16)]
    public struct RunModifierStack : IBufferElementData
    {
        public int ModifierId;
        public byte StackCount;
        public float EffectiveValue;       // Pre-computed
        public ModifierTarget Target;
        public int StatId;
        public bool IsMultiplicative;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/RuntimeDifficultyScale.cs
namespace DIG.Roguelite.Modifiers
{
    /// <summary>
    /// Effective difficulty after zone curve + all modifiers.
    /// Systems read this instead of raw DifficultyDefinitionSO.
    /// Backward compatible: when absent, systems fall back to raw values.
    /// </summary>
    public struct RuntimeDifficultyScale : IComponentData
    {
        public float EnemyHealthScale;
        public float EnemyDamageScale;
        public float EnemySpawnRateScale;
        public float LootQuantityScale;
        public float LootQualityBonus;
        public float XPMultiplier;
        public float CurrencyMultiplier;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/AscensionDefinitionSO.cs
namespace DIG.Roguelite.Modifiers
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Ascension Definition")]
    public class AscensionDefinitionSO : ScriptableObject
    {
        public List<AscensionTier> Tiers;
    }

    [Serializable]
    public class AscensionTier
    {
        public int Level;
        public string DisplayName;
        public string Description;
        public List<RunModifierDefinitionSO> ForcedModifiers;
        public float ScoreMultiplier;
        public float MetaCurrencyMultiplier;
        public Sprite Icon;
    }
}
```

---

## Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `ModifierBootstrapSystem` | InitializationSystemGroup | All | No | Loads pools and ascension definitions from Resources/. Managed registry |
| `AscensionSetupSystem` | SimulationSystemGroup | Server\|Local | No | On `Preparation`: adds forced modifiers from matching ascension tier |
| `DifficultyScalingSystem` | SimulationSystemGroup, UpdateBefore(ModifierApplicationSystem) | Server\|Local | Yes | Reads `RunConfigBlob` curve for current zone × base difficulty. Writes `RuntimeDifficultyScale` |
| `ModifierApplicationSystem` | SimulationSystemGroup, UpdateAfter(DifficultyScalingSystem) | Server\|Local | Yes | Iterates `RunModifierStack`. EnemyStat → adjust RuntimeDifficultyScale. PlayerStat → stat modifier entries. RunMechanic → enable/disable flag components |
| `ModifierAcquisitionSystem` | SimulationSystemGroup | Server\|Local | No | Processes `AddModifierRequest` (transient). Handles stacking, pre-computes effective value |

---

## Integration

- **DifficultyDefinitionSO**: `RuntimeDifficultyScale` wraps same field names. Existing systems check for singleton first, fall back to raw values. Zero breaking change
- **CharacterAttributes**: PlayerStat modifiers write through `EquippedStatsSystem` pipeline
- **EncounterState**: Encounter modifiers readable as flag components or RuntimeDifficultyScale fields

---

## Performance

- `RunModifierStack`: 16 inline entries = 320 bytes. Typical: 5-15 modifiers
- `ModifierApplicationSystem`: Burst, O(modifier_count), runs once per zone
- `RuntimeDifficultyScale`: 1 entity, ~32 bytes, `GetSingleton` cached

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/Definitions/RunModifierDefinitionSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/RunModifierPoolSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/AscensionDefinitionSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Components/RunModifierStack.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Components/RuntimeDifficultyScale.cs` | IComponentData | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ModifierBootstrapSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/AscensionSetupSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/DifficultyScalingSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ModifierApplicationSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ModifierAcquisitionSystem.cs` | SystemBase | **NEW** |

---

## Verification

1. **Stacking**: Adding same modifier increments StackCount, EffectiveValue recalculated
2. **Ascension**: Level 3 adds all forced modifiers from tiers 1-3
3. **Difficulty curve**: Zone 0 = base × curve[0]. Zone N = base × curve[N/max] × modifiers
4. **RuntimeDifficultyScale**: Reflects combined curve + all active modifiers
5. **Backward compat**: Without RuntimeDifficultyScale singleton, systems read raw difficulty values
