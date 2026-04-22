# 🤖 Session 4: Copilot Prompts

One live Copilot moment: writing a unit test that extends the existing test suite.

---

## Prompt 1: Write a Unit Test for ToolRegistry Duplicate Registration

### When
**Stage 3** (~minute 36) — after running `dotnet test` with 24 passing tests

### Context
- **File open:** `tests/OpenClawNet.UnitTests/ToolRegistryTests.cs`
- **Cursor position:** Below the last existing test method
- **What just happened:** We ran all 24 tests, walked through ToolRegistryTests (5 tests covering register, case-insensitive lookup, not found, get all, manifest)

### Mode
**Copilot Chat** (sidebar)

### Exact Prompt

```
Write a new unit test for ToolRegistry that verifies registering a tool with a duplicate name overwrites the previous registration. Register two different FakeTool instances with the same name, then verify GetTool returns the second one.
```

### Expected Result

Copilot generates a `[Fact]` test method following the existing Arrange/Act/Assert pattern:

```csharp
[Fact]
public void Register_WithDuplicateName_OverwritesPreviousRegistration()
{
    // Arrange
    var registry = new ToolRegistry();
    var firstTool = new FakeTool("duplicate-tool", "First implementation");
    var secondTool = new FakeTool("duplicate-tool", "Second implementation");

    // Act
    registry.Register(firstTool);
    registry.Register(secondTool);

    // Assert
    var result = registry.GetTool("duplicate-tool");
    Assert.NotNull(result);
    Assert.Same(secondTool, result);
}
```

### Why It's Interesting

- **Pattern matching** — Copilot reads the existing test patterns (FakeTool, Arrange/Act/Assert, `[Fact]` attribute) and generates code that fits naturally
- **Real test value** — duplicate registration behavior is an important edge case that wasn't covered in the original 24 tests
- **Immediate validation** — run `dotnet test` and see 25 pass (up from 24), proving the test is valid
- **Series callback** — we're testing Session 2's ToolRegistry using Session 4's testing patterns, showing how the architecture comes full circle
- **Copilot reads context** — it sees the `FakeTool` helper class and the existing assertion style, generating consistent code

### How to Verify

```bash
# Before: 24 tests pass
dotnet test --verbosity normal

# Add the generated test, then:
dotnet test --verbosity normal
# After: 25 tests pass ✅
```
