# EPIC 14.29: Compiler Warning Cleanup

## Goal
Analyze all CS0162 (unreachable code), CS0219 (unused variable), CS0414 (unused field), CS0168 (unused exception), and CS0618 (obsolete API) warnings and resolve them appropriately.

---

## Warning Categories

| Category | Count | Description |
|----------|-------|-------------|
| CS0162 | 27 | Unreachable code (debug logging behind `const bool = false`) |
| CS0414 | 13 | Field assigned but never used |
| CS0219 | 4 | Variable assigned but never used |
| CS0618 | 5 | Obsolete API usage |
| CS0168 | 1 | Exception declared but never used |

---

## 1. CS0162: Unreachable Code Detected

These warnings are caused by debug logging statements that are wrapped in `if (DebugEnabled)` blocks where `DebugEnabled` is a `const bool = false`. The compiler correctly identifies that the code inside these blocks will never execute.

### Pattern Analysis
All of these follow the same pattern:
```csharp
private const bool DebugEnabled = false;
// ...
if (DebugEnabled)
{
    Debug.Log(...);  // <-- CS0162: Unreachable code
}
```

### Recommendation: **KEEP AS-IS (Suppress Warning)**

**Rationale:**
- This is an intentional debug toggle pattern
- The dead code elimination by the compiler is actually desired (zero runtime cost)
- The pattern allows quick re-enabling of debug logs during development
- Alternative approaches (`#if DEBUG`, `[Conditional]`) have different trade-offs

**Action:** Add a pragma to suppress CS0162 in these files OR convert to `#pragma warning disable 0162` at the file level.

### Files Affected

| File | Lines | Notes |
|------|-------|-------|
| StartingInventoryAuthoring.cs | 57, 84, 99 | Baker debug logging |
| ItemSwitchInputSystem.cs | 69, 177 | Input bridge debug logging |
| ItemSpawnSystem.cs | 84, 90, 137, 177 | Weapon spawn debug logging |
| WeaponDebugSystem.cs | 77 | Entire system is a debug tool |
| ItemSetSwitchSystem.cs | 77, 94, 100, 116, 122, 153 | Switch request debug logging |
| ItemEquipSystem.cs | 38, 96, 196 | Equip state debug logging |
| InventoryBindingSystem.cs | 178, 214 | Binding debug logging |
| ShipMovementSystem.cs | 42, 78, 86, 130, 186 | Ship input debug logging |
| CollisionRelevancySystem.cs | 118 | Network stats debug logging |

---

## 2. CS0219: Variable Assigned But Never Used

### 2.1 DamageApplySystem.cs:80 - `totalShieldDamage`

```csharp
float totalHealthDamage = 0f;
float totalShieldDamage = 0f;  // <-- Assigned but never used
```

**Analysis:** This appears to be a **stub for future shield damage implementation**.

**Recommendation:** **IMPLEMENT or REMOVE**
- If shield damage is planned (EPIC for shields), keep and add `// TODO: EPIC XX.XX - Shield damage`
- If not planned, remove the variable

**Action:** Check if shield system exists. If yes, wire up. If no, remove.

---

### 2.2 DamageDebugSystem.cs:100 - `hasSurvivalDamage`

```csharp
bool hasSurvivalDamage = false;
float survivalPending = 0f;
if (EntityManager.HasComponent<SurvivalDamageEvent>(entity))
{
    hasSurvivalDamage = true;  // <-- Set but never read
    var survival = EntityManager.GetComponentData<SurvivalDamageEvent>(entity);
    survivalPending = survival.PendingDamage;
}
```

**Analysis:** The variable is set but only `survivalPending` is used. The boolean was likely intended for debug output.

**Recommendation:** **USE in Debug Output or REMOVE**
- Add to the debug log string: `hasSurvival={hasSurvivalDamage}`
- Or remove the boolean and just check `survivalPending > 0`

---

### 2.3 DamageDebugSystem.cs:135 - `hasTriggerBuffer`

```csharp
// bool hasTriggerBuffer = EntityManager.HasBuffer<StatefulTriggerEvent>(entity);
bool hasTriggerBuffer = false; // logic changed  // <-- Never used
```

**Analysis:** This was intentionally commented out and replaced with `false`. The comment "logic changed" indicates this was disabled during refactoring.

**Recommendation:** **REMOVE**
- The commented-out code and the `false` assignment are dead code
- Remove both lines entirely

---

### 2.4 MaterialCleanerTool.cs:90 - `hasNull`

```csharp
bool hasNull = false;  // <-- Assigned but never used
// ...
for (int i = 0; i < mats.Length; i++)
{
    if (mats[i] == null)
    {
        // hasNull would be set here but isn't used
```

**Analysis:** The variable was intended to track if any null materials were found, likely for logging or reporting.

**Recommendation:** **USE or REMOVE**
- Either use it in a summary log at the end: `if (hasNull) Debug.Log("Found null materials")`
- Or remove if the per-item logging is sufficient

---

## 3. CS0414: Field Assigned But Never Used

### 3.1 SimpleCombatFeedback.cs:15 - `defaultHitStopDuration`

```csharp
[SerializeField] private float defaultHitStopDuration = 0.05f;  // Never used
```

**Analysis:** This serialized field is exposed in the Inspector but never referenced in code. The `TriggerHitStop(float duration)` method takes duration as a parameter instead.

**Recommendation:** **USE or REMOVE**
- Option A: Use as default in `TriggerHitStop()` when duration is 0 or negative
- Option B: Remove if designers always specify duration

---

### 3.2 WeaponEquipVisualBridge.cs - Multiple Fields (340, 342, 344, 347, 348)

| Line | Field | Analysis |
|------|-------|----------|
| 340 | `_aimReleaseDebounce` | **Stub** - Timer for aim debouncing, never decremented |
| 342 | `_lastRightHeldTime` | **Stub** - Never read, only written |
| 344 | `_wasUsingBow` | **Stub** - Bow tracking, never read |
| 347 | `_throwCharging` | **Stub** - Throwable charge state, never read |
| 348 | `_throwChargeProgress` | **Stub** - Throwable charge progress, never read |

**Analysis:** These are all **feature stubs** for:
- Aim debouncing (preventing aim flickering)
- Bow weapon switching
- Throwable weapon charging

**Recommendation:** **IMPLEMENT in EPIC14.30 refactoring or REMOVE**
- These should be wired up during the WeaponEquipVisualBridge refactoring
- If throwables/bow refinement aren't planned, remove

---

### 3.3 OpsiveClimbingIK.cs:47 - `debugLog`

```csharp
[SerializeField] private bool debugLog = false;  // Never used
```

**Analysis:** Inspector toggle for debug logging that was never wired up.

**Recommendation:** **IMPLEMENT or REMOVE**
- Either add `if (debugLog) Debug.Log(...)` statements
- Or remove if `debugDraw` is sufficient for debugging

---

### 3.4 RideMountingSystem.cs:18 - `_loggedStart`

```csharp
private bool _loggedStart;  // Set to false, never read
```

**Analysis:** One-time logging flag that was never used for its intended purpose.

**Recommendation:** **IMPLEMENT or REMOVE**
- Either use for one-time startup logging
- Or remove if periodic logging (line 35-38) is sufficient

---

### 3.5 WeaponAnimationEventRelay.cs:33 - `useExplicitEntity`

```csharp
[SerializeField] private bool useExplicitEntity = false;  // Never used
```

**Analysis:** Inspector toggle for explicit entity binding that was never implemented.

**Recommendation:** **IMPLEMENT or REMOVE**
- This was likely intended to allow explicit weapon entity assignment
- Remove if auto-discovery is always sufficient

---

### 3.6 Editor Fields (WeaponCreatorModule, WeaponTemplatesModule, CameraSetupWizard, SurfaceTestObjectCreator)

| File | Field | Analysis | Recommendation |
|------|-------|----------|----------------|
| WeaponCreatorModule.cs:21 | `_autoGenerateId` | UI toggle not wired up | IMPLEMENT or REMOVE |
| WeaponCreatorModule.cs:38 | `_createPrefab` | UI toggle not wired up | IMPLEMENT or REMOVE |
| WeaponCreatorModule.cs:39 | `_addToAnimator` | UI toggle not wired up | IMPLEMENT or REMOVE |
| WeaponTemplatesModule.cs:22 | `_templatesLoaded` | Tracking flag never checked | USE for button state or REMOVE |
| CameraSetupWizard.cs:23 | `_createNewConfig` | Wizard option never used | IMPLEMENT or REMOVE |
| SurfaceTestObjectCreator.cs:24 | `MATERIAL_SAND` | Material ID defined but never used | USE in a sand surface test or REMOVE |

---

## 4. CS0618: Obsolete API Usage

### 4.1 EquipmentSlotConfigEditor.cs (Lines 13, 137, 153, 189)

```csharp
[CustomEditor(typeof(EquipmentSlotConfig))]  // EquipmentSlotConfig is obsolete
```

**Analysis:** The entire `EquipmentSlotConfigEditor` class supports a deprecated type. The deprecation message says: "Use EquipmentSlotDefinition instead. This class will be removed in a future version."

**Recommendation:** **DELETE ENTIRE FILE**
- Create `EquipmentSlotDefinitionEditor.cs` if custom editor is needed for the new type
- Remove all references to `EquipmentSlotConfig`
- This is a cleanup task, not a feature implementation

---

### 4.2 CrosshairSetup.cs:13 - `FindObjectOfType<T>()`

```csharp
var existing = Object.FindObjectOfType<CrosshairUI>();  // Obsolete in Unity 2023+
```

**Recommendation:** **FIX - Use `FindFirstObjectByType<T>()`**
```csharp
var existing = Object.FindFirstObjectByType<CrosshairUI>();
```

---

## 5. CS0168: Exception Declared But Never Used

### 5.1 EquipmentSlotConfigEditor.cs:56

```csharp
catch (System.Exception e)  // 'e' never used
{
    // Silently fail if something is wrong to prevent editor spam
}
```

**Recommendation:** **FIX - Use discard pattern**
```csharp
catch (System.Exception)
{
    // Silently fail if something is wrong to prevent editor spam
}
```

Or if the file is being deleted (per 4.1), this is moot.

---

## Implementation Plan

### Phase 1: Quick Fixes (Low Risk)

1. **CrosshairSetup.cs** - Update `FindObjectOfType` to `FindFirstObjectByType`
2. **DamageDebugSystem.cs** - Remove `hasTriggerBuffer` (already commented out logic)
3. **Exception discard** - Use `catch (Exception)` instead of `catch (Exception e)`

### Phase 2: Stub Decisions (Requires Product Decision)

4. **DamageApplySystem.cs** - Decide: implement shield damage or remove stub?
5. **WeaponEquipVisualBridge.cs** - Decide: implement aim debounce/throw charging or remove?
6. **Editor module fields** - Wire up UI toggles or remove unused options

### Phase 3: Deprecation Cleanup

7. **Delete EquipmentSlotConfigEditor.cs** entirely
8. **Delete EquipmentSlotConfig.cs** if no longer referenced
9. Search for and remove any remaining references

### Phase 4: Warning Suppression (Optional)

10. Add `#pragma warning disable 0162` to files with intentional debug toggles
    - Alternative: Convert to `[Conditional("DEBUG")]` attributes

---

## Summary Table

| Action | Files | Warnings Fixed |
|--------|-------|----------------|
| Use `FindFirstObjectByType` | 1 | 1 |
| Remove dead code | 2 | 2 |
| Delete obsolete editor | 1 | 5 |
| Fix exception discard | 1 | 1 |
| Implement or remove stubs | 8 | 14 |
| Suppress debug logging warnings | 9 | 27 |
| **Total** | **~15 unique files** | **50 warnings** |

---

## Notes

### Debug Logging Pattern Options

The current `const bool DebugEnabled = false` pattern is common but has trade-offs:

| Approach | Pros | Cons |
|----------|------|------|
| `const bool` (current) | Zero runtime cost, easy toggle | CS0162 warnings |
| `#if DEBUG` | No warnings, clear intent | Can't enable in Release |
| `static bool` | No warnings, runtime toggle | Slight overhead |
| `[Conditional("DEBUG")]` | Clean, no warnings | Requires method extraction |

**Recommendation:** Keep current pattern but add file-level pragma:
```csharp
#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
```
