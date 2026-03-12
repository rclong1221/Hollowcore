### Epic 7.13: Collision Response Profiles & Customization
**Priority**: MEDIUM
**Goal**: Allow designers to create custom collision behaviors for different game modes, scenarios, and player abilities without programmer assistance

**IMPORTANT: Customization Philosophy**
Collision "feel" is a key part of game identity:
- ✅ **Realistic**: Heavy, impactful collisions (tactical shooters)
- ✅ **Arcade**: Bouncy, forgiving collisions (party games)
- ✅ **Horror**: Oppressive, sticky collisions (survival horror)
- ✅ **Competitive**: Predictable, fair collisions (esports)

**Designers should be able to**:
- Adjust collision parameters without code changes
- Create and save presets for different modes
- Preview collision feel instantly
- A/B test different configurations

**Sub-Epic 7.13.1: Collision Profile System** *(Not Started)*
**Goal**: Create ScriptableObject-based collision profiles for data-driven configuration
**Design Notes**:
- Profiles define all collision parameters in one asset
- Can be swapped at runtime (game mode change, power-ups)
- Support inheritance (base profile + overrides)

**CollisionProfile ScriptableObject Structure**:
```csharp
[CreateAssetMenu(menuName = "DIG/Collision/Profile")]
public class CollisionProfile : ScriptableObject
{
    [Header("Push Forces")]
    public float PushForceMultiplier = 1.0f;
    public float MaxPushSpeed = 5.0f;
    
    [Header("Stagger")]
    public float StaggerPowerThreshold = 5.0f;
    public float StaggerDuration = 0.5f;
    public AnimationCurve StaggerRecoveryCurve;
    
    [Header("Knockdown")]
    public float KnockdownPowerThreshold = 0.9f;
    public float KnockdownDuration = 2.0f;
    public float KnockdownInvulnerabilityDuration = 0.5f;
    
    [Header("Cooldowns")]
    public float CollisionCooldown = 0.3f;
    public float TackleCooldown = 1.0f;
    
    [Header("Stance Multipliers")]
    public float StandingMultiplier = 1.0f;
    public float CrouchingMultiplier = 0.5f;
    public float ProneMultiplier = 0.3f;
    public float SprintingMultiplier = 1.5f;
    
    [Header("Directional Bonuses")]
    public float BracedFrontBonus = 0.8f;
    public float SideHitMultiplier = 1.0f;
    public float BackHitPenalty = 1.5f;
    
    [Header("Audio/VFX")]
    public float MinImpactSpeedForSound = 2.0f;
    public float MinImpactSpeedForVFX = 3.0f;
}
```

**Tasks**:
- [ ] **Create CollisionProfile.cs ScriptableObject**:
  - [ ] All tunable parameters from PlayerCollisionSettings
  - [ ] Validation (min/max ranges, warnings)
  - [ ] Custom inspector with grouping and help text
- [ ] **Create profile loading system**:
  - [ ] Load profile on player spawn
  - [ ] Cache profile reference per player entity
  - [ ] Support runtime profile switching
- [ ] **Create default profiles**:
  - [ ] `Default.asset` - Balanced baseline
  - [ ] `Realistic.asset` - Heavy, impactful
  - [ ] `Arcade.asset` - Light, bouncy
  - [ ] `Tactical.asset` - Punishing, strategic
  - [ ] `Testing.asset` - Extreme values for debugging
- [ ] **Add profile inheritance**:
  - [ ] Base profile + override profile
  - [ ] Only override specified values
  - [ ] Chain multiple overrides (base → mode → ability)
- [ ] **Create profile preview tool**:
  - [ ] In-editor preview of collision behavior
  - [ ] Side-by-side comparison of two profiles
  - [ ] Simulated collision with visual feedback
- [ ] **Add profile validation**:
  - [ ] Warn if values out of recommended range
  - [ ] Error if values would break physics
  - [ ] Suggest fixes for common issues

**Sub-Epic 7.13.2: Game Mode Collision Variants** *(Not Started)*
**Goal**: Configure collision behavior per game mode
**Design Notes**:
- Different modes have different collision expectations
- Mode defines profile, team settings, and special rules
- Seamless transition when mode changes

**Game Mode Configurations**:
| Mode | Profile | Team Collision | Special Rules |
|------|---------|----------------|---------------|
| PvP Deathmatch | Realistic | On | High stagger |
| Team PvP | Realistic | Off (same team) | Team immunity |
| Co-op PvE | Arcade | Off | Friendly, reduced push |
| Racing | Arcade | On | Bouncy, no knockdown |
| Horror | Tactical | Situational | Grabbing, slow recovery |
| Stealth | Minimal | Off | Near-silent, low force |
| Party | Extreme | On | Maximum chaos |

**Tasks**:
- [ ] **Create GameModeCollisionConfig.cs**:
  - [ ] Reference to CollisionProfile
  - [ ] Team collision toggle
  - [ ] Friendly fire settings
  - [ ] Special rule flags
- [ ] **Implement mode-based profile switching**:
  - [ ] Listen for game mode change events
  - [ ] Apply new profile to all players
  - [ ] Smooth transition (fade between profiles)
- [ ] **Add team collision filtering**:
  - [ ] Skip collision response for same-team players
  - [ ] Still apply minimal separation (no clipping)
  - [ ] Configurable per mode
- [ ] **Create mode-specific presets**:
  - [ ] `PvPDeathmatch.asset`
  - [ ] `TeamPvP.asset`
  - [ ] `CoopPvE.asset`
  - [ ] `Racing.asset`
  - [ ] `Horror.asset`
- [ ] **Add mode transition effects**:
  - [ ] Visual indicator when profile changes
  - [ ] Audio cue for collision rule change
  - [ ] Smooth parameter interpolation (0.5s)

**Sub-Epic 7.13.3: Dynamic Collision Scaling** *(Not Started)*
**Goal**: Adjust collision intensity based on game state (combat, exploration, cutscene)
**Design Notes**:
- Collision feel should match gameplay context
- Automatic scaling based on detected state
- Manual override for scripted sequences

**Game States and Collision Intensity**:
| State | Intensity | Behavior |
|-------|-----------|----------|
| Combat | High (1.5x) | Aggressive collisions, full stagger |
| Exploration | Normal (1.0x) | Standard behavior |
| Social/Hub | Low (0.5x) | Gentle pushes, no stagger |
| Cutscene | Disabled (0x) | No collision response |
| Scripted | Custom | Designer-defined per sequence |

**Tasks**:
- [ ] **Create CollisionIntensityManager.cs**:
  - [ ] Track current game state
  - [ ] Apply intensity multiplier to all collision forces
  - [ ] Support smooth transitions between intensities
- [ ] **Implement automatic state detection**:
  - [ ] Combat: player or nearby enemy has weapon drawn
  - [ ] Exploration: no enemies nearby, no combat recently
  - [ ] Social: in designated safe zone
  - [ ] Cutscene: cutscene system active
- [ ] **Add intensity override API**:
  - [ ] `SetCollisionIntensity(float intensity, float duration)`
  - [ ] `DisableCollision()` / `EnableCollision()`
  - [ ] Scripting support for designers
- [ ] **Create intensity transition effects**:
  - [ ] Smooth interpolation between intensities
  - [ ] Configurable transition duration (default 0.5s)
  - [ ] Optional audio/visual cue
- [ ] **Add designer UI for scripted sequences**:
  - [ ] Timeline integration for intensity keyframes
  - [ ] Preview in editor
  - [ ] Export/import intensity curves

**Sub-Epic 7.13.4: Special Collision Behaviors** *(Not Started)*
**Goal**: Implement modifiers for special player states and abilities
**Design Notes**:
- Abilities can modify how collision affects a player
- Effects can stack (multiple modifiers active)
- Some effects are mutually exclusive

**Special Collision Modifiers**:
| Modifier | Effect | Use Case |
|----------|--------|----------|
| Invulnerable | Ghost through players | Power-up, respawn immunity |
| KnockbackImmune | No push forces received | Bracing ability, heavy armor |
| SuperKnockback | 2x push force applied | Charge attack, bull rush |
| Intangible | Pass through players, not environment | Phase shift ability |
| Magnetic | Pull nearby players toward self | Gravity well ability |
| Repulsor | Push nearby players away constantly | Shield aura |
| Sticky | Slow players who collide | Tar trap, web |
| Bouncy | High restitution collisions | Power-up, joke mode |

**Tasks**:
- [ ] **Create CollisionModifier component**:
  - [ ] Enum of modifier types
  - [ ] Duration and intensity
  - [ ] Stack behavior (replace, stack, ignore)
- [ ] **Implement Invulnerable modifier**:
  - [ ] Skip all collision response
  - [ ] Visual effect (ghost/transparent)
  - [ ] Duration-based auto-removal
- [ ] **Implement KnockbackImmune modifier**:
  - [ ] Receive zero push force
  - [ ] Still apply force to others (asymmetric)
  - [ ] Visual indicator (anchor icon)
- [ ] **Implement SuperKnockback modifier**:
  - [ ] Multiply outgoing push force
  - [ ] Optional self-immunity
  - [ ] VFX on collision (impact sparks)
- [ ] **Implement Intangible modifier**:
  - [ ] Disable player-player collision layer
  - [ ] Keep environment collision active
  - [ ] Phase-shift visual effect
- [ ] **Implement Magnetic/Repulsor modifiers**:
  - [ ] Constant force applied in range
  - [ ] Falloff with distance
  - [ ] Performance: spatial query for affected players
- [ ] **Implement Sticky modifier**:
  - [ ] Apply velocity reduction on contact
  - [ ] Duration of slow effect
  - [ ] Visual (goo particles) and audio (squelch)
- [ ] **Add collision modifier stacking**:
  - [ ] Define priority for conflicting modifiers
  - [ ] Stack same-type modifiers (intensity adds)
  - [ ] Mutex groups (invulnerable vs sticky)
- [ ] **Create modifier event callbacks**:
  - [ ] `OnModifierApplied(CollisionModifier modifier)`
  - [ ] `OnModifierRemoved(CollisionModifier modifier)`
  - [ ] `OnModifiedCollision(CollisionEvent, CollisionModifier)`

**Sub-Epic 7.13.5: Per-Player Collision Customization** *(Not Started)*
**Goal**: Allow individual player collision properties (weight classes, abilities)
**Design Notes**:
- Different player characters may have different collision properties
- Weight classes affect push force given/received
- Character abilities can modify collision behavior

**Weight Classes**:
| Class | Mass Multiplier | Push Given | Push Received |
|-------|----------------|------------|---------------|
| Light | 0.7x | 0.7x | 1.3x |
| Normal | 1.0x | 1.0x | 1.0x |
| Heavy | 1.5x | 1.3x | 0.7x |
| Super Heavy | 2.0x | 1.5x | 0.5x |

**Tasks**:
- [ ] **Create PlayerCollisionProfile component**:
  - [ ] Per-player override of base profile
  - [ ] Weight class selection
  - [ ] Ability modifier references
- [ ] **Implement weight class system**:
  - [ ] Light, Normal, Heavy, Super Heavy
  - [ ] Affects mass calculation in power formula
  - [ ] Visual indicator (size, effects)
- [ ] **Add character-specific collision abilities**:
  - [ ] Tank: KnockbackImmune while blocking
  - [ ] Speedster: SuperKnockback while sprinting
  - [ ] Ghost: Intangible on ability activation
  - [ ] Juggernaut: Charge tackle with extended stagger
- [ ] **Create ability integration hooks**:
  - [ ] Ability system can apply collision modifiers
  - [ ] Modifiers auto-remove on ability end
  - [ ] Support ability combos affecting collision
- [ ] **Add loadout-based collision**:
  - [ ] Heavy armor increases weight class
  - [ ] Sprint boots increase speed collision bonus
  - [ ] Shield item grants frontal immunity

**Sub-Epic 7.13.6: Collision Callbacks & Scripting** *(Not Started)*
**Goal**: Allow game code to hook into and override collision behavior
**Design Notes**:
- Events for game systems to respond to collisions
- Ability to cancel, modify, or enhance collisions
- Support for designer-friendly visual scripting

**Callback Points**:
| Event | Timing | Can Cancel | Can Modify |
|-------|--------|------------|------------|
| OnCollisionPending | Before response | ✅ Yes | ✅ Yes |
| OnCollisionApplied | After response | ❌ No | ❌ No |
| OnStaggerStart | Stagger begins | ❌ No | ✅ Duration |
| OnKnockdownStart | Knockdown begins | ❌ No | ✅ Duration |
| OnRecoveryComplete | State restored | ❌ No | ❌ No |

**Tasks**:
- [ ] **Create collision event system**:
  - [ ] C# events for each callback point
  - [ ] Priority ordering for handlers
  - [ ] Support for cancellation (pending only)
- [ ] **Implement OnCollisionPending**:
  - [ ] Called before any response applied
  - [ ] Handler can cancel collision
  - [ ] Handler can modify collision data
- [ ] **Implement OnCollisionApplied**:
  - [ ] Called after response complete
  - [ ] Use for achievements, statistics
  - [ ] Use for sound/VFX triggers
- [ ] **Implement state change callbacks**:
  - [ ] OnStaggerStart, OnStaggerEnd
  - [ ] OnKnockdownStart, OnKnockdownEnd
  - [ ] OnRecoveryComplete
- [ ] **Add scripting API**:
  - [ ] `CollisionManager.RegisterHandler(callback, priority)`
  - [ ] `CollisionManager.UnregisterHandler(callback)`
  - [ ] `CollisionEvent.Cancel()`, `CollisionEvent.ModifyForce()`
- [ ] **Create visual scripting nodes** (if using visual scripting):
  - [ ] Collision Event node (trigger)
  - [ ] Collision Modifier node (apply effect)
  - [ ] Collision Check node (query)

**Files to Create**:
- `Assets/Scripts/Player/Profiles/CollisionProfile.cs`
- `Assets/Scripts/Player/Profiles/GameModeCollisionConfig.cs`
- `Assets/Scripts/Player/Components/CollisionModifier.cs`
- `Assets/Scripts/Player/Components/PlayerCollisionProfile.cs`
- `Assets/Scripts/Player/Systems/CollisionIntensityManager.cs`
- `Assets/Scripts/Player/Events/CollisionEventCallbacks.cs`
- `Assets/Resources/CollisionProfiles/Default.asset`
- `Assets/Resources/CollisionProfiles/Realistic.asset`
- `Assets/Resources/CollisionProfiles/Arcade.asset`
- `Assets/Resources/CollisionProfiles/Tactical.asset`
- `Assets/Resources/GameModes/PvPDeathmatch.asset`
- `Assets/Resources/GameModes/CoopPvE.asset`

**Files to Modify**:
- `Assets/Scripts/Player/Components/PlayerCollisionSettings.cs` (load from profile)
- `Assets/Scripts/Player/Systems/PlayerCollisionResponseSystem.cs` (apply modifiers)
- `Assets/Scripts/Player/Systems/PlayerProximityCollisionSystem.cs` (check modifiers)