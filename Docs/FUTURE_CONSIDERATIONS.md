# Future Considerations

Low-priority fixes and improvements tracked for later.

---

## Performance — Minor Fixes

### AnimatorRigBridge Reflection Caching
**File:** `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs` (lines 397-409)
**Issue:** Uses `GetType().GetProperty("weight")` + `SetValue()` via reflection every frame in Update.
**Fix:** Cache `PropertyInfo`/`FieldInfo` in `Start()`, reuse cached reference in Update.
**Impact:** ~0.01-0.05ms per frame. Only runs on local player (1 instance). Low priority.
**Scope:** Local fix within AnimatorRigBridge — no global system needed.

### CollisionDebugLinePool Material Creation
**File:** `Assets/Scripts/Performance/CollisionDebugLinePool.cs` (line 255)
**Issue:** `new Material(Shader.Find("Sprites/Default"))` called during pool init (64 times).
**Fix:** Create one shared material, assign to all LineRenderers.
**Impact:** Init-time only, `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guard. Negligible in production builds.
**Scope:** Local fix within CollisionDebugLinePool.
