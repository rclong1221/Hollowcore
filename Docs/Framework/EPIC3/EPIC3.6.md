# EPIC 3.6: Ship Damage, Hull Breaches, and Repair Loop

**Priority**: MEDIUM  
**Goal**: Ship damage matters (breaches depressurize, fires/toxins create hazards), and players have a clear repair loop (welder + materials).  
**Dependencies**: Epic 3.1 (pressurization), Epic 2.3 (Welder), Survival `EnvironmentZone`

## Design Notes (Match EPIC7 Level of Detail)
- **Single writer**: server is the only writer of breach state; clients only present.
- **Breach impacts survival via zones**: depressurization should manifest as `CurrentEnvironmentZone` changes (oxygen required), not as custom oxygen drain hacks.
- **Repair loop is explicit**: a breach is resolved by welder action (and optionally resource cost), producing a clear “fix it” objective.
- **Multiple breaches**: interior remains unsafe until all critical breaches are repaired (or until a threshold is met).

## Components

**ShipHullSection** (IComponentData, on hull entities; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Current` | float | Quantization=100 | Hull HP |
| `Max` | float | No | Hull HP max |
| `IsBreached` | bool | Yes | True when breach active |
| `BreachSeverity` | float | Quantization=100 | 0..1 leak severity |

**HullBreach** (IComponentData, on breach entities; optional)
| Field | Type | Description |
|---|---|---|
| `InteriorZoneEntity` | Entity | Which zone gets affected |
| `LeakRate` | float | How fast interior degrades |

**WeldRepairable** (existing; on repairable entities)
- Use this as the repair surface tag for `ShipHullSection` entities where appropriate.

## Systems

**ShipHullDamageSystem** (SimulationSystemGroup, ServerWorld)
- Applies damage events to `ShipHullSection`
- Sets `IsBreached` when below threshold (or on explosion)

**DepressurizationSystem** (SimulationSystemGroup, ServerWorld)
- When any breach is active:
  - degrades interior safety (updates interior `EnvironmentZone`/zone volumes or ship state)
- Can be as simple as: breach active → interior becomes `Vacuum` until repaired

**ShipRepairIntegrationSystem** (SimulationSystemGroup, ServerWorld)
- Integrates with welder behavior:
  - welding a `WeldRepairable` hull section restores `ShipHullSection.Current`
  - when repaired above threshold, clears breach state

## Acceptance Criteria
- [ ] Breach reliably turns ship interior dangerous (oxygen drains) until repaired
- [ ] Welder repairs hull sections and can clear breaches
- [ ] Damage/repair state replicates cleanly to clients

## Implementation Status: ✅ IMPLEMENTED

## File Locations
```
Assets/Scripts/Runtime/Ship/Hull/
├── Components/
│   └── HullComponents.cs           # ShipHullSection (WeldRepairable reused from WelderTool)
├── Authoring/
│   └── HullSectionAuthoring.cs     # Bakes HullSection and WeldRepairable
└── Systems/
    ├── ShipHullDamageSystem.cs     # Applies ExplosionDamageEvent, updates breach state, syncs to WeldRepairable
    ├── ShipRepairIntegrationSystem.cs # Takes WeldRepairable changes, updates Hull, clears breaches
    └── DepressurizationSystem.cs   # Overrides LifeSupport zones to Vacuum if breaches exist
```

## How it Works
1. **Damage**: `ExplosiveDetonationSystem` emits `ExplosionDamageEvent`. `ShipHullDamageSystem` consumes it, reduces `ShipHullSection.Current`.
2. **Breach**: If `ShipHullSection.Current` drops below 50% max, `IsBreached` becomes true.
3. **Depressurization**: `DepressurizationSystem` detects any breached hull on a ship and forces the interior zone to `Vacuum`.
4. **Repair**: Players use Welder (existing tool). `WelderUsageSystem` heals `WeldRepairable`.
5. **Recovery**: `ShipRepairIntegrationSystem` sees healed `WeldRepairable`, updates `ShipHullSection`, and clears `IsBreached` when > 50%. Interior returns to `Pressurized` (if LifeSupport online).

## Integration Guide

### Adding to Custom Ships
To make your custom ships damageable and repairable:
1. Select the hull or wall GameObjects.
2. Add the `HullSectionAuthoring` component.
3. Set `MaxHealth` (default 200).
4. Ensure the GameObject has a **Collider**.
5. Ensure the GameObject is a child of a `ShipRoot`.

### Configuring Breach Thresholds
Currently, the breach threshold is hardcoded to **50% of Max HP**.
- **100% - 50% HP**: Intact.
- **< 50% HP**: Breached (Vacuum).
- **0% HP**: Full Breach.

## Testing Instructions

### Manual Testing
1. **Create Test Ship**: Use `GameObject > DIG - Test Objects > Ships > Complete Test Ship`.
2. **Setup**: Equip an **Explosive** (C4) and a **Welder**.
3. **Damage**: Place C4 on a wall and detonate.
4. **Verify Breach**:
   - Check the **PowerHUD** (top-left).
   - It should show **⚠ OXYGEN REQUIRED** (Blinking).
5. **Repair**:
   - Equip the Welder.
   - Hold Left Click on the damaged wall.
   - Watch the repair progress.
6. **Verify Recovery**:
   - Once wall is repaired > 50%, HUD should return to **PRESSURIZED**.

### Debugging
- Use the **Entity Debugger** to view `ShipHullSection` components.
- Check `IsBreached` and `Current` health values.

## Known Limitations
1. **Visuals**: No visual hole or particle effect appears for the breach yet.
2. **Audio**: No 'hissing' sound for air leak yet.
3. **All-or-Nothing**: A ship is either 100% Pressurized or 100% Vacuum. No room-based depressurization yet.

## Sub-Epics / Tasks

### Sub-Epic 3.6.1: Hull Sections + Breach Detection ✅
**Tasks**:
- [x] Define damage application path to `ShipHullSection` (via `ExplosionDamageEvent`)
- [x] Define breach threshold rules (Current < 50%)
- [x] Track breach severity for visuals and hazard intensity

### Sub-Epic 3.6.2: Depressurization Rules ✅
**Tasks**:
- [x] Define how breaches impact interior: any breach active → interior becomes `Vacuum`
- [x] Ensure only one system mutates the interior zone state (DepressurizationSystem runs after LifeSupportSystem)

### Sub-Epic 3.6.3: Repair Integration (Welder + Costs) ✅
**Tasks**:
- [x] Welding a `WeldRepairable` hull section restores `ShipHullSection.Current` (via sync system)
- [x] When repaired above threshold, clears breach state and restores interior safety

### Sub-Epic 3.6.4: QA Checklist
**Tasks**:
- [x] Create a breach (Explosive); confirm interior becomes oxygen-required
- [x] Repair breach (Welder); confirm interior returns to pressurized (if life support online)
- [x] Two breaches: repair one; interior remains unsafe until both repaired
