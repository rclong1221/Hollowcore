# SETUP GUIDE - EPIC 13.16 Health & Damage Parity

This guide outlines the steps required to configure the new Health, Damage, and Hitbox systems on your Player characters and test environments.

## 1. Hitbox Configuration (Crucial for 13.16.1)

To enable Hitbox multipliers (Headshots 2x, Limbs 0.5x, etc.), you must configure the Player Prefab.

### Step 1: Add Owner Marker
1. Select your **Player Prefab** (e.g., `Warrok_Client.prefab` / `Warrok_Server.prefab` or the Authoring prefab).
2. Add the `HitboxOwnerMarker` component to the **Root** GameObject (the one with `PlayerAuthoring`).
   - This ensures all damage received via hitboxes is routed to this entity.

### Step 2: Add Hitbox Components
1. Locate the child GameObjects that contain your **Colliders** (e.g., `HeadCollider`, `TorsoCollider`, `LegCollider`).
2. Add the `HitboxAuthoring` component to each of these objects.
3. Configure the settings for each:
   - **Head**: 
     - Multiplier: `2.0`
     - Region: `Head`
   - **Torso**: 
     - Multiplier: `1.0`
     - Region: `Torso`
   - **limbs (Legs/Arms)**: 
     - Multiplier: `0.6`
     - Region: `Legs` or `Arms`

### Step 3: Verify Layers
Ensure your child colliders are on a layer that Projectiles collide with (e.g., `Default`, `Character`, or `Hitbox`). The `ProjectileSystem` uses standard raycasts.

## 2. Shield Configuration (13.16.3)

Shields are automatically added via `PlayerAuthoring`. Default values are:
- Max Shield: 50
- Regen Rate: 5/sec
- Regen Delay: 5 sec

To modify these:
1. Open `Assets/Scripts/Player/Components/ShieldComponent.cs`.
2. Edit the `Default` static property values.
   - *Future work: Migrate these to `PlayerStanceConfig` or a dedicated `HealthConfig` Authoring component for Inspector editing.*

## 3. Kill Attribution (13.16.12)

Kill attribution (Kills/Assists) is automatic.
- **Kills**: Awarded to the last entity that dealt damage before death.
- **Assists**: Awarded to any other entity that dealt damage within 15 seconds of death.
- Events (`KillCredited`, `AssistCredited`) are spawned on the **Killer/Assister** entity.

## 4. Event Systems (13.16.9 - 13.16.11)

These are internal systems enabled automatically.
- **Heal Events**: Use `HealEvent` buffer instead of `HealRequest`.
- **Health Changed**: `HealthChangedEvent` component toggles on change.
- **Death Events**: `WillDieEvent` (Cancellable) and `DiedEvent` fire on the victim.

## 5. Testing

### Run the Scene
1. Open `Subscene` or your test scene.
2. Enter Play Mode.
3. **Verify Hitboxes**: Shoot the player's head vs legs. Verify damage numbers in the Inspector (`Health.Current`).
4. **Verify Shields**: Damage the player. Watch `ShieldComponent.Current` drop before `Health`. Watch it regen after 5s.

## 6. Test Environment Setup (Tasks T4-T8)

I have implemented special components to assist with testing.

### Heal Station (T6)
1. Create a new Cube/GameObject.
2. Add `HealStationAuthoring` component.
   - Heal Amount: `10`
   - Radius: `5`
3. Place near player. The player should heal automatically when injured.

### God Mode (T7)
1. Add `GodModeAuthoring` to the player Prefab (or a specific test enemy).
2. Check `Enabled`.
3. Damage entity to 0 HP. Observe `WillDieEvent.Cancelled` logic preventing death state.

### Death Spawns / Loot (T4/T7)
1. Add `DeathSpawnAuthoring` to a character.
2. Assign a Prefab (e.g., a Sphere or "LootCrate") to `PrefabsToSpawn`.
3. Kill the character. The prefab will spawn at their position.

### Kill Feed & Damage Logs (T5/T8)
- These debug systems are **Enabled by Default** in Server Simulation.
- Watch the **Unity Console** for:
  - `[DAMAGE]` logs
  - `[KILL FEED]` logs
  - `[ASSIST]` logs

