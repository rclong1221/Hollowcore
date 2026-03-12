# EPIC 22.7: Configuration ScriptableObjects

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 3-4 days  
**Dependencies**: 22.1 (Assembly Definitions)

---

## Goal

Create designer-friendly ScriptableObject configurations for all player settings.

---

## Current Problem

Many settings are hard-coded in systems or spread across components. Designers can't easily tune the character without code changes.

---

## Target Configurations

### 1. MovementConfig
```csharp
[CreateAssetMenu(menuName = "DOTS Character/Movement Config")]
public class MovementConfig : ScriptableObject
{
    [Header("Walking")]
    public float WalkSpeed = 3f;
    public float WalkAcceleration = 10f;
    
    [Header("Running")]
    public float RunSpeed = 6f;
    public float SprintSpeed = 9f;
    
    [Header("Jumping")]
    public float JumpForce = 5f;
    public float AirControl = 0.3f;
    public int MaxJumps = 1;
    
    [Header("Crouching")]
    public float CrouchSpeed = 2f;
    public float CrouchHeight = 1.0f;
    
    [Header("Physics")]
    public float Gravity = -20f;
    public float GroundCheckDistance = 0.1f;
}
```

### 2. ClimbingConfig
```csharp
[CreateAssetMenu(menuName = "DOTS Character/Climbing Config")]
public class ClimbingConfig : ScriptableObject
{
    public float ClimbSpeed = 2f;
    public float MountDistance = 2f;
    public float DismountForce = 3f;
    public bool RequireStamina = true;
    public float StaminaDrainRate = 5f;
}
```

### 3. CombatConfig
```csharp
[CreateAssetMenu(menuName = "DOTS Character/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Header("Health")]
    public float MaxHealth = 100f;
    public float HealthRegen = 0f;
    
    [Header("Damage")]
    public float InvulnerabilityTime = 0.5f;
    public float KnockbackForce = 5f;
    
    [Header("Ragdoll")]
    public float RagdollSettleTime = 2f;
    public float RespawnDelay = 3f;
}
```

### 4. CameraConfig
```csharp
[CreateAssetMenu(menuName = "DOTS Character/Camera Config")]
public class CameraConfig : ScriptableObject
{
    public float Sensitivity = 2f;
    public float MinPitch = -80f;
    public float MaxPitch = 80f;
    public float SmoothTime = 0.1f;
    public float FOV = 60f;
}
```

---

## Tasks

### Phase 1: Core Configs
- [ ] Create MovementConfig
- [ ] Create CameraConfig
- [ ] Create PhysicsConfig (capsule, layers)
- [ ] Create default assets

### Phase 2: Extended Configs
- [ ] Create ClimbingConfig
- [ ] Create MantleConfig
- [ ] Create SlideConfig
- [ ] Create ProneConfig
- [ ] Create DodgeConfig

### Phase 3: Combat Configs
- [ ] Create HealthConfig
- [ ] Create DamageConfig
- [ ] Create RagdollConfig
- [ ] Create TackleConfig

### Phase 4: System Integration
- [ ] Update systems to read from configs
- [ ] Create config blob assets for Burst
- [ ] Add config references to authoring

### Phase 5: Editor Enhancement
- [ ] Custom inspectors with sliders
- [ ] Preset buttons (Realistic, Arcade)
- [ ] Live preview in play mode

---

## Blob Asset Pattern

For Burst-compiled systems:

```csharp
public struct MovementConfigBlob
{
    public float WalkSpeed;
    public float RunSpeed;
    public float JumpForce;
    // ... all fields
}

// Baked from ScriptableObject at conversion time
```

---

## Success Criteria

- [ ] All settings in ScriptableObjects
- [ ] No hard-coded values in systems
- [ ] Designers can tune without code
- [ ] Presets for common configurations
- [ ] Custom inspectors for each config
