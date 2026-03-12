# Epic 9.4: Gameplay Integration

**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Dependencies**: EPIC 8.7 (Voxel Destruction + Loot)  
**Estimated Time**: 2 days  
**Last Updated**: 2025-12-20

---

## Quick Start Guide

### For Designers

1. **Open Explosion Tester**
   - `DIG → Voxel → Explosion Tester`
   - Adjust radius (1-30) and strength (0.1-1.0)
   - Click "Start Placement Mode" to place explosions in Scene View

2. **Create Hazardous Materials**
   - Edit `VoxelHazardDetectionSystem.HazardConfigs` array
   - Assign MaterialID to match your VoxelMaterialDefinition
   - Choose hazard type: Fire, Toxic, Radiation, Crystal, Explosive, Freezing

3. **Integrate with Weapons**
   - Use `VoxelExplosion.CreateCrater()` for explosive weapons
   - Use `VoxelToolFactory.CreateDrill()` for drill-type tools

### For Developers

1. **Key Files**
   ```
   Assets/Scripts/Voxel/Systems/Interaction/
   ├── VoxelExplosionSystem.cs   # Crater creation, loot spawning
   ├── VoxelHazardSystem.cs      # Environmental hazards
   ├── VoxelToolInterface.cs     # IVoxelTool, DrillTool, ExplosiveTool
   └── VoxelInteractionSystem.cs # Existing mining (unmodified)
   
   Assets/Scripts/Voxel/Editor/
   └── VoxelExplosionTesterWindow.cs  # Scene View explosion testing
   ```

2. **Integration Points**
   - `VoxelExplosion.CreateCrater()` - Queue crater creation
   - `VoxelExplosion.CreateCraterImmediate()` - Blocking crater creation
   - `VoxelToolFactory.CreateDrill()` - Get drill tool instance
   - `VoxelToolFactory.CreateExplosive()` - Get explosive tool instance

---

## Component Reference

### CreateCraterRequest

```csharp
public struct CreateCraterRequest : IComponentData
{
    public float3 Center;      // World position of explosion
    public float Radius;       // Crater radius in world units
    public float Strength;     // 0-1, affects edge destruction probability
    public byte ReplaceMaterial; // What to fill crater with (usually AIR)
    public bool SpawnLoot;     // Whether to emit VoxelDestroyedEvents
}
```

### CraterCreated

```csharp
public struct CraterCreated : IComponentData
{
    public float3 Center;
    public float Radius;
    public int VoxelsDestroyed;
    public float Timestamp;
}
```
- Created after crater is processed
- Use for VFX, audio cues, achievements

### VoxelHazardZone

```csharp
public struct VoxelHazardZone : IComponentData
{
    public float3 Position;
    public float Radius;
    public VoxelHazardType HazardType;  // Fire, Toxic, Radiation, etc.
    public float Intensity;             // 0-1, affects damage
    public float Duration;              // -1 = permanent
    public byte SourceMaterial;
}
```

### VoxelHazardType Enum

| Value | Effect |
|-------|--------|
| `None` | No effect |
| `Fire` | Burns nearby entities, can spread |
| `Toxic` | Damage over time in radius |
| `Radiation` | Reduces max health |
| `Crystal` | Creates light source |
| `Explosive` | Chain reaction when triggered |
| `Freezing` | Slows movement, DOT |

---

## System Architecture

### Explosion Flow

```
[Weapon/Tool fires]
        │
        ▼
┌─────────────────────────────┐
│ VoxelExplosion.CreateCrater │  Static helper
│ Creates CreateCraterRequest │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│ VoxelExplosionSystem        │  (Server/Local only)
│ OnUpdate():                 │
│   - Find affected chunks    │
│   - Remove voxels in sphere │
│   - Emit VoxelDestroyedEvent│
│   - Mark chunks for remesh  │
│   - Create CraterCreated    │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│ LootSpawnServerSystem       │  Spawns loot from events
│ ChunkMeshingSystem          │  Updates visuals
│ VFX/Audio Systems           │  React to CraterCreated
└─────────────────────────────┘
```

### Tool Integration Flow

```
[Player holds fire button]
        │
        ▼
┌─────────────────────────────┐
│ IVoxelTool.ApplyEffect()    │
│   - Raycast to find voxel   │
│   - Accumulate damage       │
│   - Check material hardness │
└────────────┬────────────────┘
             │
  [Damage >= 100?]
             │ Yes
             ▼
┌─────────────────────────────┐
│ Create VoxelModificationReq │  Sent to server
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│ VoxelModificationServerSys  │  Apply + broadcast
└─────────────────────────────┘
```

---

## Setup Guide

### 1. Using Explosion System

```csharp
// Option A: Queued (recommended)
VoxelExplosion.CreateCrater(
    entityManager, 
    explosionPosition, 
    radius: 8f, 
    strength: 1f, 
    spawnLoot: true
);

// Option B: Immediate (blocks until complete)
int voxelsDestroyed = VoxelExplosion.CreateCraterImmediate(
    entityManager, 
    explosionPosition, 
    radius: 8f
);
```

### 2. Integrating Drill Tool

```csharp
// Create drill tool
var drill = VoxelToolFactory.CreateDrill(damagePerSecond: 20f, range: 5f);

// In your Update loop:
if (playerIsDrilling)
{
    bool madeHole = drill.ApplyEffect(
        entityManager, 
        playerPosition, 
        aimDirection, 
        deltaTime
    );
    
    // Show mining progress UI
    float progress = (drill as DrillTool).GetProgress();
    miningProgressBar.value = progress;
}
```

### 3. Integrating Explosive Weapons

```csharp
// Create explosive tool
var rocket = VoxelToolFactory.CreateExplosive(radius: 12f);

// On projectile impact:
void OnProjectileHit(Vector3 hitPoint)
{
    (rocket as ExplosiveTool).Detonate(
        entityManager, 
        hitPoint, 
        radiusMultiplier: 1.0f
    );
}
```

### 4. Creating Hazard Zones

```csharp
// Create a toxic gas zone
VoxelHazards.CreateHazardZone(
    entityManager,
    position: gasCanisterPosition,
    type: VoxelHazardType.Toxic,
    radius: 5f,
    intensity: 0.5f,
    duration: 30f  // Lasts 30 seconds
);

// Chain explosion
VoxelHazards.TriggerChainExplosion(
    entityManager,
    center: barrelPosition,
    radius: 6f,
    strength: 1f
);
```

---

## Integration Guide

### With Weapon System

```csharp
// In your weapon's fire/impact handler:
public class RocketLauncher : MonoBehaviour
{
    public float explosionRadius = 10f;
    
    void OnProjectileHit(Vector3 hitPosition)
    {
        // Get appropriate world
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        
        // Create terrain crater
        VoxelExplosion.CreateCrater(
            world.EntityManager,
            hitPosition,
            explosionRadius,
            strength: 1f,
            spawnLoot: true
        );
        
        // Your other effects (particles, sound, damage)
        PlayExplosionVFX(hitPosition);
        ApplyDamageInRadius(hitPosition, explosionRadius);
    }
}
```

### With Damage System

```csharp
// React to CraterCreated for damage
[UpdateAfter(typeof(VoxelExplosionSystem))]
public partial class ExplosionDamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (crater, entity) in 
            SystemAPI.Query<CraterCreated>().WithEntityAccess())
        {
            // Apply damage to entities in radius
            ApplyExplosionDamage(crater.Center, crater.Radius);
            
            // Clean up marker
            ecb.DestroyEntity(entity);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
```

### With Item/Inventory System

```csharp
// Custom tool with inventory integration
public class InventoryDrill : IVoxelTool
{
    private Item _drillItem;
    
    public bool ApplyEffect(EntityManager em, float3 pos, float3 dir, float dt)
    {
        // Check durability
        if (_drillItem.Durability <= 0) return false;
        
        // Apply effect
        bool success = base.ApplyEffect(em, pos, dir, dt);
        
        if (success)
        {
            // Consume durability
            _drillItem.Durability -= 1;
        }
        
        return success;
    }
}
```

---

## Network Synchronization
**Critical for Multiplayer**:
Voxel data (stored in `BlobAssetReference`) is not automatically replicated via NetCode ghost components due to its size. To ensure explosions are visible on all clients:

1.  **Server Authority**: The Server processes the `CreateCraterRequest` to update physics, spawn loot, and validate the action.
2.  **Client Visuals**: Clients must *also* receive a `CreateCraterRequest` to verify the voxel removal locally and trigger chunk remeshing.
3.  **Implementation**: Use an RPC (Remote Procedure Call) to broadcast the explosion parameters to all clients.

**Example RPC Logic**:
```csharp
[Rpc(SendTo.ClientsAndHost)]
void TriggerExplosionRpc(float3 center, float radius) {
    // Client-side visual update + prediction
    VoxelExplosion.CreateCrater(em, center, radius, spawnLoot: false);
}

// Server logic
void OnFire() {
    // Server authoritative update + loot
    VoxelExplosion.CreateCrater(em, center, radius, spawnLoot: true);
    TriggerExplosionRpc(center, radius);
}
```

---

## Editor Tools

### Explosion Tester Window

**Access**: `DIG → Voxel → Explosion Tester`

**Features**:
- **Crater Settings**: Radius (1-30), Strength (0.1-1.0), Spawn Loot toggle
- **Placement Mode**: Click in Scene View to detonate
- **Network Awareness**: Spawns authoritative requests on Server. The `VoxelExplosionNetworkSystem` automatically broadcasts these to all connected Clients.
- **Statistics**: Total explosions, voxels destroyed, time tracking
- **Impact Estimation**: Predicted voxels and affected chunks

**Scene View Integration**:
- Preview sphere shows crater size
- Wire circles indicate explosion radius
- Click to detonate, Escape to exit placement mode

---

## Tasks Completed

### Task 9.4.1: Drill Tool Integration ✅
- `IVoxelTool` interface for standardized tool behavior
- `DrillTool` class with damage accumulation, material hardness
- `VoxelToolFactory` for easy tool creation
- Integration guide with existing `VoxelInteractionSystem`

### Task 9.4.2: Explosion Crater System ✅
- `CreateCraterRequest` component for queued explosions
- `VoxelExplosionSystem` processes requests on server/local
- Strength-based edge falloff for natural-looking craters
- Loot spawning via `VoxelDestroyedEvent`
- `CraterCreated` marker for VFX/audio triggers

### Task 9.4.3: Resource Collection ✅
- Integrated with existing `LootSpawnServerSystem`
- Explosions emit `VoxelDestroyedEvent` for each voxel
- Material-based loot drops via `VoxelMaterialRegistry`

### Task 9.4.4: Environmental Hazards ✅
- `VoxelHazardType` enum with 6 hazard types
- `VoxelHazardZone` component for hazard areas
- `VoxelHazardDetectionSystem` for exposure detection
- `VoxelHazardDamageSystem` for damage application
- Static helper `VoxelHazards` for easy zone creation

---

## Acceptance Criteria

- [x] Drill removes voxels at aim point (via IVoxelTool interface)
- [x] Explosions create proportional craters
- [x] Loot spawned for destroyed voxels
- [x] Hazard materials framework in place
- [x] Editor tool for explosion testing

---

## Related Epics

| Epic | Relation |
|------|----------|
| EPIC 8.7 | Voxel destruction + loot (foundation) |
| EPIC 8.9 | Network modification sync |
| EPIC 8.15 | Loot spawning system |
| EPIC 9.3 | Network batching (optimizes explosion sync) |
| EPIC 11 | Item system (future tool durability integration) |
