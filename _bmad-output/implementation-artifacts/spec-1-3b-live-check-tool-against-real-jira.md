---
title: 'Story 1.3b: Live check tool against real Jira'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: '5c5a0d09635e1c06f225efd906949f75a62369a0'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Story 1.3 transport is proven only against fake handlers. Before Story 1.6 builds the Gateway on it, the engineer must see it authenticate against the real department Jira with both token kinds, and any surprise must amend AD-12.

**Approach:** Add `tools/JiraCli.LiveCheck`, a never-packaged console app with no arguments that loads the real config through Story 1.2's `JiraCliConfig.Load()`, sends `myself` and one `project/{key}` per allowed project through `AtlassianTransport.Create(..., "jira")`, and prints the endpoint, each status, and each body. The engineer switches between classic and scoped tokens by editing the config (`apiToken`, `cloudId`) between runs.

## Boundaries & Constraints

**Always:** Test-first for the runner logic. Output goes to a `TextWriter` passed in, so tests capture it. The tool never reads `ConnectionSettings.ApiToken` or `Email`, and never prints an `Authorization` value; it prints only the endpoint URI, request path, status, and body (or `TransportException` kind, status, `RetryAfter`, and `ResponseBody`). Requests run in order, stop at none: every allowed project is tried even after a failure. Exit code 0 when every request is 2xx, 1 otherwise, 2 on config failure.

**Never:** No new product code in `src` beyond two `InternalsVisibleTo` lines in `JiraCli.Cli.csproj` (`JiraCli.LiveCheck` and `JiraCli.LiveCheck.Tests`; human decision 2026-11-15); no DI container, envelope, redaction, log, Gateway, or Requests (1.4 to 1.6). No retry. No command-line arguments or environment variables. `dotnet test` never reaches a real host. Not packaged (`IsPackable=false`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Classic | config without `cloudId`, projects `ABC`,`XYZ` | prints endpoint `https://<site>.atlassian.net/`, then `GET rest/api/3/myself`, `GET rest/api/3/project/ABC`, `.../XYZ`, each with status and body | exit 0 |
| Scoped | config with `cloudId` | endpoint `https://api.atlassian.com/ex/jira/<cloudId>/`, same requests | exit 0 |
| Non-2xx | 401 on `myself`, 404 on `XYZ` | prints kind `Status`, status code, `ResponseBody`; continues with remaining requests | exit 1 |
| Timeout / connection | `TransportException` kind `Timeout` or `Connection` | prints the kind (and inner exception type for connection) | exit 1 |
| Config failure | `ConfigLoadResult.Failed` | prints the failure `Message` and `Hint`; sends nothing | exit 2 |
| Secret | token `ATATT-secret...` in settings | no output line contains the token or its Base64 Basic value | N/A |

</frozen-after-approval>

## Code Map

- `src/JiraCli.Cli/Config/JiraCliConfig.cs` -- `internal static Load()`; reuse as-is. Add `<InternalsVisibleTo Include="JiraCli.LiveCheck" />` and `<InternalsVisibleTo Include="JiraCli.LiveCheck.Tests" />` to `src/JiraCli.Cli/JiraCli.Cli.csproj`.
- `src/JiraCli.Cli/Config/ConfigLoadResult.cs`, `ConfigLoadFailure.cs` -- `Connection`, `Access.AllowedProjects` (`ProjectKey.Value`), `Failure.Message`/`Hint`.
- `src/Atlassian.Common/Transport/AtlassianTransport.cs`, `AtlassianEndpoint.cs`, `IAtlassianTransport.cs`, `RelativeRequest.cs`, `TransportException.cs`, `TransportResponse.cs` -- consume only; do not change.
- `tests/JiraCli.ArchitectureTests/OneTypePerFileTests.cs` -- already scans `tools/`; keep one type per file. `ProjectReferenceTests` and `ConsoleUseTests` scan only `src`, so the tool is outside AD-1.
- `jira-cli.slnx` -- add a `/tools/` folder with the tool and the new test project under `/tests/`.

## Tasks & Acceptance

**Execution:**
- [x] `tools/JiraCli.LiveCheck/JiraCli.LiveCheck.csproj` -- Exe, `IsPackable=false`, references `src/JiraCli.Cli` and `src/Atlassian.Common`; `InternalsVisibleTo JiraCli.LiveCheck.Tests`.
- [x] `tools/JiraCli.LiveCheck/LiveCheckRunner.cs` -- `internal` `RunAsync(ConfigLoadResult, Func<ConnectionSettings, IAtlassianTransport>, TextWriter, CancellationToken) → int`; prints per the matrix.
- [x] `tools/JiraCli.LiveCheck/Program.cs` -- wires `JiraCliConfig.Load()`, `AtlassianTransport.Create(s, "jira")`, `Console.Out`; returns the runner's exit code.
- [x] `tests/JiraCli.LiveCheck.Tests/` -- matrix rows with a recording fake `IAtlassianTransport`; asserts exact request segments (`rest`,`api`,`3`,`myself` and `...,project,<KEY>`), endpoint line, continue-after-failure, exit codes, and secret absence.
- [x] `.vscode/launch.json`, `.vscode/tasks.json` -- one `LiveCheck` launch config with a build pre-launch task, so F5 runs it with no extra setup.
- [x] `docs/live-check.md` -- how to run both modes, what to look for, and that differences from AD-12 must be recorded before Story 1.6.
- [x] `jira-cli.slnx` -- add both projects.

**Acceptance Criteria:**
- Given `dotnet build` and `dotnet test` at the repo root, then 0 warnings and all tests pass, existing ones included.
- Given the engineer's real config, when the engineer runs LiveCheck under F5 once with a classic token and once with a scoped read-only token and `cloudId`, then every request is 2xx, or each difference from AD-12 is recorded in `ARCHITECTURE-SPINE.md` AD-12 and in this spec's Implementation Notes before Story 1.6 starts.

## Design Notes

The runner takes a transport factory instead of a transport so the config-failure path is testable without constructing one, and so tests can assert the factory is never called on config failure. Bodies are printed as received (UTF-8 text); no JSON pretty-printing, so what Jira sent is what the engineer sees.

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings, 0 errors
- `dotnet test` -- expected: all pass

**Manual checks:**
- Engineer runs LiveCheck under F5 with both token kinds and confirms no token appears in the console.

## Implementation Notes

- `src/JiraCli.Cli/JiraCli.Cli.csproj` gets two `InternalsVisibleTo` lines, not one: `JiraCli.LiveCheck` (for `JiraCliConfig.Load()`) and `JiraCli.LiveCheck.Tests` (the tests build `ConfigLoadResult`/`ConfigLoadFailure`, which are `internal` in Cli). No other `src` change.
- Output format (one block per request, blank line between): `GET <segments joined by />`, then `Status: <code> (<name>)` and `Body:` + body as received; on `TransportException`: `Failure: <Kind>`, and for `Status` also `Status:`, `Retry-After: <n> s|(none)`, `Body:`; for `Timeout`/`Connection` one `Inner exception: <type>` line per exception in the inner chain, plus `(HttpRequestError: <value>)` for an `HttpRequestException` (types and enum values only, no messages); `Retry-After` is whole seconds rounded up. Empty or missing body prints `Body: (none)`. Ends with `Summary: <n> of <m> requests returned 2xx.`
- The endpoint line comes from `AtlassianEndpoint.For(connection, "jira")`, which reads only `Site` and `CloudId`. Caller cancellation (`OperationCanceledException`) and argument errors propagate; only `TransportException` is caught per request.
- Live runs against the real Jira (classic and scoped) are still to be done by the engineer; record their results or AD-12 differences here.

## Spec Change Log

## Review Triage Log

| # | Source | Finding | Verdict | Evidence | Route |
|---|--------|---------|---------|----------|-------|
| 1 | blind, edge-case | `docs/live-check.md` says the email is never printed, but the `myself` body (`emailAddress`) is printed verbatim | low | Real: Jira's `myself` returns `emailAddress`; the doc promise is false and output may be pasted into committed docs. Direct doc correction. | patch |
| 2 | blind | Connection/timeout failures print only the outermost inner type; DNS, TLS, proxy, refused cannot be told apart | medium | Real: `HttpRequestException` wraps `SocketException`/`AuthenticationException`; a first corporate-network run is the tool's main purpose. Printing the type chain and `HttpRequestError` adds no secrets. | patch |
| 3 | edge-case | `Retry-After` from an HTTP-date prints fractional seconds | low | Real: transport computes date minus now, so `TotalSeconds` is fractional. Direct format correction (`Math.Ceiling`). | patch |
| 4 | blind | Timeout test proves continuation through blank-line layout (`LinesAfter(..., 5)[4]`) | low | Real but cosmetic; direct test rewrite. | patch |
| 5 | verification-gap | Printed endpoint is computed separately from the transport's base URL; `Program.cs` wiring untested | low | Both use `LiveCheckRunner.Product` and the same `ConnectionSettings`; a mismatch would show as failures in the manual run. Fix restructures the factory. | reject |
| 6 | verification-gap, blind | Non-2xx `TransportResponse` branch and `Status: (none)` fallback unreachable/untested | low | Transport returns a response only for 2xx and `TransportException(Status)` requires a status; defensive code only, no harm. | reject |
| 7 | verification-gap, blind | "Never reads `ApiToken`/`Email`" and `IsPackable=false` have no architecture guard; `tools/` not scanned | low | Current code reads neither; dev-only tool; a new IL scan over `tools/` adds a test surface. | reject |
| 8 | blind | `epic-1-context.md` modified but excluded from the reviewed diff | false | Deliberately excluded: it is the workflow's regenerated epic context (planning artifacts were newer), not story code. | reject |
| 9 | blind | Spec `in-review` vs sprint `in-progress`; live-run AC open with no visible task | false | Sprint status syncs at story close (as in 1.2/1.3). The open live-run AC is raised to the human at presentation. | reject (raised to human) |
| 10 | blind, edge-case | No top-level error boundary; endpoint/factory/config exceptions crash with a runtime exit code; Ctrl+C not wired | false | `JiraCliConfig.Load` never throws for a bad config; `ConnectionSettings` is validated at construction and `Product` is a valid constant, so `AtlassianEndpoint.For`/`Create` cannot throw here. Ctrl+C terminating a dev tool is acceptable. | reject |
| 11 | edge-case | Non-`TransportException` from `GetAsync` aborts the loop, skipping remaining projects | false | Story 1.3 triage #7: the transport translates every handler/body failure to `TransportException`; only caller cancellation and argument errors propagate, as Implementation Notes state. | reject |
| 12 | blind | Zero allowed projects cannot be expressed in tests | false | `ConfigMapper` rejects an empty `allowedProjects`, so the runner never sees it. | reject |
| 13 | blind | 204 / non-200 2xx and cancellation propagation untested | low | Neither endpoint returns 204; cancellation behavior is the transport's (tested in 1.3). | reject |
| 14 | blind | `launch.json` hard-codes `bin/Debug/net10.0`; `.vscode/` not partially ignored | low | Standard C# launch template; breaks only on a TFM change, which would be visible at F5. | reject |
| 15 | blind, edge-case | Bodies printed raw: no size cap, control characters reach the terminal | low | Bodies come only from the configured Jira host; "as received" is the design intent. Adds sanitizing branches. | reject |
| 16 | edge-case | Error body silently capped at 8 KiB by the transport | low | Jira error bodies are small; a truncation note adds a branch. | reject |
| 17 | implementation report | Two `InternalsVisibleTo` lines in `JiraCli.Cli.csproj` (tool and its tests) vs frozen "the `InternalsVisibleTo` line" | maybe-false | Tests must build internal `ConfigLoadResult`, so the spec's own tasks require the second line; whether "the line" meant exactly one is the human's call. Human accepted two lines and amended the frozen wording (2026-11-15). | resolved by human |
