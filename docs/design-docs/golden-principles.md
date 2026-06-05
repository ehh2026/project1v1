# Golden Principles

Opinionated mechanical rules for agent consistency. When documentation is insufficient, promote violations into structural tests.

## Principles

### 1. Parse at boundary

Deserialize `visual-config.json` and `locations.json` into typed `Models/` classes at load time. Never use raw `JObject` or dynamic parsing in `Views/`.

**Enforced by:** convention + code review  
**Promote to test when:** second agent stores untyped JSON in Views

### 2. Content paths via ContentLoader

Resolve all `Images&Content/` paths through `Services/ContentLoader.cs`. Views and MainWindow must not construct content paths ad hoc.

**Enforced by:** [golden-principles.md](golden-principles.md) + review  
**Promote to test when:** second direct path construction found

### 3. Coordinate math in Utilities

Projection, validation, and clustering math live in `Utilities/`. Do not duplicate coordinate logic in Views or Services.

**Enforced by:** `Tests/CoordinateMapperTests.cs`, `Tests/CoordinateValidatorTests.cs`

### 4. Thin Views

Views contain event wiring and UI binding only. Business logic belongs in `Services/`.

**Enforced by:** layer dependency tests

### 5. Layer boundaries

```
Models ← Utilities, Services ← Views ← MainWindow/App
```

Views reference Models only. See [ARCHITECTURE.md](../../ARCHITECTURE.md).

**Enforced by:** `Tests/Architecture/LayerDependencyTests.cs`

### 6. Structured logging

Use `ILogger` / `FileLogger` in Services. Avoid `Console.WriteLine` in Services and Views (exception: `FileLogger.cs` mirrors to console by design).

**Enforced by:** `scripts/verify_taste.py`  
**Grandfathered debt:** pre-harness `Console.WriteLine` in some Views — remove incrementally (TD-001)

### 7. File size limit

Keep `.cs` files under 800 lines. Split into partial classes or extract Services when larger.

**Enforced by:** `scripts/verify_taste.py`  
**Grandfathered debt:** `MainWindow.xaml.cs` — tracked in [tech-debt-tracker.md](../exec-plans/tech-debt-tracker.md) TD-001

### 8. Promotion rule

When the same rule is violated twice:

1. Add explicit wording here
2. Add structural test or `verify_taste.py` check
3. Remove grandfather entry if applicable

## Update Cadence

- Review after each agent failure post-mortem
- Review monthly during doc-gardening (see [agent-workflows.md](../agent-workflows.md))
- Update [QUALITY_SCORE.md](../QUALITY_SCORE.md) when principles change
