---
title: 'Story 1.1: Solution skeleton with architecture guards'
type: 'feature'
created: '2026-11-08'
status: 'done'
baseline_commit: '7c78c35fb6db7ff97e1e3dc69d49e6ef1f8bed78'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The repo has no code. Every later story needs the five-project hexagonal split, and its dependency rules must be enforced by tests so the structure cannot drift.

**Approach:** Create a .NET 10 solution with the five src projects, one test project each, and `JiraCli.ArchitectureTests`. Add tests for AD-1, AD-2, and console use. Ship a `jira-cli` root command whose `--help`/`--version` print plain text without building the DI container.

## Boundaries & Constraints

**Always:** Stack versions from the spine (System.CommandLine 2.0.14, Microsoft.Extensions.DependencyInjection 10.0.14, xunit.v3 4.0.2, NetArchTest.eNhancedEdition 1.4.5). `global.json` sets `test.runner` to `Microsoft.Testing.Platform`. Test-first. Implementations `internal` where possible. Each src project has exactly one public static `Add…(this IServiceCollection)` method.

**Never:** No subcommands, config loading, envelope, `ExceptionMapper`, parser hardening, or work log (Stories 1.2–1.7). No Jira calls. No `Console.OutputEncoding` changes (it changes the user's terminal code page).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Help | `jira-cli --help` | Plain-text usage on stdout, exit 0, container factory never called | N/A |
| Version | `jira-cli --version` | `0.1.0` on stdout, exit 0, container factory never called | N/A |
| Bad reference | `Domain` or `Common` csproj gains a `ProjectReference` | Architecture test fails and names the project | N/A |
| Jira in Common | `Atlassian.Common` has a type, member, or string literal containing a forbidden token | AD-2 test fails and names it | N/A |
| Console in async lambda | Fixture type calls `Console.WriteLine` inside an `async` lambda | Rule reports the fixture's outer type | N/A |

</frozen-after-approval>

## Code Map

Greenfield. Only `.gitignore` (already ignores bin/obj/TestResults) and BMad files exist. SDK 10.0.403 is installed. Layout follows the spine's Structural Seed: `src/`, `tests/`, `global.json`.

## Tasks & Acceptance

**Execution:**
- [x] `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `jira-cli.slnx` -- SDK 10.0.100 with `latestFeature`, MTP runner; `net10.0`, nullable, warnings as errors, `SatelliteResourceLanguages=en`; central package versions -- one place for stack pins
- [x] `src/{JiraCli.Cli,JiraCli.Application,JiraCli.Domain,JiraCli.Infrastructure,Atlassian.Common}/*.csproj` -- references exactly as in AD-1; each gets one `ServiceCollectionExtensions` with `AddCli`/`AddApplication`/`AddDomain`/`AddInfrastructure`/`AddAtlassianCommon`; non-Cli projects use only `Microsoft.Extensions.DependencyInjection.Abstractions` -- AD-1 registration seam
- [x] `src/JiraCli.Cli/Program.cs`, `CliApp.cs`, `CompositionRoot.cs`, `PlainTextOutput.cs` -- `CliApp.RunAsync(args, Func<ServiceProvider> containerFactory)`: builds the `RootCommand`, and if the parse result is `--help` or `--version`, invokes with `PlainTextOutput` (UTF-8 no-BOM `StreamWriter` over `Console.OpenStandardOutput()`) and returns without calling the factory; otherwise builds the container (`ValidateOnBuild`, `ValidateScopes`) and runs inside one `AsyncServiceScope`. Csproj: `AssemblyName` `jira-cli`, `Version` 0.1.0, `IncludeSourceRevisionInInformationalVersion` false -- FR-21 base, one scope per invocation
- [x] `tests/*.Tests/` (5 projects) -- one test per project that its `Add…()` returns the same collection and the built provider validates; Cli tests cover the Help and Version rows, using a factory that throws -- MTP fails when a project runs zero tests
- [x] `tests/JiraCli.ArchitectureTests/` -- `ProjectReferenceTests` (parse every src csproj `ProjectReference` **and** check `GetReferencedAssemblies()` against the AD-1 table); `InfrastructureSurfaceTests` (the only Cli type that depends on `JiraCli.Infrastructure` is `CompositionRoot`; Infrastructure exports only its extensions class); `RegistrationTests` (exactly one public `Add…(IServiceCollection)` per src assembly); `CommonIsJiraFreeTests`; `ConsoleUseTests` with `Fixtures/AsyncLambdaConsoleViolation` -- AD-20

**Acceptance Criteria:**
- Given a fresh clone with the .NET 10 SDK, when `dotnet build` and `dotnet test` run at the repo root, then both succeed and every test project runs at least one test.
- Given any src type, when it references `System.Console` and its outermost declaring type is not in the allow-list, then `ConsoleUseTests` fails naming that type.
- Given the built tool, when `--help` or `--version` runs, then stdout is plain text (not JSON) and the exit code is 0.

## Design Notes

- **Console rule:** Use Mono.Cecil (a NetArchTest dependency) to scan every method body, including compiler-generated nested types (`<>c`, `d__N` state machines). Flag any `System.Console` member reference and map it to the outermost declaring type. The allow-list is a single table in the test project. In 1.1 it holds only `JiraCli.Cli.PlainTextOutput`; Story 1.2 adds `EnvelopeWriter` and the warning writer. The rule bans every `System.Console` use, not just output, which is stricter than the AC. The same function runs against the fixture namespace to prove detection.
- **AD-2 tokens** (case-insensitive, matched against namespaces, type and member names, and `ldstr` literals in `Atlassian.Common`): `jira`, `jql`, `issue`, `customfield`, `/rest/api`.
- **Repo root for csproj parsing:** walk up from `AppContext.BaseDirectory` until `jira-cli.slnx` is found.
- **xunit.v3 on MTP:** test projects are `OutputType Exe`. If 4.0.2 needs a specific MTP package variant, pick it per the xunit docs and record it in Implementation Notes.

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings, 0 errors
- `dotnet test` -- expected: all projects pass, none report zero tests
- `dotnet run --project src/JiraCli.Cli -- --version` -- expected: `0.1.0`, exit 0

## Implementation Notes

- **xunit.v3 on MTP:** `xunit.v3` 4.0.2 already depends on `xunit.v3.mtp-v2` (Microsoft.Testing.Platform v2), so no variant package is needed. `tests/Directory.Build.props` sets `OutputType Exe` and adds the global `using Xunit;` (xunit.v3 does not add it).
- **Version in-process:** System.CommandLine's `--version` reads the *entry* assembly. Under the test host that is the test exe, so in-process tests check only exit 0, plain text, and that the factory is never called. `BuiltToolTests` runs the real `jira-cli.dll` as a process and checks for exactly `0.1.0`, no BOM, not JSON, empty stderr, exit 0.
- **Help/version detection:** `parseResult.Action` is compared by reference with the root's `HelpOption`/`VersionOption` actions (`VersionOptionAction` is not public).
- **Cecil reading:** `IlScanner` loads the image into a `MemoryStream`, because Cecil reads method bodies lazily and a disposed file-backed module throws.
- **AD-2 self-test fixture:** `Fixtures/CommonLeak/IssueFetcher` proves the token scanner names types, members, and literals. Its namespace differs from the console fixture namespace, so each self-test sees only its own fixture.
- **Mutation checks (manual, reverted):** a Domain→Common reference failed both reference tests, naming `JiraCli.Domain`. A Common async lambda with `Console.Error` and `customfield_1` failed the console and AD-2 tests. A second Cli type calling `AddInfrastructure` failed the surface test.

## Spec Change Log

## Review Triage Log

Pass 1 (layers: blind-hunter `BH`, edge-case-hunter `EC`, verification-gap `VG`):

| # | Finding | Verdict | Evidence | Route |
|---|---------|---------|----------|-------|
| BH1 | `CliApp` creates the scope but passes it to nothing | low | True, but 1.1 has no command that could resolve from it; wiring actions to the scope needs new surface and is the Program-wrapper work of Story 1.2 | reject |
| BH2 | Parse errors use System.CommandLine's default console writers | medium | True (`CliApp.cs` non-help path), but the frozen Never excludes parser hardening and the envelope (Stories 1.2–1.7) | reject (intent-excluded) |
| BH3 | `Other_invocations_build_the_container_exactly_once` ignores the exit code | low | True; the no-arg run exits 0 today and nothing pins it; one-line assert | patch |
| BH4 | Mono.Cecil is used directly but comes in only transitively (0.11.6 via NetArchTest) | low | True per `project.assets.json`; contradicts "one place for stack pins"; direct pin is a two-line fix | patch |
| BH5 | AD-2 scan misses `const` values and attribute arguments | medium | True: a `const` is stored in field metadata and its `ldstr` lands at the use site in another assembly, so the frozen "string literal" row can be bypassed | patch |
| BH6 | `issue` substring will flag `Issuer`/`IssuedAt` | low | The token list is the spec's design; no current Common code hits it; changing matching is a design change | reject |
| BH7 | No guard that non-Cli projects use only DI.Abstractions | low | Code complies; a new package-reference rule adds a test for a drift nobody has shown | reject |
| BH8 | `BuiltToolTests` has no process timeout; `--help` content unchecked; `.Trim()` | low | Hang needs a hung tool (none shown); help content is asserted in-process by `CliAppTests`; `Trim` only drops the expected newline | reject |
| BH9 | Spec status `in-review` vs sprint-status `in-progress`; mutation checks not repeatable | false | The workflow syncs sprint-status at presentation; each rule has a committed self-test (reference, AD-2, console) | reject |
| BH10 | `IlScanner` does not dispose its module; `Outermost` duplicated | low | `MemoryStream` is managed memory only; the two `Outermost` helpers act on different type systems (Cecil vs reflection) | reject |
| VG1 | AD-2 scan misses consts, default parameters, attribute arguments | medium | Pre-verified; same root cause as BH5 | patch (with BH5) |
| VG2 | Tests cannot tell whether `ValidateOnBuild`/`ValidateScopes` are on | medium | Pre-verified; nothing is registered yet, so validation cannot be observed without a test hook (new surface) | defer |
| VG3 | Scope unused | low | Same as BH1 | reject |
| VG4 | Errors bypass the plain-text writer | medium | Same as BH2 | reject (intent-excluded) |
| VG5 | Mono.Cecil transitive | low | Same as BH4 | patch (with BH4) |
| EC1 | Closed stdout pipe during `--help` throws `IOException` | low | Help fits one 4 KB buffer, so only a reader closing before one flush can hit it; error mapping is Story 1.2 | reject |
| EC2 | Parse error builds the container, then writes to default console | medium | Same as BH2 | reject (intent-excluded) |
| EC3 | Throwing container factory gives a raw stack trace | medium | True, but `ExceptionMapper` is excluded by the frozen Never (Story 1.2) | reject (intent-excluded) |
| EC4 | AD-2 misses const and attribute strings | medium | Same as BH5 | patch (with BH5) |
| EC5 | AD-2 misses parameter and generic-parameter names | low | True: names like `issueKey` in Common pass; the fix extends the same scanner by a few lines | patch (with BH5) |
| EC6 | Mono.Cecil transitive | low | Same as BH4 | patch (with BH4) |
| EC7 | `;`-separated or Update-only `ProjectReference` | low | Not a pattern in this repo; handling needs extra parsing branches | reject |
| EC8 | `ProjectReference` injected by imported props | low | No src-level props injects references; MSBuild evaluation would be a large addition | reject |
| EC9 | Duplicate csproj names make `ToDictionary` throw | false | A loud test failure is correct behavior for an impossible layout (one csproj per project folder) | reject |
| EC10 | A second seam taking `IServiceCollection` as a later parameter | low | Unlikely shape; broadening the rule adds branching for a drift nobody has shown | reject |
| EC11 | Infrastructure types outside the `JiraCli.Infrastructure` namespace prefix | low | `RootNamespace` defaults to the project name; nothing places types elsewhere | reject |
| EC12 | Hung child process in `BuiltToolTests` | low | Same as BH8 | reject |
| EC13 | `DOTNET_HOST_PATH` unset and `dotnet` not on PATH | low | `dotnet test` sets the variable; a machine without `dotnet` on PATH cannot run `dotnet test` either | reject |
| EC14 | No-arg exit code ignored | low | Same as BH3 | patch (with BH3) |
| EC15 | Scope unused | low | Same as BH1 | reject |
| EC16 | Extra public non-`IServiceCollection` member on Infrastructure's extensions class passes | low | True but no such member exists or is planned; Cli access is already limited to `CompositionRoot` | reject |
| EC17 | Const/attribute literals slip through the AD-2 row | medium | Same as BH5 | patch (with BH5) |
| EC18 | Mono.Cecil not pinned centrally | low | Same as BH4 | patch (with BH4) |
