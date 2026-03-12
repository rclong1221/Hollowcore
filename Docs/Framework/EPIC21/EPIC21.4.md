# EPIC 21.4: Sample Scenes

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 3-4 days  
**Dependencies**: 21.3 (Code Extraction)

---

## Goal

Create 4-5 demo scenes that showcase the voxel engine capabilities.

---

## Current Problem

- No sample scenes for customers to learn from
- Asset store customers expect working demos
- New users have no reference implementation

---

## Target Structure

```
/Assets/Scripts/Voxel/Samples~/
├── Scenes/
│   ├── 01_BasicWorld.unity        # Minimal working world
│   ├── 02_MaterialShowcase.unity  # All materials demo
│   ├── 03_TerrainModification.unity # Mining/building demo
│   ├── 04_LODDemo.unity           # LOD system showcase
│   └── 05_NetworkedWorld.unity    # Multiplayer demo (optional)
├── Prefabs/
│   ├── SimplePlayer.prefab        # Basic FPS controller
│   └── VoxelCursor.prefab         # Selection indicator
└── Scripts/
    └── DemoController.cs          # UI + instructions
```

---

## Tasks

### Scene 1: BasicWorld
- [ ] Empty scene with VoxelWorldAuthoring
- [ ] Default terrain generation
- [ ] Simple fly camera
- [ ] On-screen instructions
- [ ] "It just works" in < 5 clicks

### Scene 2: MaterialShowcase
- [ ] Flat terrain with material zones
- [ ] All materials visible
- [ ] UI showing material names
- [ ] Triplanar texturing demo

### Scene 3: TerrainModification
- [ ] Player controller with raycast
- [ ] Left-click to mine
- [ ] Right-click to place
- [ ] Material selector UI

### Scene 4: LODDemo
- [ ] Large terrain (500m view)
- [ ] LOD visualization gizmos
- [ ] FPS counter
- [ ] LOD level indicator

### Scene 5: NetworkedWorld (Optional)
- [ ] Host/Join buttons
- [ ] Synchronized modifications
- [ ] Late-join sync demo
- [ ] Only if networking package installed

---

## Success Criteria

- [ ] Each scene runs without errors
- [ ] Each scene demonstrates one feature clearly
- [ ] Total setup time < 2 minutes per scene
- [ ] On-screen instructions for all scenes
- [ ] Screenshots for documentation
