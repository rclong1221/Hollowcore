# EPIC 15.10 - Unified Voxel Destruction System Setup Guide

This guide covers Unity Editor setup for developers and designers working with the voxel destruction system.

---

## Quick Start

### Opening the Shape Designer

1. **Menu**: `DIG > Voxel > Shape Designer`
2. Use the **Shape** tab to configure destruction shapes
3. Toggle **"Show Preview in Scene"** to visualize in Scene View
4. Use **"Copy Code to Clipboard"** to generate factory method calls

---

## Designer Workflows

### Configuring Explosive Prefabs

1. **Select** the explosive prefab in Project window
2. **Find** the `ExplosiveStats` component
3. **Configure** the explosion shape:

| Field | Description | Default |
|-------|-------------|---------|
| Blast Radius | Radius for entity damage (meters) | 2-6m |
| Blast Damage | Maximum damage at center | 50-200 |
| Voxel Damage Radius | Radius for voxel destruction | 1-5m |
| Shape Type | `Sphere` (standard) or `Cone` (shaped charge) | Sphere |
| Cone Angle | Degrees for shaped charges (only for Cone) | 30° |
| Shape Length | Length for directional shapes (Cone/Cylinder) | 6m |

> **TIP**: Set `Shape Type` to `Cone` for cutting charges that blast in a direction instead of all around.

### Setting Up Vehicle Drills

1. **Add Component**: `VehicleDrillBuffer` to vehicle prefab
2. **Add Tag**: `HasVehicleDrills` component
3. **Configure** each drill in the buffer:

| Field | Description | Typical Values |
|-------|-------------|----------------|
| Local Offset | Offset from vehicle origin to drill tip | (0, 0, 2) |
| Local Direction | Forward direction of drill | (0, 0, 1) |
| Drill Radius | Destruction radius | 1-4m |
| Drill Length | Destruction depth | 2-8m |
| Damage Per Second | DPS when active | 50-200 |
| Shape Type | `Cylinder` (tunnel) or `Capsule` (beam) | Cylinder |
| Heat Generation Rate | Heat/second while drilling | 5-15 |
| Heat Dissipation Rate | Heat/second while idle | 6-10 |
| Max Heat | Threshold before auto-shutdown | 80-200 |

**Preset Configurations** (accessible via code):
- `VehicleDrill.SmallMiningDrill` - Standard mining vehicle
- `VehicleDrill.LargeTunnelDrill` - Heavy tunnel boring
- `VehicleDrill.ShipLaserDrill` - Spacecraft mining laser

### Setting Up Tool Bits

1. **Add Component**: `EquippedToolBit` to tool prefab
2. **Set Bit Type** from available presets:

| Bit Type | Effect | Best For |
|----------|--------|----------|
| StandardBit | Balanced damage | General use |
| DiamondBit | +50% damage, +30% resistance bonus | Hard materials |
| TungstenBit | +10% resistance, 3.5x durability | Long sessions |
| HollowCoreBit | -30% damage, sphere shape | Fast removal |
| ConicalBit | Cone shape, +20% damage | Directional drilling |
| FlatBit | Wide cylinder, shallow depth | Surface excavation |
| SamplingBit | -70% damage, +50% resistance | Ore sampling |
| ExcavatorBit | Large cylinder, +100% damage | Tunnel boring |
| LaserBit | Long capsule, laser damage | Precision cutting |

### Setting Up Melee Weapons

Enable voxel destruction for Pickaxes, Hammers, and custom melee weapons.

1. **Add Component**: `MeleeVoxelDamageConfig` to weapon prefab
2. **Add Component**: `MeleeVoxelHitState` (required for tracking hits)
3. **Configure**:

| Field | Description | Pickaxe Example | Hammer Example |
|-------|-------------|-----------------|----------------|
| Damage Type | Material interaction type | `Mining` | `Crush` |
| Voxel Damage | Damage per hit | 25 | 40 |
| Shape Type | `Point` (precise) or `Sphere` (area) | `Point` | `Sphere` (Radius 0.5) |

### Setting Up Explosive Placement Tools

For tools that place interaction executables (Dynamite, C4, Landmines).

1. **Add Component**: `ExplosivePlacementTool` to tool prefab
2. **Configure**:
   - **Explosive Prefab**: Reference to the spawned explosive entity prefab
   - **Placement Range**: Max distance (e.g., 2.0m)
   - **Can Place On Walls**: Check for sticky explosives (C4)
   - **Subsurface Placement**: Check for insertion into drilled holes (Dynamite)

3. **Remote Detonators**:
   - Add `RemoteDetonator` component to the detonator tool prefab
   - It automatically links to explosives placed by the owner


### Setting Up Instant Explosion Tools

For developer tools or weapons that cause immediate destruction (e.g., Debug Tool, Rocket Launcher).

1. **Add Component**: `InstantExplosionTool` to tool prefab
2. **Configure**:
   - **Explosion Radius**: Radius of the crater (3-8m)
   - **Explosion Strength**: Probability of destroying edge voxels (0.0-1.0)
   - **Effective Distance**: Max raycast distance (e.g., 100m)
   - **Cooldown**: Time between uses
   - **Voxel Layer**: Ensure `Voxel` layer is selected in the Raycast Mask

### Adding Chain-Triggerable Entities

1. **Add Component**: `ChainTriggerable` to explosive/hazard prefabs
2. **Configure** trigger behavior:

| Field | Description | Explosives | Gas Pockets |
|-------|-------------|------------|-------------|
| Trigger Radius | Detection range | 5m | 8m |
| Trigger Threshold | Minimum damage to trigger | 50 | 10 |
| Trigger Delay | Seconds before detonation | 0.1s | 0s |
| Max Chain Depth | Prevent infinite loops | 10 | 5 |

### Adding Environmental Hazards

1. **Add Component**: `EnvironmentalHazard` to hazard entity
2. **Set Hazard Type**:

| Type | Effect When Breached |
|------|----------------------|
| GasPocket | Sphere explosion |
| LavaFlow | Sustained heat damage |
| UnstableGround | Triggers collapse |
| ToxicVent | Releases poison |
| WaterPocket | Floods area |
| CrystalVein | Amplifies lasers |

---

## Developer Workflows

### Creating Destruction Requests (Runtime)

For weapons, tools, or gameplay systems that need to destroy voxels:

1. Create an entity with `EntityCommandBuffer.CreateEntity()`
2. Add a `VoxelDamageRequest` component using factory methods
3. The system pipeline handles validation and processing automatically

**Factory Methods Available**:
- `VoxelDamageRequest.CreatePoint(...)` - Single voxel
- `VoxelDamageRequest.CreateSphere(...)` - Explosions
- `VoxelDamageRequest.CreateCylinder(...)` - Drills
- `VoxelDamageRequest.CreateCone(...)` - Shaped charges
- `VoxelDamageRequest.CreateCapsule(...)` - Lasers
- `VoxelDamageRequest.CreateBox(...)` - Precision cuts

### Validation Configuration

Edit `VoxelDamageValidationSystem.cs` to adjust anti-cheat limits:

| Constant | Purpose | Default |
|----------|---------|---------|
| `MAX_RADIUS` | Maximum allowed destruction radius | 20m |
| `MAX_DAMAGE` | Maximum allowed damage per request | 1000 |
| `MIN_REQUEST_INTERVAL` | Minimum time between requests | 0.05s |

### Network Configuration

For client-side prediction, clients send `VoxelDamageRpcRequest` to server. Configure:
- Rate limiting in `VoxelDamageRpcReceiveSystem`
- Response handling in client systems

---

## Scene View Debugging

### Viewing Shape Gizmos

1. Select any entity with destruction-related components
2. Gizmos are automatically drawn when `VoxelShapeGizmos` is active
3. Colors indicate shape type:
   - **Cyan**: Sphere
   - **Yellow**: Cylinder
   - **Orange**: Cone
   - **Magenta**: Capsule
   - **Green**: Box

### Using the Shape Designer

1. Open `DIG > Voxel > Shape Designer`
2. Configure shape parameters in the **Shape** tab
3. Use **"Move to Scene Camera"** to position preview at camera location
4. Copy generated code with **"Copy Code to Clipboard"**

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Shapes not appearing | Check `VoxelHealthStorage` singleton exists |
| Damage not applying | Verify server-side systems are running |
| Chain reactions not triggering | Check `ChainDepth` < `MaxChainDepth` |
| Tool bits not affecting damage | Ensure `ToolBitModifier` component is added to request |
| Drills overheating too fast | Reduce `HeatGenerationRate` or increase `MaxHeat` |

---

## System Processing Order

```
VehicleDrillInputSystem
        ↓
VehicleDrillSystem → Creates VoxelDamageRequest entities
        ↓
ToolBitModifierSystem → Applies bit modifiers
        ↓
VoxelDamageValidationSystem → Anti-cheat, rate limiting
        ↓
VoxelDamageProcessingSystem → Shape queries, damage calculation
        ↓
VoxelHealthTrackingSystem → Per-voxel health, destruction queue
        ↓
ChainReactionSystem → Propagates to nearby triggerables
        ↓
EnvironmentalHazardSystem → Handles breached hazards
```
