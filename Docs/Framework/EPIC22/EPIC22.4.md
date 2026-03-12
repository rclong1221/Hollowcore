# EPIC 22.4: Editor Tools & Setup Wizard

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 1 week  
**Dependencies**: 22.1 (Assembly Definitions)

---

## Goal

Create comprehensive editor tools that make player setup quick and error-free.

---

## Current Problem

- No setup wizard (20+ components to manually add)
- No animator helper (10+ parameters to create)
- No configuration dashboard
- No prefab validator
- Competitors have these tools; we don't

---

## Deliverables

### 1. Player Setup Wizard
One-click player prefab creation:

```
Window → DOTS Character Controller → Create Player Prefab
```

Features:
- Select which modules to include (Core, Extended, Combat, Networking)
- Configure initial settings (height, radius, speeds)
- Auto-create ghost prefab structure
- Auto-setup physics layers
- Generate animator controller with all parameters

### 2. Animator Setup Helper
```
Window → DOTS Character Controller → Animator Setup
```

Features:
- List required animator parameters
- One-click "Add All Parameters" button
- Validate existing animator
- Generate state machine templates

### 3. Configuration Dashboard
```
Window → DOTS Character Controller → Configuration
```

Features:
- Single window for all settings
- Live preview of values
- Presets (Realistic, Arcade, Tank-like)
- Import/Export configurations

### 4. Prefab Validator
```
Window → DOTS Character Controller → Validate Prefab
```

Features:
- Check for missing components
- Verify physics layer setup
- Validate animator configuration
- List warnings and errors
- Auto-fix common issues

### 5. Layer Setup Tool
```
Window → DOTS Character Controller → Physics Layers
```

Features:
- Auto-create required layers (Player, PlayerCollision, Climbable)
- Configure collision matrix
- Apply to project settings

---

## Tasks

### Phase 1: Player Setup Wizard
- [ ] Create wizard window UI
- [ ] Implement prefab generation
- [ ] Add module selection (checkboxes)
- [ ] Implement settings configuration
- [ ] Add physics layer setup
- [ ] Generate animator controller

### Phase 2: Animator Helper
- [ ] Create animator setup window
- [ ] List all required parameters
- [ ] Implement "Add All" functionality
- [ ] Add validation for existing animator
- [ ] Create state machine templates

### Phase 3: Configuration Dashboard
- [ ] Create unified settings window
- [ ] Add tabs for each module
- [ ] Implement presets system
- [ ] Add import/export

### Phase 4: Prefab Validator
- [ ] Create validation window
- [ ] Implement component checks
- [ ] Add layer validation
- [ ] Implement auto-fix for common issues

### Phase 5: Layer Setup
- [ ] Create layer setup window
- [ ] Implement layer creation
- [ ] Configure collision matrix
- [ ] Apply to TagManager.asset

---

## Wizard UI Mockup

```
┌─────────────────────────────────────────────────┐
│     DOTS Character Controller Setup Wizard       │
├─────────────────────────────────────────────────┤
│                                                  │
│  Prefab Name: [Player                    ]       │
│                                                  │
│  ┌─ Modules ─────────────────────────────────┐  │
│  │ ☑ Core Movement (required)                │  │
│  │ ☑ Extended Actions (climb, mantle, slide) │  │
│  │ ☑ Combat (damage, ragdoll, tackle)        │  │
│  │ ☐ Networking (NetCode prediction)         │  │
│  │ ☑ Audio (footsteps, surface materials)    │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ┌─ Settings ────────────────────────────────┐  │
│  │ Height:    [1.8  ] m                      │  │
│  │ Radius:    [0.3  ] m                      │  │
│  │ Walk Speed: [3.0  ] m/s                   │  │
│  │ Run Speed:  [6.0  ] m/s                   │  │
│  │ Jump Force: [5.0  ]                       │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ☑ Setup Physics Layers                          │
│  ☑ Create Animator Controller                    │
│  ☑ Add to Current Scene                          │
│                                                  │
│         [ Cancel ]    [ Create Player ]          │
└─────────────────────────────────────────────────┘
```

---

## Success Criteria

- [ ] Player creation in < 1 minute with wizard
- [ ] Zero manual component addition required
- [ ] Animator auto-generated with all parameters
- [ ] Physics layers auto-configured
- [ ] Validation catches all common errors
