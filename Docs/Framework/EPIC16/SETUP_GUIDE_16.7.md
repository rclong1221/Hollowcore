# SETUP GUIDE 16.7: Unified VFX Event Pipeline

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** SubScene for ECS singletons, VFX prefabs with ParticleSystem(s)

This guide covers Unity Editor setup for the unified VFX event pipeline. After setup, any system (Burst or managed) can spawn VFX by creating a `VFXRequest` entity — the pipeline handles budgeting, LOD, pooling, and cleanup automatically.

---

## What Changed

Previously, VFX spawning was scattered across multiple systems with inconsistent spawn patterns, no frame budget, and no LOD awareness. Surface impacts used a static queue, ground effects used another queue, and damage numbers had their own bridge — each with independent spawn logic and no throttling.

Now:

- **Single spawn pattern** — any system creates a `VFXRequest` entity with position, type ID, category, and priority
- **Per-category frame budgets** — 7 categories (Combat, Environment, Ability, Death, UI, Ambient, Interaction) each with independent caps
- **Distance-based LOD** — VFX automatically downgrade or cull based on camera distance (Full → Reduced → Minimal → Culled)
- **Priority-based culling** — when budget is exceeded, lowest-priority requests are culled first
- **Dynamic budget scaling** — optional frame-time adaptive system scales budgets up/down based on GPU load
- **5 quality presets** — Ultra/High/Medium/Low/Minimal for quick quality switching
- **Dissolve shader** — entities with dissolve-capable materials fade out via shader instead of sinking into the ground
- **Legacy bridges** — existing surface impact, ground effect, and damage visual queues automatically feed into the new pipeline
- **Ambient emitters** — place VFX emitters in the world for repeating/proximity-triggered ambient effects

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Budget enforcement | VFXBudgetSystem culls excess requests per category each frame |
| LOD resolution | VFXLODSystem assigns LOD tier based on camera distance |
| Request cleanup | VFXCleanupSystem destroys all VFXRequest entities each frame |
| Legacy bridges | SurfaceImpactVFXBridgeSystem, GroundEffectVFXBridgeSystem, DamageVisualVFXBridgeSystem drain existing static queues into VFXRequest entities |
| Dissolve on death | Entities with `DissolveCapable` tag use shader dissolve instead of position sinking (handled by CorpseDissolveSystem) |
| Telemetry | VFXTelemetry static counters track per-frame/per-category/per-LOD stats automatically |

All VFX systems run on `ClientSimulation | LocalSimulation` only — no server-side VFX processing.

---

## 1. VFX Type Database (Required)

The VFX Type Database maps integer type IDs to prefabs. Every VFX spawned through the pipeline references an entry in this database.

### 1.1 Create the Database

1. In the Project window, right-click in `Assets/Resources/`
2. Select **Create > DIG > VFX > VFX Type Database**
3. Name it `VFXTypeDatabase` (must be in a Resources folder for runtime loading)

### 1.2 Add Entries

Each entry in the database defines one VFX type:

| Field | Type | Description |
|-------|------|-------------|
| **Type Id** | int | Unique integer ID. Must be stable across sessions. See ID ranges below |
| **Name** | string | Human-readable name for debug/editor display |
| **Prefab** | GameObject | The VFX prefab with ParticleSystem(s) |
| **Default Category** | VFXCategory | Budget category if not overridden by the request |
| **Minimum LOD Tier** | EffectLODTier | Minimum LOD at which this VFX is visible |
| **Prewarm** | bool | Pool prewarm on startup |
| **Prewarm Count** | int | Number of instances to prewarm |
| **Max Instances** | int | Maximum simultaneous instances. 0 = unlimited |
| **Reduced Prefab** | GameObject | LOD-Reduced variant (null = use main prefab with emission reduction) |
| **Minimal Prefab** | GameObject | LOD-Minimal variant (null = skip at Minimal tier) |

### 1.3 Type ID Ranges

Use these ranges to keep IDs organized:

| Range | Category | Examples |
|-------|----------|---------|
| 1000–1099 | Combat | BulletImpactDefault (1000), MuzzleFlashRifle (1010), ProjectileTrailDefault (1020) |
| 2000–2199 | Ability / Elemental | AbilityFireBurst (2000), BuffApply (2100), DebuffApply (2101) |
| 3000–3099 | Death | DeathBloodSplatter (3000), DeathDissolve (3002) |
| 4000–4099 | Environment | FootstepDust (4000), WaterSplashSmall (4010) |
| 5000–5099 | Interaction / UI | LootGlow (5000), PickupFlash (5001), LevelUp (5003) |
| 6000–6099 | Ambient | AmbientDust (6000), AmbientFireflies (6001), AmbientEmber (6002) |

Predefined constants are in `VFXTypeIds.cs` — use these in code rather than raw integers.

---

## 2. Budget Config (Singleton)

Controls how many VFX requests per category are allowed per frame.

### 2.1 Add the Component

1. In your gameplay SubScene, create an empty GameObject named "VFXBudgetConfig"
2. Click **Add Component** > search for **VFX Budget Config Authoring**

### 2.2 Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Combat Budget** | int | 16 | Max combat VFX per frame (bullet impacts, muzzle flash) |
| **Environment Budget** | int | 24 | Max environment VFX per frame (footsteps, splashes) |
| **Ability Budget** | int | 12 | Max ability VFX per frame (spells, elemental effects) |
| **Death Budget** | int | 8 | Max death VFX per frame (blood, gibs, dissolve) |
| **UI Budget** | int | 20 | Max UI VFX per frame (pickup flash, level up) |
| **Ambient Budget** | int | 10 | Max ambient VFX per frame (dust, fireflies) |
| **Interaction Budget** | int | 8 | Max interaction VFX per frame (loot glow, crafting) |
| **Global Max Per Frame** | int | 64 | Hard cap across all categories combined |

When a category exceeds its budget, lowest-priority requests are culled first.

### 2.3 Budget Defaults by Quality

If no singleton exists, the system uses these defaults:

| Quality | Combat | Environment | Ability | Death | UI | Ambient | Interaction | Global |
|---------|--------|-------------|---------|-------|-----|---------|-------------|--------|
| Ultra | 32 | 48 | 24 | 16 | 40 | 20 | 16 | 128 |
| High | 16 | 24 | 12 | 8 | 20 | 10 | 8 | 64 |
| Medium | 8 | 12 | 6 | 4 | 10 | 5 | 4 | 32 |
| Low | 4 | 6 | 3 | 2 | 5 | 3 | 2 | 16 |
| Minimal | 2 | 3 | 2 | 1 | 3 | 1 | 1 | 8 |

---

## 3. LOD Config (Singleton)

Controls distance-based LOD tiers for VFX.

### 3.1 Add the Component

1. In your gameplay SubScene, create an empty GameObject named "VFXLODConfig"
2. Click **Add Component** > search for **VFX LOD Config Authoring**

### 3.2 Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Full Distance** | float | 15m | Below this: all particles, sub-emitters, trails |
| **Reduced Distance** | float | 40m | Below this: 50% emission, no sub-emitters |
| **Minimal Distance** | float | 80m | Below this: billboard sprite only. Beyond = culled entirely |

### 3.3 LOD Tiers

| Tier | Distance Range | What Renders |
|------|---------------|-------------|
| Full | 0–15m | All particles, sub-emitters, trails, full emission |
| Reduced | 15–40m | 50% particle count, no sub-emitters, simplified trails |
| Minimal | 40–80m | Billboard sprite only (uses MinimalPrefab from database if set) |
| Culled | 80m+ | Nothing — request is skipped entirely |

---

## 4. Quality Presets (Optional Singleton)

Provides one-click quality switching that adjusts both budget and LOD configs.

### 4.1 Add the Component

1. In your gameplay SubScene, create an empty GameObject named "VFXQuality"
2. Click **Add Component** > search for **VFX Quality Authoring**

### 4.2 Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Initial Preset** | VFXQualityPreset | High | Starting quality level |

### 4.3 Preset Levels

| Preset | Description |
|--------|-------------|
| **Ultra** | Maximum VFX fidelity. All effects at full emission with extended LOD distances |
| **High** | Default. Good balance of visual quality and performance |
| **Medium** | Reduced budgets and shorter LOD distances. Suitable for mid-range hardware |
| **Low** | Minimal VFX. Short LOD distances, low budgets |
| **Minimal** | Bare minimum. Only critical VFX (damage indicators, death) render |

Changing the preset at runtime sets `VFXQualityState.IsDirty = true`, which triggers `VFXQualityApplySystem` to update budget and LOD singletons.

---

## 5. VFX Emitters (Per-Entity)

Place VFX emitters in the world for ambient, repeating, or proximity-triggered effects.

### 5.1 Add the Component

1. Select any GameObject in your SubScene (torch, campfire, waterfall, etc.)
2. Click **Add Component** > search for **VFX Emitter Authoring**

### 5.2 Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **VFX Type Id** | int | 0 | Type ID from VFXTypeDatabase |
| **Category** | VFXCategory | Ambient | Budget category |
| **Intensity** | float | 1.0 | Intensity scalar (affects particle count/emission rate) |
| **Scale** | float | 1.0 | Uniform scale multiplier on spawned VFX |
| **Color Tint** | Color | Clear | Color override. Clear = use prefab default |
| **Duration** | float | 0 | Duration override in seconds. 0 = use prefab default |
| **Priority** | int | 0 | Priority within category. Higher survives budget culling |
| **Emission Mode** | VFXEmissionMode | OneShot | See modes below |
| **Repeat Interval** | float | 1.0 | Seconds between emissions (Repeating mode only) |
| **Trigger Radius** | float | 0 | Player proximity radius to trigger emission (Proximity mode only) |

### 5.3 Emission Modes

| Mode | Behavior | Use Case |
|------|----------|----------|
| **OneShot** | Emits once when entity spawns | Explosion aftermath, single spawn |
| **Repeating** | Emits every RepeatInterval seconds | Torch flames, steam vents, dripping water |
| **Proximity** | Emits when a player enters TriggerRadius | Cave dust when player walks near, glowing mushrooms |

### 5.4 Example Setups

**Torch:**

| Field | Value |
|-------|-------|
| VFX Type Id | 6002 (AmbientEmber) |
| Category | Ambient |
| Emission Mode | Repeating |
| Repeat Interval | 0.5 |
| Priority | -5 |

**Waterfall Splash:**

| Field | Value |
|-------|-------|
| VFX Type Id | 4011 (WaterSplashLarge) |
| Category | Environment |
| Emission Mode | Repeating |
| Repeat Interval | 0.2 |
| Scale | 2.0 |
| Priority | 5 |

**Cave Dust (proximity):**

| Field | Value |
|-------|-------|
| VFX Type Id | 6000 (AmbientDust) |
| Category | Ambient |
| Emission Mode | Proximity |
| Trigger Radius | 8.0 |
| Priority | -10 |

---

## 6. Dissolve Shader Setup

Entities with dissolve-capable materials fade out via shader instead of sinking into the ground on death.

### 6.1 Material Setup

1. Create a new Material (or duplicate an existing one)
2. Set shader to **DIG/URP/Dissolve**
3. Assign textures:

| Property | Description | Default |
|----------|-------------|---------|
| **Albedo** | Base color texture | white |
| **Normal Map** | Surface normal texture | bump |
| **Metallic (R) Smoothness (A)** | Combined metallic/smoothness map | white |
| **Dissolve Noise** | Grayscale noise texture driving dissolve pattern | white |

4. Configure dissolve parameters:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| **Dissolve Amount** | Range(0,1) | 0.0 | Main control. 0 = fully visible, 1 = fully dissolved |
| **Edge Width** | Range(0,0.15) | 0.04 | Width of the glowing dissolve edge |
| **Edge Color** | Color (HDR) | (3, 1.5, 0.3, 1) | Color of the emissive dissolve edge |
| **Dissolve Direction** | Vector3 | (0, 1, 0) | World-space direction for directional dissolve |
| **Use Directional** | Toggle | Off | If on, dissolves from bottom-to-top (or along direction). If off, noise-based dissolve |

### 6.2 Dissolve Capable Authoring

For the dissolve to activate on death, the entity needs a `DissolveCapable` tag.

1. Select the enemy/NPC prefab root
2. Click **Add Component** > search for **Dissolve Capable Authoring**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Auto Detect** | bool | true | Automatically searches child Renderers for materials using `DIG/URP/Dissolve` shader. Only adds the tag if found |

When Auto Detect is enabled, the baker scans all child Renderers. If none use the dissolve shader, the tag is not added — the entity falls back to the standard corpse sink behavior.

### 6.3 How Dissolve Works at Runtime

1. Entity dies → `DeathTransitionSystem` enables `CorpseState`
2. `CorpseLifecycleSystem` progresses through Ragdoll → Settled → Fading phases
3. During Fading: `CorpseDissolveSystem` drives `_DissolveAmount` from 0 → 1 via `MaterialPropertyBlock` on all renderers
4. `CorpseSinkSystem` skips entities with `DissolveCapable` (they dissolve instead of sinking)
5. Entity is destroyed when dissolve completes

---

## 7. Pipeline Execution Order

```
VFXEmitterSystem               → Creates VFXRequest entities from emitters
Legacy Bridges                 → Drain static queues → VFXRequest entities
  ├ SurfaceImpactVFXBridgeSystem
  ├ GroundEffectVFXBridgeSystem
  └ DamageVisualVFXBridgeSystem
        ↓
VFXBudgetSystem                → Per-category culling (enables VFXCulled on excess)
        ↓
VFXLODSystem                   → Distance-based LOD (adds VFXResolvedLOD, enables VFXCulled beyond max)
        ↓
VFXExecutionSystem             → Spawns VFX prefabs via VFXManager pooling
        ↓
VFXCleanupSystem               → Destroys all remaining VFXRequest entities
```

All systems run in `PresentationSystemGroup`, `ClientSimulation | LocalSimulation` only.

---

## 8. Editor Tools

### 8.1 VFX Workstation

**Menu:** DIG > VFX Workstation

A 4-tab editor window for live VFX monitoring during Play Mode. Refreshes at 4 Hz.

#### Budget Monitor Tab

Shows per-category budget usage as progress bars:
- Green: executed count within budget
- Yellow: approaching budget
- Red: over budget (requests being culled)
- Displays requested / executed / culled counts per category

#### LOD Visualizer Tab

Shows LOD distance thresholds and per-tier execution counts:
- Full / Reduced / Minimal / Culled tier counts
- Distance range display

#### Request Log Tab

Per-frame telemetry from `VFXTelemetry`:
- Total requested, executed, culled this frame
- Session totals
- Pool hit rate

#### Registry Browser Tab

Lists all entries from `VFXTypeDatabase`:
- Type ID, name, prefab reference, category, LOD tier
- Useful for verifying database completeness

---

## 9. Creating VFX Requests (For Programmers)

Any system can create a VFX request. From Burst-compiled systems, use an `EntityCommandBuffer`:

```csharp
var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);
var entity = ecb.CreateEntity();
ecb.AddComponent(entity, new VFXRequest
{
    Position = hitPoint,
    Rotation = quaternion.LookRotation(hitNormal, math.up()),
    VFXTypeId = VFXTypeIds.BulletImpactDefault,
    Category = VFXCategory.Combat,
    Priority = 0,
    Intensity = 1f,
    Scale = 1f,
});
ecb.AddComponent<VFXCulled>(entity);
ecb.SetComponentEnabled<VFXCulled>(entity, false);
ecb.AddComponent<VFXCleanupTag>(entity);
```

**Important:** Always add `VFXCulled` (disabled) and `VFXCleanupTag` to ensure proper pipeline processing and cleanup.

---

## 10. After Setup: Reimport SubScene

After adding or modifying VFX authoring components in a SubScene:

1. Right-click the SubScene > **Reimport**
2. Wait for baking to complete

---

## 11. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Database loads | Enter Play Mode | VFX Workstation Registry tab shows all entries |
| 3 | Combat VFX | Shoot a surface | Bullet impact VFX spawns at hit point |
| 4 | Budget culling | Rapid-fire in enclosed space | After budget cap, some impacts are culled |
| 5 | LOD transition | Walk away from VFX emitter | VFX downgrades at distance thresholds |
| 6 | LOD culling | Move beyond MinimalDistance | VFX stops rendering entirely |
| 7 | Emitter OneShot | Place OneShot emitter in scene | VFX spawns once on entity creation |
| 8 | Emitter Repeating | Place Repeating emitter (torch) | VFX spawns at interval |
| 9 | Emitter Proximity | Place Proximity emitter | VFX spawns when player enters radius |
| 10 | Dissolve on death | Kill enemy with dissolve material | Enemy dissolves via shader instead of sinking |
| 11 | Quality preset | Change preset at runtime | Budget and LOD thresholds update |
| 12 | VFX Workstation | Open DIG > VFX Workstation | Live budget/LOD/telemetry data visible |
| 13 | Priority works | Set boss VFX priority to 100, ambient to -10 | Under budget pressure, ambient culled first |

---

## 12. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No VFX spawning | VFXTypeDatabase not in Resources/ folder | Move to `Assets/Resources/VFXTypeDatabase.asset` |
| VFX type not found | TypeId mismatch between request and database | Verify type ID matches database entry |
| All VFX culled | Budget too low or quality preset too aggressive | Increase budgets or raise quality preset |
| Dissolve not working | Material doesn't use DIG/URP/Dissolve shader | Assign dissolve shader, re-add DissolveCapableAuthoring |
| Entity sinks instead of dissolving | DissolveCapable tag not baked | Add DissolveCapableAuthoring, ensure AutoDetect finds dissolve material |
| Emitter fires once then stops | EmissionMode set to OneShot | Change to Repeating or Proximity |
| Budget monitor shows zero | Not in Play Mode or wrong world | VFX systems only run in ClientSimulation/LocalSimulation |
| LOD distances wrong | VFXLODConfig singleton missing | Add VFXLODConfigAuthoring to SubScene |
| Quality preset change has no effect | VFXQualityAuthoring not in SubScene | Add VFXQualityAuthoring singleton |
| Dissolve edge color too bright | HDR color values too high | Reduce Edge Color intensity in material |

---

## 13. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Corpse lifecycle (death → dissolve trigger) | SETUP_GUIDE_16.3 |
| Surface impacts (legacy bridge source) | SETUP_GUIDE_16.10 |
| Ability VFX spawning | SETUP_GUIDE_15.32 |
| Loot glow VFX | SETUP_GUIDE_16.6 |
| **VFX event pipeline** | **This guide (16.7)** |
