# EPIC 22.5: Sample Scenes

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 3-4 days  
**Dependencies**: 22.1, 22.4 (Wizard)

---

## Goal

Create 4-5 demo scenes that showcase all character controller features.

---

## Target Structure

```
/Assets/Scripts/Player/Samples~/
├── Scenes/
│   ├── 01_BasicMovement.unity     # Core movement demo
│   ├── 02_ClimbingShowcase.unity  # All climbing features
│   ├── 03_CombatDemo.unity        # Damage, ragdoll, tackle
│   ├── 04_MultiplayerTest.unity   # Network prediction demo
│   └── 05_AllFeatures.unity       # Everything combined
├── Prefabs/
│   ├── DemoPlayer.prefab          # Pre-configured player
│   ├── DemoCamera.prefab          # Cinemachine setup
│   └── Climbables/                # Ladder, pipe, wall prefabs
├── Scripts/
│   ├── DemoController.cs          # UI + instructions
│   └── FeatureToggle.cs           # Enable/disable features
└── Documentation/
    └── SampleScenes.md
```

---

## Tasks

### Scene 1: BasicMovement
- [ ] Flat terrain with obstacles
- [ ] Demo: Walk, run, sprint, jump
- [ ] Demo: Crouch, slide
- [ ] On-screen control hints
- [ ] FPS/velocity display

### Scene 2: ClimbingShowcase
- [ ] Multiple climbable types:
  - Ladder
  - Pipe (horizontal and vertical)
  - Rock wall
  - Sphere/cylinder
  - Arch
  - Tower
- [ ] Mantle platforms (various heights)
- [ ] Instructions for each

### Scene 3: CombatDemo
- [ ] Damage sources (trigger zones)
- [ ] Health display
- [ ] Tackle targets (NPCs or dummies)
- [ ] Death/ragdoll demonstration
- [ ] Respawn system

### Scene 4: MultiplayerTest
- [ ] Host/Client buttons
- [ ] Split-screen or multi-window
- [ ] Prediction visualization
- [ ] Network stats display
- [ ] Latency simulation

### Scene 5: AllFeatures
- [ ] Parkour course combining all features
- [ ] Timer/scoring system
- [ ] Feature toggle panel
- [ ] Performance stats

---

## Demo Controller Features

```csharp
public class DemoController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI controlsText;
    public TextMeshProUGUI velocityText;
    public TextMeshProUGUI stateText;
    
    [Header("Feature Toggles")]
    public bool enableClimbing = true;
    public bool enableMantling = true;
    public bool enableSliding = true;
    public bool enableCombat = true;
    
    [Header("Debug")]
    public bool showVelocity = true;
    public bool showGroundState = true;
    public bool showNetworkStats = false;
}
```

---

## Success Criteria

- [ ] Each scene demonstrates features clearly
- [ ] On-screen instructions for all inputs
- [ ] Scenes run without errors
- [ ] Total setup time < 30 seconds per scene
- [ ] Performance stable at 60+ FPS
