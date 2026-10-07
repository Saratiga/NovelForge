# Custom Command Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `Command` subclass marked `[NovelCommand("name")]` compiles everywhere a script is compiled — runtime `NovelRunner`, importer, script editor window, character editor, autocomplete — with no call-site changes.

**Architecture:** `CommandRegistry.CreateDefault()` registers built-ins, then registers every discovered attributed type. Discovery is split in two: a pure `RegisterTypes(registry, pairs)` that validates and registers (unit-testable with plain types), and a cached `FindAttributedTypes()` that scans loaded assemblies.

**Tech Stack:** C#, Unity 6000.6, NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-10-07-architecture-fixes-design.md](../specs/2026-10-07-architecture-fixes-design.md), section A.

## Global Constraints

- Unity Editor **6000.6.0f1** at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package `com.novelforge.core`, root `G:\ClaudeProjects\NovelForge`.
- No new package dependency.
- Test command (absolute paths, no `-quit`; poll for a fresh `TestResults.xml` every ~20–30 s up to ~90 s; if "Couldn't set project path", create `TestProject~\Assets`):
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
- Baseline: **229 passing tests** on `master` `4c7013c`. Each task states the expected total.
- `.meta` files are Unity-generated only. `git add` whole containing folders, never bare `.cs` paths; check `git status` before every commit.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Unity Test Framework fails a test on any unexpected `Debug.LogError`. Therefore **no invalid type in any test assembly may carry `[NovelCommand]`** — invalid-type tests go through `RegisterTypes` with explicit `(name, Type)` pairs on un-attributed types. Every attributed test command uses a `test_` name prefix.

## Review Focus

- Custom constructor throws on bad args → `ParseException` message carries the inner exception's message, not "Exception has been thrown by the target of an invocation". Test in Task 1.
- Custom command named like a built-in (`bg`) → built-in keeps working, one error logged. Test in Task 1.
- Assembly that fails `GetTypes()` (`ReflectionTypeLoadException`) → scan continues with the types that did load, no exception escapes `CreateDefault()`. Covered by implementation rule in Task 2 (not unit-testable without a broken assembly).
- `CreateDefault()` called hundreds of times (every keystroke in the script window) → scan runs once. Test in Task 2.
- Script with a custom command imported as `.nfscript` → no import error. Test in Task 3.

---

### Task 1: `NovelCommandAttribute` and `RegisterTypes`

**Files:**
- Create: `Runtime/Parsing/NovelCommandAttribute.cs`
- Modify: `Runtime/Parsing/CommandRegistry.cs`
- Test: `Tests/Runtime/CommandDiscoveryTests.cs`

**Interfaces:**
- Produces:
  - `[AttributeUsage(AttributeTargets.Class, Inherited = false)] public sealed class NovelCommandAttribute : Attribute` with `public NovelCommandAttribute(string name)` and `public string Name { get; }`.
  - `internal static void CommandRegistry.RegisterTypes(CommandRegistry registry, IEnumerable<(string name, Type type)> candidates)`.
  - `public bool CommandRegistry.IsRegistered(string commandName)`.

- [ ] **Step 1: Write failing tests** in `Tests/Runtime/CommandDiscoveryTests.cs`, namespace `NovelForge.Runtime.Tests`. Declare un-attributed helper types inside the test class: `EchoCommand : Command` (public ctor `(string rawArgs)`, stores it in `public string Args`), `NoStringCtorCommand : Command` (only a parameterless ctor), `abstract AbstractCommand : Command`, `NotACommand` (plain class with `(string)` ctor), `ThrowingCommand : Command` whose ctor throws `new FormatException("bad speed")`.

```csharp
[Test] public void RegisterTypes_ValidType_CreatesInstanceWithRawArgs()
// registry = new CommandRegistry(); RegisterTypes(registry, new[] { ("echo", typeof(EchoCommand)) });
// Assert.IsTrue(registry.TryCreate("echo", "hi there", out var cmd));
// Assert.AreEqual("hi there", ((EchoCommand)cmd).Args);

[Test] public void RegisterTypes_TypeWithoutStringCtor_LogsErrorAndSkips()
// LogAssert.Expect(LogType.Error, new Regex("NoStringCtorCommand.*constructor"));
// Assert.IsFalse(registry.IsRegistered("nostr"));

[Test] public void RegisterTypes_AbstractType_LogsErrorAndSkips()       // Regex "AbstractCommand"
[Test] public void RegisterTypes_NonCommandType_LogsErrorAndSkips()     // Regex "NotACommand"

[Test] public void RegisterTypes_NameAlreadyRegistered_LogsErrorAndKeepsFirst()
// registry = CommandRegistry.CreateDefault(); RegisterTypes(registry, new[] { ("bg", typeof(EchoCommand)) });
// LogAssert.Expect(LogType.Error, new Regex("'bg'.*already registered"));
// registry.TryCreate("bg", "room", out var cmd); Assert.IsInstanceOf<ShowBackgroundCommand>(cmd);

[Test] public void Compile_CustomCtorThrows_ParseExceptionCarriesInnerMessage()
// registry = new CommandRegistry(); RegisterTypes(registry, new[] { ("boom", typeof(ThrowingCommand)) });
// var ex = Assert.Throws<ParseException>(() => new ScriptCompiler(registry).Compile("label a\nboom x\n"));
// StringAssert.Contains("bad speed", ex.Message); Assert.AreEqual(2, ex.LineNumber);
```

- [ ] **Step 2: Run tests, expect compile failure** (`NovelCommandAttribute`, `RegisterTypes`, `IsRegistered` missing).

- [ ] **Step 3: Implement.** `NovelCommandAttribute` as in Interfaces. `IsRegistered` = `_factories.ContainsKey`. `RegisterTypes` checks in this order, logging `Debug.LogError` with the type's `FullName` and skipping: not assignable to `Command`; abstract; no public `(string)` constructor (`type.GetConstructor(new[] { typeof(string) })`); name already registered. Factory: `args => (Command)ctor.Invoke(new object[] { args })`, catching `TargetInvocationException e` and rethrowing `e.InnerException` via `ExceptionDispatchInfo.Capture(e.InnerException).Throw()`. Error texts:
  - `NovelForge: [NovelCommand] type '{FullName}' does not derive from Command — skipped.`
  - `NovelForge: [NovelCommand] type '{FullName}' is abstract — skipped.`
  - `NovelForge: [NovelCommand] type '{FullName}' needs a public constructor (string rawArgs) — skipped.`
  - `NovelForge: [NovelCommand] '{name}' on '{FullName}' is already registered — skipped.`

- [ ] **Step 4: Run tests.** Expected: **235 passing**.

- [ ] **Step 5: Commit** `Runtime/Parsing/` and `Tests/Runtime/` folders: `feat: add NovelCommand attribute and validated type registration`.

---

### Task 2: Assembly scan inside `CreateDefault()`

**Files:**
- Modify: `Runtime/Parsing/CommandRegistry.cs`
- Create: `Tests/Runtime/Doubles/TestPingCommand.cs`
- Test: `Tests/Runtime/CommandDiscoveryTests.cs`

**Interfaces:**
- Consumes: Task 1 `RegisterTypes`, `NovelCommandAttribute`.
- Produces: `internal static IReadOnlyList<(string name, Type type)> CommandRegistry.FindAttributedTypes()` (cached); `CreateDefault()` now includes discovered commands. `internal static int CommandRegistry.ScanCountForTesting`.

- [ ] **Step 1: Add the test double** `Tests/Runtime/Doubles/TestPingCommand.cs`: `[NovelCommand("test_ping")] public class TestPingCommand : Command`, ctor `(string rawArgs)` stores `public string Args`, `Execute` does `yield break`.

- [ ] **Step 2: Write failing tests**

```csharp
[Test] public void CreateDefault_IncludesAttributedCommandFromLoadedAssembly()
// Assert.IsTrue(CommandRegistry.CreateDefault().TryCreate("test_ping", "pong", out var cmd));
// Assert.AreEqual("pong", ((TestPingCommand)cmd).Args);

[Test] public void ScriptCompiler_Default_CompilesAttributedCommand()
// var script = new ScriptCompiler().Compile("label a\ntest_ping hello\n");
// Assert.IsInstanceOf<TestPingCommand>(script.Commands[0]);

[Test] public void CreateDefault_RepeatedCalls_ScanAssembliesOnce()
// CommandRegistry.CreateDefault(); int before = CommandRegistry.ScanCountForTesting;
// for (int i = 0; i < 50; i++) CommandRegistry.CreateDefault();
// Assert.AreEqual(before, CommandRegistry.ScanCountForTesting);
// Assert.LessOrEqual(before, 1);
```

- [ ] **Step 3: Run tests, expect FAIL** (`Unknown command 'test_ping'`).

- [ ] **Step 4: Implement.** `FindAttributedTypes()` backed by `static readonly Lazy<IReadOnlyList<(string, Type)>>`; the factory increments `ScanCountForTesting`. Scan `AppDomain.CurrentDomain.GetAssemblies()` where `assembly == typeof(Command).Assembly` or `assembly.GetReferencedAssemblies()` contains name `NovelForge.Runtime`. Per assembly: `GetTypes()`, on `ReflectionTypeLoadException e` use `e.Types` minus nulls. Keep types with `GetCustomAttribute<NovelCommandAttribute>()`, sort by `FullName` with `StringComparer.Ordinal`. `CreateDefault()` = existing built-ins, then `RegisterTypes(registry, FindAttributedTypes())`.

- [ ] **Step 5: Run tests.** Expected: **238 passing**.

- [ ] **Step 6: Commit** `Runtime/Parsing/` and `Tests/Runtime/`: `feat: discover [NovelCommand] types in CommandRegistry.CreateDefault`.

---

### Task 3: Editor surfaces see custom commands; docs

**Files:**
- Create: `Tests/Editor/Doubles/TestEditorCommand.cs`
- Test: `Tests/Editor/NovelScriptImporterTests.cs`, `Tests/Editor/DslAutocompleteProviderTests.cs`
- Modify: `USAGE.md`

**Interfaces:**
- Consumes: Task 2 `CreateDefault()` discovery. No production code change expected in `Editor/`; if a test fails, the fix is in Editor code calling something other than `CreateDefault()`.

- [ ] **Step 1: Add double** `[NovelCommand("test_editor_cmd")] public class TestEditorCommand : Command` (ctor `(string rawArgs)`, `Execute` yields break) in namespace `NovelForge.Editor.Tests`.

- [ ] **Step 2: Write tests**

```csharp
// NovelScriptImporterTests
[Test] public void ScriptWithAttributedCustomCommand_ImportsWithoutError()
// path = WriteScriptFile("custom.nfscript", "label start\ntest_editor_cmd 1\n");
// AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
// LogAssert.NoUnexpectedReceived();
// Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(path));
```

```csharp
// DslAutocompleteProviderTests
[Test] public void GetCandidates_WithDefaultRegistryNames_OffersCustomCommand()
// var result = DslAutocompleteProvider.GetCandidates("test_e", 6,
//     CommandRegistry.CreateDefault().RegisteredNames, System.Array.Empty<string>());
// CollectionAssert.Contains(result, "test_editor_cmd");
```

- [ ] **Step 3: Run tests.** Expected: **240 passing** (both pass on first run if Task 2 is correct; that is the point of the task).

- [ ] **Step 4: Document** in `USAGE.md` (Russian, matching the file): new section «Собственные команды» with a 10–15 line example: `[NovelCommand("shake")] public class ShakeCommand : Command`, constructor `(string rawArgs)`, `Execute(StoryContext, IStoryPointer)`, script line `shake 0.5`; rules: class not abstract, public `(string)` constructor, name must not clash with built-ins `bg`, `cg`, `music`, `sfx`, `wait`; the command is picked up by the importer, editor window and `NovelRunner` automatically.

- [ ] **Step 5: Commit** `Tests/Editor/` and `USAGE.md`: `test: cover custom commands in importer and autocomplete; document NovelCommand`.
