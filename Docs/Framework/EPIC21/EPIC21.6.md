# EPIC 21.6: Debug & Validation Tools (9.5)

**Status**: 🔲 NOT STARTED  
**Priority**: MEDIUM  
**Estimated Effort**: 3 days  
**Dependencies**: 21.1 (Networking Split)

---

## Goal

Complete EPIC 9.5 - Create comprehensive debug and validation tools for the voxel system.

---

## Current State

Existing tools:
- `VoxelDebugWindow` - Basic debug info
- `WorldSliceViewer` - Horizontal slice view
- `CollisionTester` - Physics validation

Missing:
- World data validator
- Mesh integrity checker
- Memory leak detector
- Configuration validator

---

## Tasks

### Phase 1: World Data Validator
- [ ] Verify chunk integrity (no null blobs)
- [ ] Check for orphaned chunks
- [ ] Validate material IDs in range
- [ ] Report issues with fix suggestions

### Phase 2: Mesh Integrity Checker
- [ ] Detect degenerate triangles
- [ ] Check normal consistency
- [ ] Validate UV ranges
- [ ] Identify mesh seam issues

### Phase 3: Memory Health Monitor
- [ ] Track blob allocations
- [ ] Detect potential leaks
- [ ] Show memory per chunk
- [ ] Alert on threshold exceeded

### Phase 4: Configuration Validator
- [ ] Validate all ScriptableObjects
- [ ] Check for missing references
- [ ] Verify material texture assignments
- [ ] Pre-play validation button

### Phase 5: Integration
- [ ] Add to VoxelSetupDashboard
- [ ] One-click "Validate All" button
- [ ] Export report to file

---

## UI Requirements

- Traffic light status (Green/Yellow/Red)
- Detailed issue list with line numbers
- Fix suggestions where possible
- Export to JSON/CSV for CI integration

---

## Success Criteria

- [ ] All validators operational
- [ ] False positive rate < 5%
- [ ] Validation completes in < 5 seconds
- [ ] Clear, actionable error messages
