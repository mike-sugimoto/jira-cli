---
title: 'Story 1.2: Fixed config location and config loading'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b5c552216a32a813f4e453a63c78c4fe614d9b9f'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** jira-cli has no config. Every later story needs connection settings and the allowed projects, read from one fixed user-level file that no argument or environment variable can redirect (AD-11).

**Approach:** Resolve the config path from OS user identity (Windows known folder with an explicit process token, Linux passwd home), load it once per invocation through a source-generated `Load(path, JsonTypeInfo<T>)`, and map it to `ConnectionSettings` (Common) and `AccessSettings` (Domain). Every failure is a value naming the cause or key, never a value from the file.

## Boundaries & Constraints

**Always:** Test-first. AD-2: Common gets the app name (`jira-cli`) and schema from the caller. Validation messages and hints never contain a value from the file. `ConnectionSettings` has no auto-generated `ToString` that prints `apiToken`. Unknown keys inside the `jira-cli` section are an error naming the key; unknown top-level keys are ignored (room for a future Confluence section). JSON comments and trailing commas are allowed; a UTF-8 BOM is tolerated; duplicate keys are an error if .NET 10 source generation supports rejecting them. Regexes use `[0-9]`, `\z`, and culture-invariant matching.

**Never:** No envelope, exit code 3, `ExceptionMapper`, or parser hardening (1.4); no `doctor` (1.7); no redaction or log (1.5); no HTTP. No file writes. No fallback path. No environment variable or `Environment.GetFolderPath` read anywhere in src. No macOS support (lookup failure value).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid | full config, `allowedProjects: ["abc"]` | success; `ProjectKey` `ABC`; defaults 20/100; site lower-cased | N/A |
| Scoped token | valid config plus `cloudId` GUID | `CloudId` set | N/A |
| Hostile env | decoy config under `HOME`, `XDG_*`, `USERPROFILE`, `HOMEDRIVE`, `HOMEPATH`, `APPDATA`, `LOCALAPPDATA` | resolved path is never under a decoy | N/A |
| Lookup fails | native lookup returns error or empty home | failure `pathUnavailable` | no fallback |
| Missing / unreadable | no file, or access denied | failure `missing` / `unreadable` with the path | N/A |
| Bad JSON / wrong type | syntax error or `"defaultPageSize": "x"` | failure `invalid` naming the key path and line | never the JSON exception message |
| Bad value | site `a.b`, bad GUID, `allowedProjects` empty or `["1X"]`, passthrough `cf_1`, page sizes `0` or default > max, required key missing | failure `invalid` naming the key, e.g. `jira-cli.allowedProjects[0]` | value absent from message and hint |

</frozen-after-approval>

## Code Map

- `src/JiraCli.Cli/CompositionRoot.cs` -- `Build()` loads config and registers the result as a singleton; add internal `Build(ConfigLoadResult, Action<IServiceCollection>?)` test seam. Keep `ValidateOnBuild`/`ValidateScopes`.
- `src/JiraCli.Cli/CliApp.cs` -- no change; `--help`/`--version` skip the factory, so they skip loading.
- `tests/JiraCli.ArchitectureTests/IlScanner.cs`, `ConsoleUseTests.cs` -- reuse the Cecil scan and fixture self-test pattern.

## Tasks & Acceptance

**Execution:**
- [x] `src/Atlassian.Common/Paths/` -- `UserPaths` (Windows `FOLDERID_RoamingAppData` via `SHGetKnownFolderPath` + `OpenProcessToken(TOKEN_QUERY|TOKEN_IMPERSONATE)`, `KF_FLAG_DONT_VERIFY`; Linux `getpwuid_r(geteuid())` with `ERANGE` retry, absolute home required) behind an internal native-lookup seam; `ConfigFile(appName)` → `<Roaming>\<app>\config.json` or `<home>/.config/<app>/config.json` -- AD-11, reused by 1.5 and Epic 4
- [x] `src/Atlassian.Common/Config/` -- `ConfigFileLoader.Load<T>(path, JsonTypeInfo<T>)` → result value (`pathUnavailable`/`missing`/`unreadable`/`invalid` + key path + line + path); `ConnectionSettings(site, email, apiToken, cloudId?)` with ctor guards -- generic loading
- [x] `src/JiraCli.Domain/` -- `ProjectKey` (`TryParse`, `ToUpperInvariant`, `^[A-Z][A-Z0-9_]+\z`), `AccessSettings` (keys deduped, passthrough ids, page sizes) -- Domain value objects
- [x] `src/JiraCli.Cli/Config/` -- nullable schema classes + `JsonSerializerContext`, `[JsonExtensionData]` on the section for unknown keys, `ConfigMapper` (all validation, key-named messages and hints), `ConfigLoadResult` (success: `ConnectionSettings` + `AccessSettings`; failure: kind, key, message, hint, path) -- schema stays in Cli
- [x] `tests/*` -- matrix rows; secret-shaped values in every invalid field never appear in message/hint; hostile-env tests in a `DisableParallelization` collection, OS-conditional via `Assert.Skip`; container tests: real `Build()` registers `ConfigLoadResult`, seam proves a missing dependency fails at build and a scoped service fails from root
- [x] `tests/JiraCli.ArchitectureTests/EnvironmentUseTests.cs` + fixture -- no src type calls `Environment.GetFolderPath`, `GetEnvironmentVariable(s)`, or `ExpandEnvironmentVariables`; self-test proves detection
- [x] `deferred-work.md` -- mark the 1.1 `ValidateOnBuild` entry resolved by 1.2

**Acceptance Criteria:**
- Given `dotnet build` and `dotnet test` at the repo root, then 0 warnings and all tests pass, existing ones included.
- Given any non-help invocation, when the container is built, then config is loaded exactly once from the fixed path and a load failure does not throw.
- Given AD-2, when Common gains config and path code, then `CommonIsJiraFreeTests` still passes.

## Design Notes

- Report `JsonException.Path` and `LineNumber` only; its `Message` can quote file content (a token).
- Missing required keys are detected in the mapper (nullable schema), not with `required`, so the key can be named.
- Page sizes: `1 ≤ defaultPageSize ≤ maxPageSize`; no upper cap (Jira enforces its own).

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings, 0 errors
- `dotnet test` -- expected: all pass; Linux-only tests skipped on Windows
- `wsl dotnet test` or Docker with the .NET 10 SDK, if available -- expected: Linux path tests pass

## Implementation Notes

- `UserPaths.ConfigFile(appName)` returns a `UserPathLookup` value (path or problem); `ConfigFileLoader.Load<T>` takes that value, so `pathUnavailable` flows through the same result as `missing`/`unreadable`/`invalid`. Seam: internal `IUserFolderSource` (`NativeUserFolderSource` in production).
- Linux P/Invoke uses `DllImport("libc")`; verified on WSL Ubuntu (aarch64, glibc) with the .NET 10 SDK. musl/Alpine not tested.
- Duplicate keys: .NET 10 `JsonSourceGenerationOptions.AllowDuplicateProperties = false` is used and works for known keys, section keys, and extension-data keys.
- A syntax error between members has no key path (`JsonException.Path` is `$`), so only the line is reported.
- Key names from the file are masked (human decision after walkthrough, 2026-11-14): a key name may be a pasted secret, so messages, hints, and `Failure.Key` keep only names jira-cli knows (`ConfigKeys.MaskUnknownNames`); from the first unknown name on, the path reads `<unknown key>`. An unknown `jira-cli` key is reported as `jira-cli.<unknown key>`, and the hint lists the accepted keys. This narrows the frozen "an error naming the key" for unknown keys.
- `email` and `apiToken` with leading or trailing whitespace (space, tab, line break) are rejected as `invalid` naming the key, not trimmed: a pasted token with a trailing newline would otherwise fail later as a 401 (human decision after review).
- `docs/config.example.json` is the copy-and-edit template (human request after review). `ConfigExampleTests` loads it through the real loader, both as-is (classic token) and with the `cloudId` line uncommented (scoped token), so it cannot drift from the schema. Story 5.2 README should point to it.

## Spec Change Log

## Review Triage Log

| # | Source | Finding | Verdict | Evidence | Route |
|---|--------|---------|---------|----------|-------|
| 1 | verification-gap | Real `Build()`/`JiraCliConfig.Load()` never shown to use `UserPaths.ConfigFile("jira-cli")`; real-Build test only asserts non-null (+ blind: test reads real config, proves little) | medium | Pre-verified gap: wrong `AppName` or a cwd-relative path keeps all tests green. | patch |
| 2 | verification-gap | Windows real lookup not asserted to equal RoamingAppData | medium | Pre-verified gap: swapping the folder GUID (e.g. LocalAppData) keeps tests green. | patch |
| 3 | verification-gap, edge-case | Linux native lookup (struct layout, ERANGE retry) and Linux hostile-env test run only by hand; managed `SetEnvironmentVariable` does not reach libc `getenv` | medium | Pre-verified: no CI, Linux tests skip on Windows. Needs a routine Linux run, beyond this story. | defer |
| 4 | edge-case | Whitespace-only native path or passwd home makes `UserPathLookup.FromPath` throw out of `Build()` | low | `FromPath` uses `ThrowIfNullOrWhiteSpace` while native code checks `IsNullOrEmpty`; direct correction. | patch |
| 5 | blind | Blank or null value (`"site": ""`, `null`) reported as `has no "site"` / "Add it" | low | `ConfigMapper.Missing` is used for null and blank; message is wrong when the key exists. Wording fix. | patch |
| 6 | blind | Hints hard-code 20 and 100 instead of the `AccessSettings` constants | low | `ConfigMapper` hint strings; drift if defaults change. Direct correction. | patch |
| 7 | blind | Matrix "access denied" not tested; only lock and directory | low | `UnauthorizedAccessException` branch has no test; a Windows deny-ACL test closes it. | patch |
| 8 | blind, verification-gap | `sprint-status.yaml` `last_updated` moved backwards (16:00 → 15:02) | low | Workflow bookkeeping wrote local time; fixed at story close. | patch |
| 9 | blind, edge-case | Missing native library (musl/Alpine `libc`, Nano Server) throws `DllNotFoundException` out of `Build()` | maybe-false (medium if true) | Could not run on Alpine; settle by running `UserPaths.ConfigFile` in an Alpine container. | defer |
| 10 | blind | Spec `in-review` vs sprint `in-progress` disagree | false | Workflow syncs sprint status at story close; not a code defect. | reject |
| 11 | blind | Unknown key names appear in messages | low | Rejected in review (intent said name the key); human chose to mask unknown key names after the walkthrough (2026-11-14). | patch (human) |
| 12 | blind, edge-case | Control chars / `.` / apostrophe in key names give odd or null key paths | low | Rare keys; fix adds escaping logic; only line is reported then. | reject |
| 13 | blind | UTF-16 config gets generic invalid-JSON message; invalid UTF-8 untested | low | Still a correct `invalid` failure with path; probe shows invalid UTF-8 surfaces as `JsonException` (caught). Specific BOM detection adds a branch. | reject |
| 14 | blind, edge-case | No size/file-type limit; FIFO or huge file at config path hangs | low | Fixed user-owned path; unlikely; fix adds guards. | reject |
| 15 | blind, edge-case | Leading/trailing whitespace in `apiToken`/`email` accepted | low | Initially rejected as rare; human chose to fix it in this sprint after review (2026-11-14). | patch (human) |
| 16 | blind | Misspelled top-level key silently ignored | false | Intent: unknown top-level keys are ignored. | reject |
| 17 | blind, edge-case | Architecture guard misses `GetTempPath`, `CurrentDirectory`, P/Invoke `getenv` | low | No src uses them; fix widens the scanner. | reject |
| 18 | blind | "Loaded exactly once" unverified | false | `CliApp.cs:37` calls the factory once; `Build()` calls `Load()` once. | reject |
| 19 | blind | File-permission check missing | false | Intent: no `doctor` (1.7). | reject |
| 20 | blind | `ProjectKey` redundant second regex, no length cap; `DefaultDefaultPageSize` name | low | No user harm; cosmetic. | reject |
| 21 | edge-case | `EINTR` from `getpwuid_r` treated as permanent | low | Rare; fix adds a branch. | reject |
| 22 | edge-case | Bare UNC root `\` makes a drive-relative path | low | `SHGetKnownFolderPath` never returns that; adds a guard. | reject |
| 23 | edge-case | `appName` with invalid file-name chars | false | Only caller passes the constant `jira-cli`. | reject |
