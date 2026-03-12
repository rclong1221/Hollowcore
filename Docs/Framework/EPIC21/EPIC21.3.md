# EPIC 21.3: Game-Specific Code Extraction

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 2-3 days  
**Dependencies**: 21.1 (Networking Split)

---

## Goal

Move game-specific systems (loot, hazards, explosions) from core package to optional samples.

---

## Current Problem

These systems are DIG-specific and shouldn't be in a generic voxel engine:

| File | Issue |
|------|-------|
| `VoxelLootSystem.cs` | Game-specific loot mechanics |
| `VoxelHazardSystem.cs` | Fire/Toxic/Radiation specific to DIG |
| `VoxelExplosionSystem.cs` | Crater + loot spawning |
| `LootSpawnNetworkSystem.cs` | Game-specific networking |
| `LootPhysicsProxySystem.cs` | Game-specific physics |
| `LootPhysicsSettings.cs` | Game-specific config |

---

## Target Structure

```
/Assets/Scripts/Voxel/
├── Core/                    # Reusable core
├── Systems/                 # Core systems only
└── Samples~/                # Optional samples (Unity excludes from packages)
    ├── Gameplay/
    │   ├── LootSystem/
    │   │   ├── VoxelLootSystem.cs
    │   │   ├── LootSpawnNetworkSystem.cs
    │   │   └── LootPhysicsSettings.cs
    │   ├── HazardSystem/
    │   │   └── VoxelHazardSystem.cs
    │   └── ExplosionSystem/
    │       ├── VoxelExplosionSystem.cs
    │       └── VoxelExplosionNetworkSystem.cs
    └── Gameplay.asmdef
```

---

## Tasks

### Phase 1: Create Samples Structure
- [ ] Create `Samples~/Gameplay/` directory
- [ ] Create `Samples~/Gameplay/Gameplay.asmdef`
- [ ] Configure asmdef to reference DIG.Voxel.Core

### Phase 2: Extract Loot System
- [ ] Move `VoxelLootSystem.cs` to Samples
- [ ] Move `LootPhysicsSettings.cs` to Samples
- [ ] Move `LootPhysicsProxySystem.cs` to Samples
- [ ] Update namespace to `DIG.Voxel.Samples.Gameplay`

### Phase 3: Extract Hazard System
- [ ] Move `VoxelHazardSystem.cs` to Samples
- [ ] Update namespace

### Phase 4: Extract Explosion System
- [ ] Move `VoxelExplosionSystem.cs` to Samples
- [ ] Move `VoxelExplosionNetworkSystem.cs` to Samples
- [ ] Keep `VoxelExplosion.CreateCrater()` helper in Core (generic)

### Phase 5: Update References
- [ ] Update DIG game project to reference Samples
- [ ] Ensure core package compiles without Samples
- [ ] Update editor tool references

---

## Keep in Core (Generic)

| File | Reason |
|------|--------|
| `VoxelModificationSystem.cs` | Generic modification API |
| `VoxelToolInterface.cs` | Generic tool interface |
| `VoxelInteractionSystem.cs` | Generic raycast/interaction |

---

## Success Criteria

- [ ] Core package has no game-specific systems
- [ ] Samples compile as optional addon
- [ ] DIG game continues to work (references Samples)
- [ ] New users can ignore Samples folder
- [ ] Each sample is self-contained
