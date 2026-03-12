# EPIC 21.9: Unit Tests & CI

**Status**: 🔲 NOT STARTED  
**Priority**: LOW  
**Estimated Effort**: 2-3 days  
**Dependencies**: 21.1 (Networking Split)

---

## Goal

Create comprehensive unit tests and prepare for CI/CD integration.

---

## Current State

- Tests/ folder exists with 4 files
- Limited coverage
- No CI integration

---

## Tasks

### Phase 1: Core Tests
- [ ] `CoordinateUtilsTests` - Coordinate conversion
- [ ] `VoxelDensityTests` - Density calculations
- [ ] `VoxelDataTests` - Blob creation/disposal
- [ ] `ChunkLookupTests` - Spatial lookup

### Phase 2: System Tests
- [ ] `GenerationSystemTests` - Chunk generation
- [ ] `MeshingSystemTests` - Mesh output validation
- [ ] `ModificationSystemTests` - Voxel modification
- [ ] `LODSystemTests` - LOD transitions

### Phase 3: Integration Tests
- [ ] Full chunk lifecycle test
- [ ] Multi-chunk generation test
- [ ] Modification across chunks test
- [ ] Networking sync test (if enabled)

### Phase 4: CI Configuration
- [ ] Create GitHub Actions workflow
- [ ] Run tests on PR
- [ ] Report coverage
- [ ] Build verification

---

## Test Structure

```
/Assets/Scripts/Voxel/Tests/
├── EditMode/
│   ├── CoordinateUtilsTests.cs
│   ├── VoxelDensityTests.cs
│   └── ...
├── PlayMode/
│   ├── GenerationSystemTests.cs
│   ├── MeshingSystemTests.cs
│   └── ...
└── DIG.Voxel.Tests.asmdef
```

---

## Coverage Targets

| Area | Target Coverage |
|------|-----------------|
| Core utilities | 90% |
| Components | 80% |
| Systems | 60% |
| Editor tools | 30% |

---

## Success Criteria

- [ ] 60%+ overall coverage
- [ ] All tests pass on clean project
- [ ] Tests run in < 2 minutes
- [ ] CI workflow operational
- [ ] No flaky tests
