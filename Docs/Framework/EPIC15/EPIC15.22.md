# EPIC 15.22: Floating Damage Text System

**Status:** Phase 1-3 Implemented (Core Architecture + Visual Configuration + AAA Culling)
**Priority:** Medium (Visual Feedback)
**Dependencies:**
- ✅ `Assets/DamageNumbersPro`
- ✅ `DamageNumbersProAdapter.cs`
- ✅ ECS Damage Pipeline (`DamageApplySystem`)

**Feature:** A robust, modular, and performant floating damage text system bridging ECS data with the `DamageNumbersPro` asset.

---

## Overview

Visual feedback for combat is critical for player agency. Players need to know:
1.  **How much damage** they dealt.
2.  **What kind of damage** (Physical, Fire, Critical).
3.  **If an attack failed** (Miss, Block, Immune).
4.  **Who they hit** (tracking targets in a crowd).

This epic defines the architecture for bridging the high-performance ECS damage pipeline with the rich visual capabilities of the `DamageNumbersPro` Unity asset.

---

## Combat Feedback Intricacy

To match the standards of high-end action games (like *Elden Ring*, *Borderlands*, *WoW*), the system must provide nuanced feedback beyond simple numbers.

### 1. Hit Severity Tiers
We will differentiate hits based on their impact-to-health ratio and mechanics.

| Severity | Trigger Condition | Visual Style | Motion | Priority |
|----------|-------------------|--------------|--------|----------|
| **Graze** | Damage < 5% or Partial Dodge | Small, Grey, 50% Opacity | Slide Down | Low |
| **Normal** | Standard Hit | White/Color, Medium Size | Float Up | Medium |
| **Critical** | Headshot or Crit Roll | Yellow/Red, Large, "!" | Pop In + Shake | High |
| **Crushing** | Damage > 50% Max HP | Huge, Bold, Screen Shake | Slow Float | Critical |
| **Execute** | Killing Blow | Gold, "FATALITY" style | Stationery + Fade | Critical |

### 2. Defensive Feedback
When damage is negated, players must know *why*.

| Type | Condition | Text Display | Color |
|------|-----------|--------------|-------|
| **Block** | Shield/Parry blocks damage | "BLOCKED" (or shield icon) | Blue |
| **Parry** | Perfect Parry timing | "PARRY" | Gold |
| **Dodge** | Evasion success | "MISS" | Grey |
| **Immune** | Indestructible state | "IMMUNE" | White |
| **Resist** | High elemental resistance | "RESIST" | Grey |

### 3. Contextual Combat Events
Special text events for specific gameplay actions.

- **"HEADSHOT"**: When hitting a Weakspot collider.
- **"BACKSTAB"**: When hitting from behind (+30% damage).
- **"BROKEN"**: When breaking an enemy's poise/shield.
- **"COMBO x5"**: (Optional) Multi-hit counter for rapid attackers.

### 4. Elemental Efficacy
Feedback on damage type effectiveness.

- **Super Effective**: (e.g. Fire vs Ice) -> Text pulses or has a "▲" arrow.
- **Not Effective**: (e.g. Fire vs Fire) -> Text is small, greyed out, or has "▼" arrow.

---

## Architecture: The ECS-to-Managed Bridge

### Data Flow

```
[Attacker] -> [DamageEvent Buffer] -> [DamageApplySystem (Burst)]
                                              ↓
                                      [DamageResult Buffer] (New)
                                              ↓
                                   [DamageNumberBridgeSystem (Managed)]
                                              ↓
                                   [DamageNumbersProAdapter (MB)] (Existing)
                                              ↓
                                     [DamageNumber Mesh (World)]
```

### 1. The Source: `DamageApplySystem` Update
Modified to calculate and write results *before* clearing events.

### 2. The Data: `DamageResult` Component
Updated to support the expanded feature set.

```csharp
public struct DamageResult : IBufferElementData
{
    public float Amount;            // Final damage applied
    public float3 Position;         // World position
    public DamageType Type;         // Physical, Fire, etc.
    public HitType HitType;         // Crit, Block, etc.
    public ResultFlags Flags;       // Context flags
}

// Extended Enum in DIG.Targeting.Theming
public enum HitType : byte
{
    None = 0,
    Miss = 1,
    Graze = 2,
    Normal = 3,
    Critical = 4,
    Blocked = 5,
    Parried = 6,
    Immune = 7,
    Execute = 8
}

[Flags]
public enum ResultFlags : byte
{
    None = 0,
    Headshot = 1 << 0,
    Backstab = 1 << 1,
    Weakness = 1 << 2,
    Resistance = 1 << 3,
    PoiseBreak = 1 << 4
}
```

### 3. The Bridge: `DamageNumberBridgeSystem`
Reads the buffer and maps `HitType` + `ResultFlags` to specific `DamageNumbersPro` prefab configurations.

### 4. The View: `DamageNumbersProAdapter`
Extended to handle the complex mapping of types to visuals.

---

---

## Performance Architecture: DOTS & Burst

The system is architected to maximize parallelism where possible, respecting the limitations of Managed UI APIs.

| Component | Technology | Threading | Details |
|-----------|------------|-----------|---------|
| **Damage Calculation** | **Burst + Jobs** | **Multi-Threaded** | `DamageApplySystem` uses `[BurstCompile]` and `.ScheduleParallel()` to process thousands of damage events across worker threads. Writing to `DamageResult` buffers is thread-safe via parallel writers. |
| **Buffer Access** | **Entity Chunks** | **Cache-Friendly** | `DamageResult` is an `IBufferElementData` stored contiguously in memory (Chunk extraction), ensuring CPU cache efficiency when iterating. |
| **Visual Bridge** | **SystemBase** | **Main Thread** | `DamageNumberBridgeSystem` *must* run on the Main Thread to call `DamageNumbersPro` (GameObject API). However, it does **zero logic**—only reading values and passing them to the spawner. |
| **Spawning** | **Pooling** | **Managed** | `DamageNumbersPro` uses efficient object pooling. We pre-warm pools during loading screens to avoid runtime instantiation spikes. |

**Why not Async/Await?**
ECS Systems run per-frame. `Async/Await` is generally avoided in the hot loop of simulation to prevent context-switching overhead and GC allocations.

---

## AAA Optimization Requirements

This section ensures the system performs at a AAA level, handling high-density combat without frame drops or visual clutter.

### 1. Zero-Allocation Pooling
- **Requirement:** No `new` allocations during combat.
- **Implementation:** `DamageNumbersPro` handles pooling internally. We must ensure our bridge does not allocate managed objects (strings, lists) per hit.
- **String Handling:** Avoid `string.Format` in `Update`. Use pre-allocated `NativeArray<char>` or cached strings for static text ("CRIT!", "BLOCKED").

### 2. Priority & Culling System
When 100+ enemies are hit by an AoE, we cannot spawn 100+ damage numbers.

**Culling Logic (in Bridge System):**
1.  **Distance Culling:** If distance > X meters, do not spawn (unless Boss/Critical).
2.  **Frustum Culling:** If target is behind camera, do not spawn.
3.  **Occlusion Culling:** Raycast check to see if number would be visible (Optional - heavy).
4.  **Priority Sorting:**
    - If active number count > Max (e.g. 50):
        - Discard "Graze" (Low Priority).
        - Discard "Normal" (Medium Priority) if needed.
        - Always show "Critical" (High Priority).

### 3. Data-Driven Configuration (`DamageFeedbackProfile`)
Hardcoding logic in `DamageNumbersProAdapter` is fragile. We will move configuration to a ScriptableObject.

[CreateAssetMenu(menuName = "DIG/Combat/Damage Feedback Profile")]
public class DamageFeedbackProfile : ScriptableObject
{
    [Header("Hit Severity (Motion/Style)")]
    public DamageNumberProfile CriticalHit;
    public DamageNumberProfile NormalHit;
    public DamageNumberProfile GrazeHit;
    public DamageNumberProfile BlockedHit;
    
    [Header("Damage Types (Color/Font/Juice)")]
    public List<DamageTypeProfile> DamageTypes;

    [Header("Context Events")]
    public DamageNumberProfile HeadshotText;
    public DamageNumberProfile BackstabText;
    
    [Header("Settings")]
    public float CullDistance = 50f;
    public int MaxActiveNumbers = 50;

    // Helper to find profile by type
    public DamageTypeProfile GetProfile(DamageType type) => DamageTypes.Find(x => x.Type == type);
}

[System.Serializable]
public struct DamageNumberProfile
{
    public DamageNumber Prefab;
    public MMF_Player Feedback; // General feedback (e.g. Critical Hit Shake)
    public float ScaleMultiplier;
    public Color ColorOverride;
    public bool UseColorOverride;
}

[System.Serializable]
public struct DamageTypeProfile
{
    public DamageType Type;
    public string DisplayName;      // Localized Key
    public Color Color;             // Primary Color (Fire=Red)
    public TMP_FontAsset Font;      // Specific Font (e.g. Jagged for Lightning)
    public MMF_Player HitFeedback;  // Specific Feel (e.g. Screen Tint, Glitch)
    public float SizeMultiplier;    // e.g. Heavy attacks = larger
}
```

### Visual Combination Matrix
The final visual is a composition of **Hit Severity** and **Damage Type**:

| Component | Determined By | Example |
|-----------|---------------|---------|
| **Motion** | Hit Severity | *Critical* (Pop-in) or *Graze* (Slide-down) |
| **Prefab** | Hit Severity | *Critical* (Big Text) |
| **Color** | Damage Type | *Fire* (Orange) |
| **Font** | Damage Type | *Void* (Distorted Font) |
| **Juice** | Both | *Critical* Shake + *Fire* Chromatic Aberration |


---

## Implementation Tasks

### Phase 1: Core Architecture
- [x] **Extend Enums**
    - [x] Update `HitType` in `IndicatorThemeContext.cs` with Blocked, Parried, Immune, Execute.
    - [x] Create `ResultFlags` enum (Headshot, Backstab, Weakness, Resistance, PoiseBreak).
- [x] **Add ResultFlags to Combat Pipeline** (replaces DamageResult buffer approach)
    - [x] Add `ResultFlags` field to `CombatResult` struct.
    - [x] Add `ResultFlags` field to `CombatResultEvent` struct.
    - [x] Pass flags through in `CombatResolutionSystem.OnUpdate()`.
- [x] **Update `CombatUIBridgeSystem`** (existing managed bridge, no new system needed)
    - [x] Pass `ResultFlags` through to `IDamageNumberProvider`.
    - [x] Handle defensive HitTypes (Blocked, Parried, Immune) for UI routing.
    - [x] Show contextual floating text for special flags (PoiseBreak).
    - [x] Execute-type feedback (longer hit stop, kill marker).
- **Note:** `DamageResult` IBufferElementData and `DamageNumberBridgeSystem` were NOT created.
  The existing `CombatResultEvent` + `CombatUIBridgeSystem` pipeline already provides
  this functionality. Creating new ECS components on ghost entities caused host-time errors.

### Phase 2: Visual Configuration (Data-Driven)
- [x] **Create `DamageFeedbackProfile` ScriptableObject**
    - [x] Define structure for mapping HitType -> DamageNumberProfile (prefab, scale, color).
    - [x] Define DamageTypeProfile for elemental configurations.
    - [x] Create asset `DefaultDamageFeedbackProfile` via Editor tool (DIG > Setup > Create Default Damage Feedback Profile).
- [x] **Update `DamageNumbersProAdapter`**
    - [x] Add prefab fields for Parried, Immune, Execute hit types.
    - [x] Implement `ShowDamageNumber` overload with `ResultFlags` parameter.
    - [x] Implement `SpawnDefensiveText` for BLOCKED/PARRY!/IMMUNE feedback.
    - [x] Show contextual text (HEADSHOT, BACKSTAB, BROKEN) via left text.
    - [x] Show efficacy indicators (▲ weakness, ▼ resistance) via top text.
    - [x] Cache all display strings as `static readonly` for zero-alloc.
- [x] **Update `DamageNumberAdapterBase`**
    - [x] Add colors for Blocked, Parried, Immune, Execute hit types.
    - [x] Add scales for Execute, Blocked, Parried hit types.
    - [x] Update `GetHitTypeColor`, `GetScale`, `GetFinalColor` for new types.
- [x] **Update `DamageNumberConfig` ScriptableObject**
    - [x] Add prefab slots for Parried, Immune, Execute.
    - [x] Add format strings for new hit types.
    - [x] Add culling settings (CullDistance, MaxActiveNumbers).
    - [x] Update `GetPrefab()` and `GetFormat()` for new HitTypes.
- [x] **Extend `IDamageNumberProvider` Interface**
    - [x] Add `ShowDamageNumber` overload with `ResultFlags` (default interface method).
    - [x] Add `ShowDefensiveText` method (default interface method).

### Phase 3: AAA Enhancements
- [x] **Implement Culling Logic**
    - [x] Distance check in `DamageNumbersProAdapter` (sqrMagnitude vs cullDistance²).
    - [x] Frustum check (GeometryUtility.TestPlanesAABB with per-frame cached planes).
    - [x] Priority count check (5-tier priority: Graze=0 through Execute=4, never cull Critical/Execute).
- [x] **Optimize String Usage**
    - [x] Cache common strings ("CRIT!", "BLOCKED", "MISS", etc.) as static readonly.
    - [x] Use pre-allocated strings throughout adapter.

### Phase 4: Integration & Testing
- [ ] **Test: Critical Feedback**
    - [ ] Force Crit in debug menu -> Verify "CRIT!" visuals and screen shake (if applicable).
- [ ] **Test: Mitigation**
    - [ ] Spawn Shielded Enemy -> Attack -> Verify "BLOCKED" text.
- [ ] **Test: Elemental Matchups**
    - [ ] Attack with Weak element -> Verify "RESIST" or grey number.
- [ ] **Test: Performance**
    - [ ] Spawn 50 enemies, AoE attack -> Verify framerate stability (Asset pooling).

---

## Architectural Design Decisions

### Why Event-Driven? (vs State Machine)
The user asked about **State Machines**. For *Floating Text*, we explicitly choose an **Event-Driven Architecture** over a State Machine for the following reasons:

1.  **Transient Nature**: Damage numbers are "fire-and-forget". They do not have complex persistent states (like an enemy AI moving `Idle` -> `Alert` -> `Attack`).
2.  **Decoupling**: A State Machine would require the Visual System to track the detailed state of the Combat System. By using Events (`DamageResult`), visuals are purely reactive.
3.  **Performance**: Evaluating state transitions for hundreds of particles is expensive. Processing a linear buffer of events is cache-friendly and fast.

### Where State *Does* Exist
While the text system is stateless, it *visualizes* state managed by other systems:
-   **Combo State**: Managed by `ComboSystem` (ECS). The text system just receives `ResultFlags.Combo` and the count.
-   **Stagger/Brokent State**: Managed by `PoiseSystem`. The text system receives `ResultFlags.PoiseBreak`.

**Conclusion:** Use State Machines for *Game Logic* (Combat), use Event Streams for *Feedback* (Visuals).

---

## Modularity & Replaceability

This system is designed using the **Observer Pattern** to ensure complete decoupling between Core Gameplay and Visuals.

### The Decoupling Point: `DamageResult`
The `DamageResult` ECS component is a pure data contract.
- **Core Systems (`DamageApplySystem`)** only know how to *write* data. They **do not** call visual code.
- **Bridge Systems** only know how to *read* data.

### Replacement Scenario: "Swapping the Asset"
*Scenario: We want to replace `DamageNumbersPro` with a different system (e.g. `Feel` or a custom UI).*

1.  **Keep**: `DamageApplySystem`, `DamageResult` (No core logic changes).
2.  **Delete**: `DamageNumberBridgeSystem`, `DamageNumbersProAdapter`.
3.  **Create**: `NewVisualBridgeSystem`.

**Cost to Swap:** Low (Minutes). No risk of breaking combat logic.

---

## Accessibility & Player Options
AAA games must account for visual impairments and player preference.

### 1. Colorblind Support
- **Implementation:** `DamageFeedbackProfile` supports override sets for Protanopia, Deuteranopia, Tritanopia.
- **Runtime:** `DamageNumbersProAdapter` listens for `SettingsChanged` event and hot-swaps the active profile.

### 2. Text Scaling
- **Implementation:** Global scale multiplier applied in `DamageNumbersProAdapter.Spawn()`.
- **Option:** Value from 0.5x to 2.0x in settings.

### 3. "Floaty" Style Preference
- **Option:** Toggle between "Stacking" (accumulating numbers) and "Floating" (individual numbers).
- **Implementation:** Handled by switching `DamageNumbersPro` prefab preset references in the Profile.

---

## Network Prediction Strategy
*Critical for "Game Feel"*

If we wait for server confirmation, damage numbers will lag behind the hit by RTT/2 (e.g. 100ms). This feels unresponsive.

1.  **Prediction:** `DamageApplySystem` runs in `PredictedFixedStepSimulationSystemGroup`.
2.  **Local Execution:** On the client controlling the attacker, we **predict** the damage and write to `DamageResult`.
3.  **Visualization:** `DamageNumberBridgeSystem` consumes the *predicted* result immediately.
4.  **Reconciliation:** If the server rejects the hit (rare), we do *not* rollback the text (it's already gone).
    - *Outcome:* Occasional "phantom" number on laggy connections, but **instant** feedback for 99% of hits.
    - *Tradeoff:* Responsive > 100% Accurate for transient VFX.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Visual Clutter** | Screen filled with numbers in AoE | Rigorous Culling & Priority system; "Stacking" mode for DOTs. |
| **Performance Spikes** | GC allocs during intense combat | Zero-alloc pooling; Pre-allocated strings; Throttled spawning (max X per frame). |
| **Desync** | Health bar drops but no text (or vice versa) | Ensure `DamageResult` is written in the exact same frame as `Health` modification. |

---

---

## Success Criteria

1.  **Visual Parity**: Damage numbers match the quality and responsiveness of `DamageNumbersPro` demos.
2.  **Performance**: Handling 50+ concurrent damage events without GC allocs or FPS drop.
3.  **Readability**: Players can clearly distinguish a Critical Hit vs a Blocked Hit in a chaotic fight.
4.  **Extensibility**: A designer can create a new "Poison" damage type and assign it a purple color/icon without code changes.
5.  **Modularity**: The visualization system can be disabled (e.g. for a "Hardcore" mode) without affecting damage calculation.

---

---

## Further Best Practices

### 1. Audio Integration
Visuals should not be silent. The *Impact* feeling comes from AV sync.
-   **Implementation:** `DamageNumberBridgeSystem` triggers an FMOD/Audio event for specific `HitTypes` (e.g. `PlaySound("CriticalHit")`).
-   **Culling:** Audio events must be culled aggressively (max 3 sounds per frame) to prevent "machine gun" clipping artifacts.

### 2. Localization
Text like "BLOCKED", "IMMUNE", "CRIT" must be localized.
-   **Implementation:** Store keys ("COMBAT_BLOCKED") in `DamageFeedbackProfile`.
-   **Runtime:** Adapter translates keys via the Localization Manager before spawning.

### 3. UI Sorting (World vs Screen)
-   **Decision:** We use **World Space** simulation (numbers fly out of the enemy) but render on a high-priority sorting layer.
-   **Best Practice:** Ensure damage numbers render **in front of** the enemy mesh but **behind** the vital Health Bar UI, to avoid obscuring the most critical information (how much HP is left).

---

## Migration Guide

- `DamageNumbersProAdapter` API will change signature slightly (taking `ResultFlags`).
- Existing calls to `ShowDamageNumber` in other systems (if any) will need update.
