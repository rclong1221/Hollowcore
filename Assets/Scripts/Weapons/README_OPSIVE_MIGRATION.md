# OPSIVE to DIG ECS Migration Guide

This guide provides comprehensive setup instructions for migrating OPSIVE Ultimate Character Controller features into your DOTS/ECS-based DIG game.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ANIMATION BRIDGE PATTERN                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  OPSIVE Animator Controller                    ECS World (Server + Client)  │
│  ─────────────────────────                     ────────────────────────────  │
│                                                                              │
│  ┌──────────────────────┐                     ┌──────────────────────────┐  │
│  │ Animation Clip fires │                     │ WeaponAnimationEvents    │  │
│  │ ExecuteEvent("Fire") │ ───────────────────>│ (Static Queue)           │  │
│  └──────────────────────┘                     └────────────┬─────────────┘  │
│            │                                               │                 │
│            │                                               ▼                 │
│  ┌─────────▼────────────┐                     ┌──────────────────────────┐  │
│  │ OpsiveWeaponAnim-    │                     │ WeaponAnimationEvent-    │  │
│  │ EventRelay.cs        │                     │ System.cs                │  │
│  │ (on Animator GO)     │                     │ (reads queue, updates    │  │
│  └──────────────────────┘                     │  ECS components)         │  │
│                                               └──────────────────────────┘  │
│                                                            │                 │
│                                                            ▼                 │
│  ┌──────────────────────┐                     ┌──────────────────────────┐  │
│  │ WeaponAnimatorBridge │ <───────────────────│ WeaponFireState,         │  │
│  │ (on Weapon Model GO) │                     │ WeaponAmmoState, etc.    │  │
│  │ Sets Animator params │                     │ (ECS Components)         │  │
│  └──────────────────────┘                     └──────────────────────────┘  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Prefab Hierarchy Reference

Your DIG project uses a **hybrid prefab structure** with separate Server and Client representations:

```
Warrok_Server (Ghost Prefab - Baked to ECS)
├── [ECS Components via Authoring]
│   ├── PlayerMovementAuthoring
│   ├── WeaponSwitchAuthoring        ◄── ADD HERE (Phase 3)
│   └── etc.
└── (No visual components)

Warrok_Client (Presentation Prefab - MonoBehaviour)
├── Warrok_Model (has Animator)
│   ├── Animator Controller (OPSIVE)
│   ├── AnimationEventRelay    ◄── ALREADY EXISTS (climbing)
│   ├── WeaponAnimationEventRelay ◄── ADD HERE (Phase 1)
│   └── ClimbAnimatorBridge          ◄── ALREADY EXISTS
├── WeaponAnchor
│   └── [Weapon Models attached here at runtime]
└── UI Elements

Weapon Prefabs (e.g., AssaultRifle_ECS)
├── WeaponAuthoring                  ◄── ALREADY EXISTS
├── Visual Model (has Animator for weapon-specific anims)
│   ├── Animator (weapon animations)
│   └── WeaponAnimatorBridge         ◄── ADD HERE (Phase 4)
└── Muzzle/Shell Eject Points
```

---

## Phase 1: Weapon Animation Bridge Setup

### Step 1.1: Add WeaponAnimationEventRelay to Character

**Location:** `Warrok_Client/Warrok_Model` (same GameObject as the Animator)

**Why this location?** Unity Animation Events can only call methods on MonoBehaviours attached to the **same GameObject** as the Animator component. Your OPSIVE Animator Controller is on `Warrok_Model`, so the relay must be there too.

**Setup:**
1. Select `Warrok_Client/Warrok_Model` in the hierarchy
2. Add Component → `WeaponAnimationEventRelay`
3. (Optional) Enable `Debug Logging` to verify events are firing

**Inspector Settings:**
```
WeaponAnimationEventRelay
├── Use Explicit Entity: false (auto-finds active weapon)
└── Debug Logging: true (disable in production)
```

### Step 1.2: Configure OPSIVE Animation Clips

Your OPSIVE animation clips should already have Animation Events. Verify they call `ExecuteEvent` with these names:

| Animation | Event Name | When Called |
|-----------|------------|-------------|
| Fire | `OnAnimatorItemFire` or `Fire` | Frame when bullet spawns |
| Reload Start | `OnAnimatorReloadStart` | First frame of reload |
| Reload Insert | `OnAnimatorReloadInsertAmmo` | Magazine inserted |
| Reload End | `OnAnimatorReloadComplete` | Reload finished |
| Melee Swing | `OnAnimatorMeleeStart` | Swing begins |
| Melee Hit | `OnAnimatorMeleeHitFrame` | Damage frame |
| Equip | `OnAnimatorEquipComplete` | Weapon ready |
| Unequip | `OnAnimatorUnequipComplete` | Weapon holstered |

**If clips don't have events:**
1. Open Animation window
2. Navigate to the clip (e.g., `Fire`)
3. Add Animation Event at the key frame
4. Set Function to `ExecuteEvent`
5. Set String parameter to the event name (e.g., `Fire`)

### Step 1.3: Verify WeaponAnimationEventSystem Runs

The system is already configured to run on both Client and Server:

```csharp
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(WeaponFireSystem))]
public partial class WeaponAnimationEventSystem : SystemBase
```

**No additional setup needed** - it automatically processes the static queue.

---

## Phase 2: Data Extraction Tools

### Step 2.1: Extract Weapon Stats (Editor Window)

**Open:** Tools → DIG → OPSIVE Weapon Extractor

**Workflow:**
1. Drag an OPSIVE weapon prefab (e.g., `AssaultRifle` from OPSIVE demo) to "Source OPSIVE Weapon"
2. Drag your ECS weapon prefab to "Target ECS Weapon"
3. Click "Extract from OPSIVE" to see extracted values
4. Click "Apply to ECS Weapon" to copy values to `WeaponAuthoring`

**What gets extracted:**
- Clip size, fire rate, damage, range
- Spread angle, reload time
- Automatic/semi-auto mode
- Hitscan vs projectile

### Step 2.2: Map Item Types (Editor Window)

**Open:** Tools → DIG → OPSIVE Item Mapper

**Workflow:**
1. Click "Create New Registry" → save as `OpsiveItemMappingRegistry.asset`
2. Click "Scan for OPSIVE Items" to find all `ItemDefinitionBase` assets
3. Review discovered items in the list
4. Click "Add All to Registry" to save mappings
5. Click "Generate ECS Item Definitions" to create `ItemTypeIds.cs`

**Output:** `Assets/Scripts/Items/Generated/ItemTypeIds.cs`
```csharp
public static class ItemTypeIds
{
    public const int AssaultRifle = -1234567890;
    public const int Pistol = 987654321;
    // ... etc
}
```

### Step 2.3: Convert Prefabs (Editor Window)

**Open:** Tools → DIG → OPSIVE Prefab Converter

**Workflow:**
1. Set "Output Folder" to `Assets/Prefabs/Items/Converted`
2. Drag OPSIVE weapon prefab to "Source OPSIVE Prefab"
3. Enable "Add Animation Relay" to auto-add the relay component
4. Click "Convert Prefab"

**What happens:**
- OPSIVE components are removed
- `WeaponAuthoring` is added with extracted stats
- Visual mesh/materials are preserved
- Output: `WeaponName_ECS.prefab`

---

## Phase 3: Item Set System Setup

### Step 3.1: Add WeaponSwitchAuthoring to Server Prefab

**Location:** `Warrok_Server` (the ghost prefab that gets baked)

**Setup:**
1. Select `Warrok_Server` prefab
2. Add Component → `WeaponSwitchAuthoring`
3. Configure settings:

**Inspector Settings:**
```
WeaponSwitchAuthoring
├── Switch Cooldown: 0.25 (seconds between switches)
├── Wrap Around: true (cycle last→first)
└── Auto Equip Default: true (equip on spawn)
```

**What this adds to the entity:**
- `ItemSwitchSettings` - configuration
- `WeaponSwitchInput` - input state (replicated)
- `ItemSwitchRequest` - pending switch request
- `ActiveItemSet` - current weapon tracking
- `LastEquippedItem` - for quick-swap (Q key)
- `DynamicBuffer<ItemSetEntry>` - weapon inventory

### Step 3.2: Populate ItemSetEntry Buffer

You need to populate the weapon buffer either:

**Option A: At spawn time (recommended)**
```csharp
// In your player spawn system
var itemSets = EntityManager.GetBuffer<ItemSetEntry>(playerEntity);
itemSets.Add(new ItemSetEntry
{
    SetName = "Primary",
    ItemEntity = rifleEntity,
    Order = 0,
    QuickSlot = 1,
    IsDefault = true
});
itemSets.Add(new ItemSetEntry
{
    SetName = "Primary",
    ItemEntity = shotgunEntity,
    Order = 1,
    QuickSlot = 2,
    IsDefault = false
});
itemSets.Add(new ItemSetEntry
{
    SetName = "Secondary",
    ItemEntity = pistolEntity,
    Order = 0,
    QuickSlot = 3,
    IsDefault = false
});
```

**Option B: Via authoring (static loadout)**
Create a custom authoring component that bakes the buffer.

### Step 3.3: Feed Input to WeaponSwitchInput

Your existing input system needs to populate `WeaponSwitchInput`:

```csharp
// In your PlayerInputSystem or similar
foreach (var (switchInput, entity) in
         SystemAPI.Query<RefRW<WeaponSwitchInput>>()
         .WithAll<PlayerInput, Simulate>())
{
    ref var input = ref switchInput.ValueRW;

    // Reset each frame
    input.ScrollDelta = 0;
    input.QuickSlotPressed = 0;
    input.SwitchToLastPressed = false;
    input.HolsterPressed = false;

    // Read from Unity Input or your input system
    input.ScrollDelta = UnityEngine.Input.mouseScrollDelta.y;

    // Number keys 1-9
    for (int i = 1; i <= 9; i++)
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0 + i))
        {
            input.QuickSlotPressed = i;
            break;
        }
    }

    if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
        input.SwitchToLastPressed = true;

    if (UnityEngine.Input.GetKeyDown(KeyCode.H))
        input.HolsterPressed = true;
}
```

---

## Phase 4: Effects & Animation Driver Setup

### Step 4.1: Add WeaponAnimatorBridge to Weapon Models

**Location:** Each weapon prefab's visual model (the child with the Animator)

**Example hierarchy:**
```
AssaultRifle_ECS
├── WeaponAuthoring (on root)
├── Model (has Animator)
│   ├── Animator Controller
│   └── WeaponAnimatorBridge  ◄── ADD HERE
├── MuzzleFlash_Point
└── ShellEject_Point
```

**Setup:**
1. Select the weapon's Model child (with Animator)
2. Add Component → `WeaponAnimatorBridge`
3. Configure parameter names to match your Animator Controller:

**Inspector Settings:**
```
WeaponAnimatorBridge
├── Animator: [auto-found]
│
├── Shootable Parameters
│   ├── Param Is Firing: "IsFiring"
│   ├── Param Is Reloading: "IsReloading"
│   ├── Param Reload Progress: "ReloadProgress"
│   ├── Param Fire Trigger: "Fire"
│   ├── Param Reload Trigger: "Reload"
│   ├── Param Ammo Count: "AmmoCount"
│   └── Param Is Empty: "IsEmpty"
│
├── Melee Parameters
│   ├── Param Is Attacking: "IsAttacking"
│   ├── Param Attack Trigger: "Attack"
│   └── Param Combo Index: "ComboIndex"
│
├── Common Parameters
│   ├── Param Is Equipped: "IsEquipped"
│   ├── Param Equip Trigger: "Equip"
│   └── Param Unequip Trigger: "Unequip"
│
└── Debug Logging: false
```

### Step 4.2: Add WeaponEffectConfig to Weapon Prefabs

Add this component to weapon prefabs that need muzzle flash/shell ejection:

```csharp
// In WeaponAuthoring.cs Baker, add:
AddComponent(entity, new WeaponEffectConfig
{
    MuzzleFlashPrefabIndex = 0,    // Index into your effect registry
    ShellEjectPrefabIndex = 1,
    TracerPrefabIndex = 2,
    TracerProbability = 0.3f,      // 30% of shots show tracer
    MuzzleOffset = new float3(0, 0, 0.5f),
    ShellEjectOffset = new float3(0.1f, 0.1f, 0),
    ShellEjectDirection = new float3(1, 1, 0),
    ShellEjectSpeed = 3f
});
```

### Step 4.3: Create Effect Prefab Registry

The effect systems reference prefabs by index. Create a registry:

```csharp
// EffectPrefabRegistry.cs
public class EffectPrefabRegistry : MonoBehaviour
{
    public static EffectPrefabRegistry Instance;

    [Header("Muzzle Flashes")]
    public GameObject[] MuzzleFlashPrefabs;

    [Header("Shell Casings")]
    public GameObject[] ShellCasingPrefabs;

    [Header("Tracers")]
    public GameObject[] TracerPrefabs;

    [Header("Impact Effects")]
    public GameObject[] ImpactPrefabs;

    void Awake() => Instance = this;
}
```

Add this to a persistent GameObject in your scene.

### Step 4.4: Drive WeaponAnimatorBridge from ECS

Create a presentation system that reads ECS state and updates bridges:

```csharp
// WeaponAnimatorBridgeSystem.cs (presentation layer, client only)
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class WeaponAnimatorBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // For each weapon entity with visual representation
        foreach (var (fireState, ammoState, meleeState, entity) in
                 SystemAPI.Query<
                     RefRO<WeaponFireState>,
                     RefRO<WeaponAmmoState>,
                     RefRO<MeleeState>>()
                 .WithEntityAccess())
        {
            // Find the MonoBehaviour bridge (you need a lookup mechanism)
            var bridge = WeaponBridgeLookup.Get(entity);
            if (bridge == null) continue;

            bridge.ApplyWeaponState(new WeaponAnimationState
            {
                IsFiring = fireState.ValueRO.IsFiring,
                IsReloading = ammoState.ValueRO.IsReloading,
                ReloadProgress = ammoState.ValueRO.ReloadProgress,
                AmmoCount = ammoState.ValueRO.AmmoCount,
                IsAttacking = meleeState.ValueRO.IsAttacking,
                ComboIndex = meleeState.ValueRO.CurrentCombo,
                IsEquipped = true // Based on CharacterItem.State
            });
        }
    }
}
```

---

## Phase 5: Scene Conversion

### Step 5.1: Convert OPSIVE Demo Scene

**Open:** Tools → DIG → OPSIVE Scene Converter

**Workflow:**
1. Click "Browse" and select an OPSIVE demo scene:
   - `OPSIVE/com.opsive.ultimatecharactercontroller/Samples~/Demo/Demo.unity`
   - `OPSIVE/untitled folder/.../Agility/Demo/Demo.unity`
2. Set "Output Folder" to `Assets/Scenes/Test`
3. Enable options:
   - ✓ Preserve Lighting
   - ✓ Preserve NavMesh
   - ✓ Add Default Spawn Point
   - ✓ Convert Trigger Zones
4. Click "Convert Scene"

**What gets removed:**
- OPSIVE characters (UltimateCharacterLocomotion)
- OPSIVE managers (SpawnManager, ObjectPool, etc.)
- OPSIVE cameras (CameraController)
- OPSIVE UI (ItemMonitor, HealthFlash)

**What gets added:**
- `DIG_PlayerSpawn` object with `PlayerSpawnMarker`
- `StaticGeometry_Subscene` marker
- `TriggerZoneMarker` on all trigger colliders

### Step 5.2: Post-Conversion Setup

After conversion, the scene needs manual setup:

1. **Create Subscene:**
   - Select static geometry
   - Right-click → New Sub Scene → From Selection

2. **Add Player Spawner:**
   - Create your DIG player spawner prefab
   - Position at `DIG_PlayerSpawn` location

3. **Configure Trigger Zones:**
   - Find objects with `TriggerZoneMarker`
   - Replace with your ECS trigger authoring

4. **Add Item Pickups:**
   - Place your weapon pickup prefabs
   - Configure `ItemPickupAuthoring`

---

## Quick Reference: Component Placement

| Component | GameObject Location | Prefab Type |
|-----------|---------------------|-------------|
| `WeaponAnimationEventRelay` | Character Model (with Animator) | Client |
| `WeaponSwitchAuthoring` | Player Root | Server (Ghost) |
| `WeaponAuthoring` | Weapon Prefab Root | Baked |
| `WeaponAnimatorBridge` | Weapon Model (with Animator) | Client Visual |
| `ClimbAnimatorBridge` | Character Model | Client |
| `AnimationEventRelay` | Character Model | Client |

---

## Troubleshooting

### Animation events not firing
- Check `WeaponAnimationEventRelay` is on the **same GameObject** as the Animator
- Enable debug logging and check Console
- Verify animation clips have events calling `ExecuteEvent`

### Weapon switching not working
- Verify `WeaponSwitchAuthoring` is on the **Server** prefab (not client)
- Check `ItemSetEntry` buffer is populated
- Ensure `WeaponSwitchInput` is being written by input system

### Reload animation not playing
- Check `WeaponAnimatorBridge` parameter names match Animator Controller
- Verify Animator has parameters: `IsReloading`, `ReloadProgress`, `Reload` (trigger)
- Ensure `WeaponAmmoState.IsReloading` is being set by `WeaponAnimationEventSystem`

### Effects not spawning
- Verify `WeaponEffectConfig` is on weapon entity
- Check `FireEffectRequest.Pending` is being set
- Ensure effect prefab registry is configured

---

## File Locations Summary

```
Assets/
├── Scripts/
│   ├── Weapons/
│   │   ├── Animation/
│   │   │   ├── WeaponAnimationEvents.cs      # Static event queue
│   │   │   ├── WeaponAnimationEventRelay.cs  # Animation → ECS
│   │   │   └── WeaponAnimatorBridge.cs       # ECS → Animator
│   │   ├── Systems/
│   │   │   └── WeaponAnimationEventSystem.cs # Processes queue
│   │   ├── Effects/
│   │   │   ├── WeaponEffectComponents.cs
│   │   │   ├── FireEffectSpawnerSystem.cs
│   │   │   └── ImpactEffectSpawnerSystem.cs
│   │   └── Components/
│   │       └── WeaponActionComponents.cs     # Already existed
│   └── Items/
│       ├── Components/
│       │   └── ItemSetComponents.cs          # ItemSetEntry, etc.
│       └── Systems/
│           ├── ItemSetSwitchSystem.cs        # Weapon cycling
│           └── ItemSwitchInputSystem.cs      # Input + authoring
├── Editor/
│   └── OpsiveWeaponExtractor/
│       ├── OpsiveWeaponExtractorWindow.cs
│       ├── OpsiveItemMapper.cs
│       ├── OpsivePrefabConverter.cs
│       └── OpsiveSceneConverter.cs
```
