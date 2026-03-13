# EPIC 8.1 Setup Guide: Trace State & Threshold Configuration

**Status:** Planned
**Requires:** Framework Roguelite/ (RunModifierStack), Run Bootstrap Subscene

---

## Overview

Trace is the expedition-level pressure meter — a single integer that climbs as players attract attention (killing enemies, looting, using abilities). At defined thresholds, escalating consequences trigger: hunter spawns, reduced gate options, boss upgrades, price inflation. Designers configure thresholds and their effects via a singleton config asset.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Run Bootstrap Subscene | `RunLifecycleAuthoring` | Framework roguelite lifecycle |
| RunModifierStack | (Framework) | Threshold effects compose as run modifiers |
| Gate System | EPIC 6 | Trace threshold reduces gate count |
| Hunter System | EPIC 8.4 | Hunters spawn at Trace thresholds |

### New Setup Required

1. **Create a `TraceConfigSO`** asset with threshold definitions
2. **Add `TraceAuthoring`** to the run bootstrap subscene
3. **Configure threshold effects** (what happens at each level)
4. **(Optional)** Set up `TraceMeterUI` in your HUD (EPIC 8.5)

---

## 1. Creating the Trace Config

**Create:** `Assets > Create > Hollowcore/Trace/Trace Config`

**Recommended location:** `Assets/Data/Trace/TraceConfig.asset`

### 1.1 Base Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **MaxTrace** | Soft cap for UI display scaling (does not clamp actual Trace) | 100 | 50–500 |
| **TimeBasedIncrementInterval** | Seconds between passive +1 Trace ticks | 60 | 15–300 |
| **TimeBasedIncrementEnabled** | Whether Trace grows passively over time | true | |
| **TraceDecayRate** | Trace lost per second when in a "safe zone" (0 = no decay) | 0 | 0–5 |

**Tuning tip:** TimeBasedIncrementInterval is your primary pacing knob. 60s means a 30-minute run accumulates ~30 Trace passively. Set to 120s for a more relaxed pace, 30s for intense pressure. Combine with source events (EPIC 8.2) for the full picture.

### 1.2 Thresholds

Configure up to 8 thresholds as an ordered list. Each threshold has:

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **TraceLevel** | Trace value at which this threshold activates | (varies) | 1–MaxTrace |
| **EffectType** | What happens | (varies) | Enum: HunterSpawn, GateReduction, BossUpgrade, PriceInflation, EnemyBuff, Custom |
| **EffectMagnitude** | Strength of the effect (type-dependent) | 1.0 | 0.1–10.0 |
| **Label** | Designer-facing label for UI/debug | "Threshold 1" | |
| **OneShot** | If true, triggers once then never again | true | |

**Example threshold table (recommended defaults):**

| Level | Label | EffectType | Magnitude | Notes |
|-------|-------|------------|-----------|-------|
| 15 | Noticed | EnemyBuff | 1.1 | +10% enemy HP |
| 30 | Tracked | HunterSpawn | 1 | First hunter variant spawns |
| 45 | Hunted | GateReduction | 1 | One fewer gate option |
| 60 | Marked | HunterSpawn | 2 | Second hunter variant |
| 75 | Exposed | BossUpgrade | 1 | Boss gains 1 extra clause |
| 90 | Condemned | PriceInflation | 1.5 | Shop prices ×1.5 |

**Tuning tip:** Keep thresholds monotonically increasing. Space them 15-20 apart for a standard run. The first threshold should hit around minute 10-15 of a run. Players who play aggressively should hit 3-4 thresholds; careful players might hit 1-2.

### 1.3 UI Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ShowMeterInHUD** | Always show Trace meter | true | |
| **PulseOnThreshold** | Flash the meter when a threshold activates | true | |
| **ThresholdWarningDistance** | Trace points before threshold to start warning pulse | 5 | 1–20 |
| **MeterColor_Low** | Color below 33% | Green | |
| **MeterColor_Mid** | Color at 33-66% | Yellow | |
| **MeterColor_High** | Color above 66% | Red | |

---

## 2. Trace Authoring Setup

### 2.1 Bootstrap Subscene

1. Open your **run bootstrap subscene**
2. Create a new GameObject: `TraceConfig`
3. **Add Component** → `TraceAuthoring` (from `Hollowcore.Trace`)
4. Assign your `TraceConfigSO` to the **Config** field

| Field | Description |
|-------|-------------|
| **Config** | Reference to your TraceConfigSO asset |
| **DebugStartTrace** | Debug: start with this Trace value (0 = normal) |

### 2.2 How It Works at Runtime

1. `TraceBootstrapSystem` creates the `TraceState` singleton from baked config
2. `TraceAccumulationSystem` processes `TraceSourceEvent` entities (from EPIC 8.2)
3. `TraceThresholdSystem` checks thresholds, applies effects via `RunModifierStack`
4. `TraceSinkSystem` processes decay from `TraceSinkAPI` calls (EPIC 8.3)

---

## 3. Configuring Trace Sources (Preview)

Trace sources (EPIC 8.2) define what actions generate Trace. Quick reference:

| Source | Typical Trace | Notes |
|--------|--------------|-------|
| Enemy Kill | +1 | Per enemy |
| Elite Kill | +3 | Named/elite enemies |
| Loot Chest | +2 | Per chest opened |
| Loud Ability | +1 | Explosions, AoE |
| Side Goal Skip | +5 | Skipping insurance |
| Time Tick | +1 | Per TimeBasedIncrementInterval |

Full source configuration is in SETUP_GUIDE_8.2.

---

## 4. Asset Checklist

| Asset Type | Minimum Count | Location |
|------------|--------------|----------|
| TraceConfigSO | 1 | `Assets/Data/Trace/` |
| TraceAuthoring GO | 1 (in bootstrap subscene) | Bootstrap Subscene |

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Run Bootstrap Subscene | `TraceAuthoring` on config GO | Creates singleton at run start |
| HUD Scene | `TraceMeterUI` (EPIC 8.5) | Optional but recommended |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Thresholds not in ascending order | "Trace thresholds must be monotonically increasing" | Sort by TraceLevel ascending |
| Duplicate threshold levels | Second threshold at same level never fires | Each level must be unique |
| MaxTrace too low | Meter looks full early, thresholds bunch up | Set MaxTrace ≥ highest threshold + 10 |
| TimeBasedIncrementInterval too low (e.g., 5s) | Trace skyrockets, every threshold hit in 2 minutes | 30s minimum for intense; 60s default |
| Missing TraceAuthoring in bootstrap | "TraceState singleton not found" errors | Add to bootstrap subscene |
| EffectMagnitude = 0 on a threshold | Threshold fires but nothing happens | Set > 0 for all non-Custom effects |

---

## Verification

1. **Enter Play Mode** — Console should show:
   ```
   [TraceBootstrapSystem] Initialized TraceState: MaxTrace=100, Thresholds=6
   ```
2. **HUD** → Trace meter should appear at 0/100
3. **Kill enemies** → Trace should increment (check Entity Debugger → `TraceState.CurrentTrace`)
4. **Wait for threshold** → Console: `[TraceThresholdSystem] Threshold 'Noticed' activated at Trace=15`
5. **Debug shortcut** → Set `DebugStartTrace=89` on TraceAuthoring, enter play — should trigger first 5 thresholds immediately
6. **Run validator:** `Hollowcore > Validation > Trace` — should report 0 errors
