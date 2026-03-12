# EPIC 14.11 - Generic Footstep Trigger System

**Goal:** Create a generic, physics-based footstep trigger system for DIG that hooks into the existing ECS audio pipeline. This allows for accurate footstep timing based on physical contact (animation-driven) rather than velocity timers.

## 1. Architecture Overview

We will replicate the Opsive `FootstepTrigger` pattern but adapt it to feed into our ECS `FootstepEvent` system.

### Components

1.  **`DIGFootstepTrigger` (MonoBehaviour)**
    *   **Placement:** Attached to the Foot/Toe GameObjects of the character rig (e.g., `ORG-toes.L`, `ORG-toes.R`).
    *   **Role:** Detects physical collision with the ground via `OnTriggerEnter`.
    *   **Logic:** Forwards the collision event to the parent `DIGCharacterFootEffects` handler.

2.  **`DIGCharacterFootEffects` (MonoBehaviour)**
    *   **Placement:** Attached to the Character Root (same level as `DIGEquipmentProvider`).
    *   **Role:** Acts as the Bridge between physical triggers and the ECS world.
    *   **Logic:**
        *   Receives callback from `DIGFootstepTrigger`.
        *   Resolves Surface Material using `SurfaceDetectionService`.
        *   Creates an ECS `FootstepEvent` component.
        *   Adds the event to the Player Entity via `EntityManager`.

3.  **Existing ECS Systems (Unchanged)**
    *   `FootstepSystem`: We will skip its velocity-based logic (via `AudioSettings.UseAnimatorForFootsteps` flag).
    *   `AudioPlaybackSystem`: Consumes the `FootstepEvent` produced by our bridge and plays the sound.

---

## 2. Implementation Plan

### Step 1: `DIGFootstepTrigger.cs`

A lightweight script to sit on the feet.

```csharp
public class DIGFootstepTrigger : MonoBehaviour 
{
    private DIGCharacterFootEffects _effects;
    
    // Auto-find parent handler
    void Awake() => _effects = GetComponentInParent<DIGCharacterFootEffects>();

    void OnTriggerEnter(Collider other)
    {
        if (_effects != null) _effects.OnFootstepHit(other, transform.position);
    }
}
```

### Step 2: `DIGCharacterFootEffects.cs`

The logic hub.

```csharp
public class DIGCharacterFootEffects : MonoBehaviour
{
    [Tooltip("Layers to consider as ground")]
    public LayerMask GroundLayers = -1;
    
    // Reference to provider to get ECS Entity
    private DIGEquipmentProvider _provider;

    void Awake() => _provider = GetComponent<DIGEquipmentProvider>();

    public void OnFootstepHit(Collider groundCollider, Vector3 position)
    {
        // 1. Check Layer
        if (((1 << groundCollider.gameObject.layer) & GroundLayers) == 0) return;

        // 2. Resolve Material (using existing service)
        int matID = SurfaceDetectionService.ResolveMaterialId(default, Entity.Null, groundCollider.gameObject);

        // 3. Send to ECS
        if (_provider != null && _provider.PlayerEntity != Entity.Null) 
        {
            var em = _provider.EntityWorld.EntityManager;
            
            // Add FootstepEvent directly to the player entity
            // (The system consumes and removes it)
            em.AddComponentData(_provider.PlayerEntity, new FootstepEvent {
                Position = position,
                MaterialId = matID,
                Stance = 0, // Todo: Get stance from provider/animator if needed
                Intensity = 1.0f
            });
        }
    }
}
```

## 3. Setup Instructions (For User)

1.  **Add Components:**
    *   Add `DIGCharacterFootEffects` to the **Character Root** (e.g., `Atlas_Client`).
    *   Add `DIGFootstepTrigger` to **Left Toe** and **Right Toe** bones.
    *   Ensure Toe bones have a **Collider** (Sphere/Box) set to `IsTrigger = true`.
    *   Add a **Rigidbody** (Kinematic) to the Toes if `OnTriggerEnter` doesn't fire (Unity requirement: one object needs RB). *Correction: The Character Controller usually satisfies the RB requirement, or the ground has static colliders. If not firing, add Kinematic RB to toes.*

2.  **Configuration:**
    *   Set `GroundLayers` on `DIGCharacterFootEffects` (typically `Default`, `Ground`, `Terrain`).
