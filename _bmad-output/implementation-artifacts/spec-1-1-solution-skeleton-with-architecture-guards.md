---
title: 'Story 1.1: Solution skeleton with architecture guards'
type: 'feature'
created: '2026-11-08'
status: 'draft'
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
- [ ] `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `jira-cli.slnx` -- SDK 10.0.100 with `latestFeature`, MTP runner; `net10.0`, nullable, warnings as errors, `SatelliteResourceLanguages=en`; central package versions -- one place for stack pins
- [ ] `src/{JiraCli.Cli,JiraCli.Application,JiraCli.Domain,JiraCli.Infrastructure,Atlassian.Common}/*.csproj` -- references exactly as in AD-1; each gets one `ServiceCollectionExtensions` with `AddCli`/`AddApplication`/`AddDomain`/`AddInfrastructure`/`AddAtlassianCommon`; non-Cli projects use only `Microsoft.Extensions.DependencyInjection.Abstractions` -- AD-1 registration seam
- [ ] `src/JiraCli.Cli/Program.cs`, `CliApp.cs`, `CompositionRoot.cs`, `PlainTextOutput.cs` -- `CliApp.RunAsync(args, Func<ServiceProvider> containerFactory)`: builds the `RootCommand`, and if the parse result is `--help` or `--version`, invokes with `PlainTextOutput` (UTF-8 no-BOM `StreamWriter` over `Console.OpenStandardOutput()`) and returns without calling the factory; otherwise builds the container (`ValidateOnBuild`, `ValidateScopes`) and runs inside one `AsyncServiceScope`. Csproj: `AssemblyName` `jira-cli`, `Version` 0.1.0, `IncludeSourceRevisionInInformationalVersion` false -- FR-21 base, one scope per invocation
- [ ] `tests/*.Tests/` (5 projects) -- one test per project that its `Add…()` returns the same collection and the built provider validates; Cli tests cover the Help and Version rows, using a factory that throws -- MTP fails when a project runs zero tests
- [ ] `tests/JiraCli.ArchitectureTests/` -- `ProjectReferenceTests` (parse every src csproj `ProjectReference` **and** check `GetReferencedAssemblies()` against the AD-1 table); `InfrastructureSurfaceTests` (the only Cli type that depends on `JiraCli.Infrastructure` is `CompositionRoot`; Infrastructure exports only its extensions class); `RegistrationTests` (exactly one public `Add…(IServiceCollection)` per src assembly); `CommonIsJiraFreeTests`; `ConsoleUseTests` with `Fixtures/AsyncLambdaConsoleViolation` -- AD-20

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

## Spec Change Log

## Review Triage Log
