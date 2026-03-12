# EPIC 23.1 Setup Guide — Run Lifecycle & State Machine

This guide walks through everything a developer or designer needs to do in the Unity Editor to get the rogue-lite run lifecycle working in a game built on DIG.

---

## Quick Start (Setup Wizard)

The fastest way to set up the module:

1. Open **DIG > Roguelite Setup** from the Unity menu bar
2. In the **Setup Validation** section, click any **Create** buttons next to missing items
3. Configure the Run Configuration in the inline inspector
4. Enter Play Mode and use the **Runtime State** test buttons to verify

The wizard handles asset creation, template generation, and validation automatically. The sections below explain each step in detail for manual setup or customization.

---

## Prerequisites

- Unity 6000.3.x with Entities, Burst, Collections, Mathematics, and NetCode packages
- DIG framework with `DIG.Shared` assembly present

---

## 1. Create the Run Configuration Asset

The run configuration defines the structure, difficulty curve, time limits, and economy of a run. Designers author this as a ScriptableObject.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In the **Setup Validation** section, click **Create RunConfig Asset**
3. The asset is created at `Assets/Resources/RunConfig.asset` and auto-selected
4. Configure it in the **Run Configuration** section of the wizard, which includes a visual difficulty curve preview

### Via Project Window (Manual)

1. In the Project window, navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Run Configuration**
3. Name the asset **RunConfig** (this exact name is required — the bootstrap system loads `Resources.Load<RunConfigSO>("RunConfig")`)
4. Select the asset and configure it in the Inspector:

### Inspector Fields

| Section | Field | Type | Default | Description |
|---------|-------|------|---------|-------------|
| **Identity** | Config Name | string | — | Human-readable label (e.g., "Standard Run", "Quick Mode") |
| | Config Id | int | 0 | Unique integer ID. Must match `LobbyState.RunConfigId` if using lobby selection |
| **Structure** | Zone Count | int | 5 | Total number of zones in the run. Boss zone is typically the last one |
| **Difficulty** | Difficulty Per Zone | AnimationCurve | Linear 1→3 | X-axis: normalized zone position (0 = first zone, 1 = last zone). Y-axis: difficulty multiplier. Curve is sampled per-zone and baked into a BlobArray at runtime |
| **Time** | Base Zone Time Limit | float | 0 | Per-zone time limit in seconds. Set to 0 for no limit |
| | Run Time Limit | float | 0 | Total run time limit in seconds. When elapsed time exceeds this, the run ends with `TimedOut`. Set to 0 for no limit |
| **Economy** | Starting Run Currency | int | 0 | Currency the player begins with at run start |
| | Run Currency Per Zone Clear | int | 10 | Currency awarded each time a zone is cleared |
| | Meta Currency Conversion Rate | float (0–2) | 0.5 | Multiplier when converting run currency to permanent meta-currency at run end. 0.5 = half of run currency becomes meta-currency |

### Difficulty Curve Tips

- Click the curve field to open the AnimationCurve editor
- The curve is evaluated at evenly-spaced points (one per zone)
- A **linear 1→3** curve means zone 0 has 1x difficulty, the final zone has 3x
- Use an **exponential** curve for a gentle start with a steep late-game spike
- Use a **stepped** curve if you want discrete difficulty plateaus

### Multiple Run Configurations

To support multiple run types (e.g., "Standard", "Quick", "Endless"):

1. Create additional RunConfigSO assets in `Assets/Resources/RunConfigs/`
2. Give each a unique `ConfigId`
3. Modify `RunConfigBootstrapSystem` to load by `LobbyState.RunConfigId` instead of the default path (or create a custom loader system in your game assembly)

---

## 2. Lobby Integration

The lobby now supports selecting a run configuration before starting a game.

### LobbyState.RunConfigId

A `RunConfigId` field has been added to `LobbyState`. When the lobby UI selects a rogue-lite run:

- Set `RunConfigId` to the matching `RunConfigSO.ConfigId`
- Set `RunConfigId = -1` for non-rogue-lite game modes (the roguelite systems will create a default config but remain in `RunPhase.None`)

This field is automatically included in lobby serialization (WriteTo/ReadFrom).

---

## 3. Connecting Player Death (PermadeathSignal Bridge)

The rogue-lite module does not directly access `Health` or `DeathState` (they live in the main assembly). Instead, your game must provide a **bridge system** that tells the framework when a player has died.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In the **Setup Validation** section, click **Create PermadeathBridge Template**
3. A ready-to-use system is created at `Assets/Scripts/Game/Roguelite/PermadeathBridgeSystem.cs`
4. Customize the death condition for your game (single-player vs co-op, downed state, etc.)

### Manual Setup

Create a system in your game's Assembly-CSharp code that:

1. Queries players who are dead (`DeathState.Phase == Dead`)
2. Enables `PermadeathSignal` on the RunState entity

Place this file anywhere in your game code (not inside `Assets/Scripts/Roguelite/`):

```csharp
using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Roguelite;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PermadeathSystem))]
public partial class PermadeathBridgeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<RunState>();
    }

    protected override void OnUpdate()
    {
        var run = SystemAPI.GetSingleton<RunState>();
        if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter)
            return;

        foreach (var (death, _) in
            SystemAPI.Query<RefRO<DeathState>, RefRO<PlayerTag>>())
        {
            if (death.ValueRO.Phase == DeathPhase.Dead)
            {
                var runEntity = SystemAPI.GetSingletonEntity<RunState>();
                EntityManager.SetComponentEnabled<PermadeathSignal>(runEntity, true);
                return;
            }
        }
    }
}
```

### Co-op Considerations

- The example above triggers permadeath on **any** player death. For co-op, you may want to check if **all** players are dead before enabling the signal
- The bridge is entirely in your control — add whatever logic fits your game

---

## 4. Starting a Run (Game-Side Code)

The framework creates the `RunState` entity at startup with `Phase = None`. Your game code is responsible for transitioning through the phases. Here is the expected flow:

### Phase Transition Sequence

```
None → Lobby → Preparation → ZoneLoading → Active → ... → RunEnd → MetaScreen → None
```

To start a run, your game's lobby/menu system sets the RunState fields:

```csharp
using Unity.Entities;
using DIG.Roguelite;

// When the player presses "Start Run":
var runEntity = SystemAPI.GetSingletonEntity<RunState>();
var run = SystemAPI.GetSingleton<RunState>();

run.RunId = nextRunId++;           // Increment per run
run.Seed = GenerateSeed();         // Your seed source (System.Random, user input, etc.)
run.Phase = RunPhase.Preparation;  // Skip Lobby if going straight in
run.EndReason = RunEndReason.None;
run.CurrentZoneIndex = 0;
run.ElapsedTime = 0f;
run.Score = 0;
run.RunCurrency = 0;               // RunInitSystem will apply StartingRunCurrency
run.AscensionLevel = 0;            // Or selected ascension level

SystemAPI.SetSingleton(run);
```

### Advancing Zones

After a zone is cleared (detected by your game or by 23.3 ZoneClearDetectionSystem):

```csharp
var run = SystemAPI.GetSingleton<RunState>();
run.Phase = RunPhase.ZoneTransition;  // Triggers reward screen
SystemAPI.SetSingleton(run);

// Later, when the player is ready for the next zone:
run.CurrentZoneIndex++;
run.Phase = RunPhase.ZoneLoading;     // IZoneProvider begins loading
SystemAPI.SetSingleton(run);

// When the zone is ready:
run.Phase = RunPhase.Active;
SystemAPI.SetSingleton(run);
```

### Ending a Run (Victory)

```csharp
var run = SystemAPI.GetSingleton<RunState>();
run.Phase = RunPhase.RunEnd;
run.EndReason = RunEndReason.BossDefeated;
SystemAPI.SetSingleton(run);
```

`RunEndSystem` will calculate the score and transition to `MetaScreen` automatically.

---

## 5. Connecting the HUD (IRunUIProvider)

To display run information in your game's UI, implement `IRunUIProvider` and register it.

### What You Need To Build

1. Create a MonoBehaviour on your HUD canvas that implements `IRunUIProvider`
2. Register it on `OnEnable`, unregister on `OnDisable`

```csharp
using DIG.Roguelite;
using UnityEngine;
using TMPro;

public class RunHUDAdapter : MonoBehaviour, IRunUIProvider
{
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _zoneText;
    [SerializeField] private TMP_Text _currencyText;
    [SerializeField] private GameObject _runEndPanel;

    private void OnEnable()  => RunUIRegistry.Register(this);
    private void OnDisable() => RunUIRegistry.Unregister(this);

    public void OnPhaseChanged(RunPhase newPhase, RunPhase previousPhase)
    {
        // Show/hide UI panels based on phase
    }

    public void OnRunStart(uint runId, uint seed, byte maxZones)
    {
        // Reset HUD for new run
    }

    public void OnZoneChanged(byte zoneIndex, uint zoneSeed)
    {
        _zoneText.text = $"Zone {zoneIndex + 1}";
    }

    public void OnRunEnd(RunEndReason reason, int finalScore, int runCurrency, int zonesCleared)
    {
        _runEndPanel.SetActive(true);
        // Populate end-of-run stats
    }

    public void UpdateHUD(float elapsedTime, int score, int runCurrency, byte currentZone, byte maxZones)
    {
        int minutes = (int)(elapsedTime / 60f);
        int seconds = (int)(elapsedTime % 60f);
        _timerText.text = $"{minutes:00}:{seconds:00}";
        _scoreText.text = score.ToString();
        _currencyText.text = runCurrency.ToString();
    }
}
```

Place this on a GameObject in your HUD scene/canvas. `RunUIBridgeSystem` calls these methods every frame from PresentationSystemGroup.

---

## 6. Verifying the Setup

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. Check the **Setup Validation** section — all items should show green checkmarks
3. Enter Play Mode
4. The **Runtime State** section appears automatically, showing live RunState values
5. Use the test buttons (**Start Run**, **Activate Zone**, **Clear Zone**, **Kill Player**, **Boss Victory**, **Reset Run**) to walk through the full lifecycle without writing any game code

### In the Entity Debugger (Window > Entities > Systems)

After entering Play Mode, confirm:

| Entity | Components | Expected |
|--------|-----------|----------|
| **RunConfig** | `RunConfigSingleton` | BlobAssetReference is valid (not null) |
| **RunState** | `RunState`, `RunPhaseChangedTag`, `PermadeathSignal` | Phase = None. RunPhaseChangedTag disabled. PermadeathSignal disabled |

### In the Console

On startup you should see:

```
[RunConfigBootstrap] Loaded run config: 'YourConfigName' (Id=0, Zones=5)
```

If the asset is missing:

```
[RunConfigBootstrap] No RunConfigSO found at Resources/RunConfig. Using defaults.
```

### System Execution Order

Open **Window > Entities > Systems** and verify the following order inside SimulationSystemGroup:

```
RunLifecycleSystem
  └─ RunInitSystem
      └─ PermadeathSystem
          └─ RunEndSystem
```

And in PresentationSystemGroup:

```
RunUIBridgeSystem
```

---

## 7. Excluding the Roguelite Module

If you are building a non-rogue-lite game (e.g., a shooter, survival game) and want to remove this module entirely:

**Option A — Delete the folder:**
Delete `Assets/Scripts/Roguelite/`. Since no other assembly references `DIG.Roguelite`, there will be zero compile errors.

**Option B — Define constraint:**
Open `Assets/Scripts/Roguelite/DIG.Roguelite.asmdef` in a text editor and add:

```json
"defineConstraints": ["DIG_ROGUELITE"]
```

The assembly will only compile when `DIG_ROGUELITE` is defined in **Project Settings > Player > Scripting Define Symbols**. Without the define, the module is invisible to the compiler.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No RunConfigSO found at Resources/RunConfig" warning | Asset not created or not in `Assets/Resources/` | Create the asset via **Create > DIG > Roguelite > Run Configuration** and place it in `Assets/Resources/` with the name **RunConfig** |
| RunState entity exists but Phase never changes from None | No game code is advancing the phase | You must write game-side code to set `RunState.Phase` (see Section 4) |
| Player dies but run doesn't end | PermadeathBridgeSystem not created | Create the bridge system in your game assembly (see Section 3) |
| HUD not updating | IRunUIProvider not registered | Ensure your MonoBehaviour calls `RunUIRegistry.Register(this)` in `OnEnable` |
| LobbyState serialization errors after update | RunConfigId field added to binary format | Both host and client must be on the same build. Old save data from before this change is incompatible |
| "The BlobAssetReference is null" in RunLifecycleSystem | RunConfigBootstrapSystem failed or hasn't run yet | Check console for bootstrap errors. Ensure InitializationSystemGroup runs before SimulationSystemGroup (default Unity ordering) |

---

## File Reference

```
Assets/
├── Resources/
│   └── RunConfig.asset                              ← Created by wizard or manually
│
├── Editor/
│   └── RogueliteWorkstation/
│       └── RogueliteSetupWizard.cs  (DIG > Roguelite Setup menu)
│
├── Scripts/
│   └── Roguelite/
│       ├── DIG.Roguelite.asmdef
│       ├── Components/
│       │   ├── RunPhase.cs          (RunPhase, RunEndReason enums)
│       │   └── RunState.cs          (RunState, RunPhaseChangedTag, PermadeathSignal)
│       ├── Definitions/
│       │   └── RunConfigSO.cs       (ScriptableObject — designer-authored)
│       ├── Utility/
│       │   ├── RunSeedUtility.cs    (Deterministic seed derivation)
│       │   └── RunConfigBlobBuilder.cs (BlobAsset types + builder)
│       ├── Systems/
│       │   ├── RunConfigBootstrapSystem.cs  (Loads config, creates entities)
│       │   ├── RunLifecycleSystem.cs        (State machine, time, seeds — Burst)
│       │   ├── RunInitSystem.cs             (Phase change reactions)
│       │   ├── PermadeathSystem.cs          (Signal → RunEnd — Burst)
│       │   ├── RunEndSystem.cs              (Score, meta-currency, → MetaScreen)
│       │   └── RunUIBridgeSystem.cs         (ECS → managed UI)
│       └── Bridges/
│           └── RunUIRegistry.cs     (IRunUIProvider + static registry)
│
└── Scripts/Lobby/
    └── LobbyState.cs               (Modified — added RunConfigId)
```

### What the Framework Provides vs. What You Build

| Framework (DIG.Roguelite) | Your Game (Assembly-CSharp) |
|---------------------------|---------------------------|
| RunState entity + lifecycle systems | Phase transitions (start run, advance zones, end run) |
| PermadeathSignal component | PermadeathBridgeSystem (detects player death, enables signal) |
| RunUIRegistry + IRunUIProvider interface | HUD MonoBehaviour implementing IRunUIProvider |
| RunConfigSO (ScriptableObject definition) | RunConfig.asset (configured in Inspector) |
| Score calculation + meta-currency queuing | Meta-currency spending UI (23.2) |
