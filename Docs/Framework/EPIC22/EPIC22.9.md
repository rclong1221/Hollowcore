# EPIC 22.9: Unit Tests

**Status**: 🔲 NOT STARTED  
**Priority**: LOW  
**Estimated Effort**: 3 days  
**Dependencies**: 22.1 (Assembly Definitions)

---

## Goal

Create unit tests to ensure stability and prevent regressions.

---

## Test Structure

```
/Assets/Scripts/Player/Tests/
├── EditMode/
│   ├── MovementTests.cs
│   ├── GroundCheckTests.cs
│   ├── ConfigValidationTests.cs
│   └── ...
├── PlayMode/
│   ├── MovementIntegrationTests.cs
│   ├── ClimbingIntegrationTests.cs
│   ├── NetworkPredictionTests.cs
│   └── ...
└── Player.Tests.asmdef
```

---

## Tasks

### Phase 1: Core Tests
- [ ] `MovementMathTests` - Velocity calculations
- [ ] `GroundCheckTests` - Raycast logic
- [ ] `CapsuleCacheTests` - Cache operations
- [ ] `CoordinateTests` - Transformations

### Phase 2: Component Tests
- [ ] `PlayerStateTests` - State transitions
- [ ] `HealthTests` - Damage/heal logic
- [ ] `StaminaTests` - Drain/recovery

### Phase 3: System Integration Tests
- [ ] `MovementIntegrationTests` - End-to-end movement
- [ ] `ClimbingIntegrationTests` - Climb lifecycle
- [ ] `CombatIntegrationTests` - Damage flow

### Phase 4: Network Tests
- [ ] `PredictionTests` - Input prediction
- [ ] `ReconciliationTests` - State correction
- [ ] `LatencySimulationTests` - High latency behavior

### Phase 5: CI Integration
- [ ] Create test runner configuration
- [ ] Add to GitHub Actions
- [ ] Coverage reporting

---

## Test Examples

```csharp
[Test]
public void Movement_WalkSpeed_AppliesCorrectVelocity()
{
    var config = CreateDefaultConfig();
    config.WalkSpeed = 3f;
    
    var velocity = CalculateMovementVelocity(
        input: new float2(0, 1), 
        config: config,
        isRunning: false
    );
    
    Assert.AreEqual(3f, math.length(velocity), 0.01f);
}

[Test]
public void GroundCheck_OnGround_ReturnsTrue()
{
    var result = PerformGroundCheck(
        position: new float3(0, 0.1f, 0),
        groundHeight: 0f,
        checkDistance: 0.2f
    );
    
    Assert.IsTrue(result.IsGrounded);
}
```

---

## Coverage Targets

| Area | Target |
|------|--------|
| Core movement | 70% |
| Ground check | 80% |
| Components | 60% |
| Systems | 50% |
| Networking | 40% |

---

## Success Criteria

- [ ] 50%+ overall coverage
- [ ] All tests pass
- [ ] Tests run in < 2 minutes
- [ ] No flaky tests
- [ ] CI integration working
