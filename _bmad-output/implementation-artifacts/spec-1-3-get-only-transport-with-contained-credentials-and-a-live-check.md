---
title: 'Story 1.3: GET-only transport with contained credentials'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e2b663f4b1b56ea67a59238dde714ecf89a2e54d'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** jira-cli cannot talk to Jira yet. The HTTP layer must make a write request inexpressible and keep the token on the one base URL (AD-5, AD-12, AD-19).

**Approach:** Add `IAtlassianTransport` (GET only by type), `AtlassianEndpoint`, and `TransportException` to `Atlassian.Common`, proven by a fake-handler suite and architecture tests. The live check (`tools/JiraCli.LiveCheck`) was split off at the size gate (see `deferred-work.md`) and must land before Story 1.6.

## Boundaries & Constraints

**Always:** Test-first. AD-2: the product segment (`jira`) is caller-supplied; Common holds no Jira token. `Authorization: Basic base64(email:apiToken)` is set per request, only after the final URI is checked to sit under the base URL. Redirects are never followed; TLS validation and proxy stay .NET defaults. JSON requests: 30 s total including the body read, no retry. Caller cancellation propagates as `OperationCanceledException`, not as a timeout. No exception message or `ToString` contains the token or the header value.

**Never:** No DI registration of the transport, no `JiraGateway`, Requests, or scopes (1.6); no category or exit-code mapping, envelope, or Ctrl+C decision (1.4); no redaction or log (1.5); no `Download` kind or its timeout profile (4.2 adds both); no LiveCheck (split off). No test calls real Jira.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Classic | site `acme`, segments `rest`,`api`,`3`,`myself` | GET `https://acme.atlassian.net/rest/api/3/myself`, auth + `Accept: application/json` | N/A |
| Scoped | plus `cloudId` | GET `https://api.atlassian.com/ex/jira/<cloudId>/rest/api/3/myself` | N/A |
| Encoding | segment `a/b c`, query `jql`=`a=b&c` | `a%2Fb%20c`, `jql=a%3Db%26c`, sent exactly so | N/A |
| Dot / empty segment | `.`, `..`, `""` | no request sent | `ArgumentException` |
| Redirect | 302 with `Location` | handler called once | `TransportException` status 302 |
| Non-2xx | 401, 404, 500 | — | `TransportException` with status and body text (capped) |
| 429 | `Retry-After: 7` or HTTP-date | — | `TransportException` status 429, `RetryAfter` 7 s / date minus now |
| Timeout | no complete response in 30 s | — | `TransportException` kind timeout |
| Connection | `HttpRequestException` without status | — | `TransportException` kind connection, inner kept |

</frozen-after-approval>

## Code Map

- `src/Atlassian.Common/Config/ConnectionSettings.cs` -- input to `AtlassianEndpoint`; not a record, keep it that way.
- `src/Atlassian.Common/ServiceCollectionExtensions.cs` -- unchanged (no registration this story).
- `tests/JiraCli.ArchitectureTests/IlScanner.cs`, `CommonIsJiraFreeTests.cs`, `EnvironmentUseTests.cs` -- reuse Cecil scanning and the fixture self-test pattern; `SolutionLayout.Assemblies` lists the src assemblies to scan.

## Tasks & Acceptance

**Execution:**
- [x] `src/Atlassian.Common/Transport/` -- `IAtlassianTransport.GetAsync(RelativeRequest, CancellationToken)`; `RelativeRequest(IReadOnlyList<string> segments, IReadOnlyList<KeyValuePair<string,string>> query, ResponseKind kind)`; `ResponseKind { Json }`; `TransportResponse` (status, buffered body `Stream`, `IDisposable`); `TransportException` (`TransportFailureKind` `Status`/`Timeout`/`Connection`, `StatusCode?`, `RetryAfter?`, `ResponseBody?` capped at 8 KiB, never in `Message`); `AtlassianEndpoint.For(ConnectionSettings, product)` → base `Uri`; internal `HttpAtlassianTransport` with a public factory taking `ConnectionSettings`, product, and optional `TimeProvider`, plus an internal ctor taking an `HttpMessageHandler` and timeout for tests; production handler `SocketsHttpHandler { AllowAutoRedirect = false }` -- AD-5/12/19
- [x] `tests/Atlassian.Common.Tests/Transport/` -- matrix rows with a recording fake handler; auth header present on every request; production handler has redirects off, no certificate callback, default proxy; secret-shaped token never in any exception `Message`/`ToString`
- [x] `tests/JiraCli.ArchitectureTests/TransportSurfaceTests.cs` + fixture -- `IAtlassianTransport` has exactly one method with that signature; `RelativeRequest` exposes no `string`/`Uri` URL member and no method/verb; across all src assemblies only `HttpAtlassianTransport` depends on `System.Net.Http.HttpClient`; no src code references `ServerCertificateCustomValidationCallback`, `RemoteCertificateValidationCallback`, or `DangerousAcceptAnyServerCertificateValidator`; self-test fixture proves both scans flag a violation inside a lambda

**Acceptance Criteria:**
- Given `dotnet build` and `dotnet test` at the repo root, then 0 warnings and all tests pass, existing ones included (`CommonIsJiraFreeTests` too).
- Given `dotnet test`, then no request reaches a real host.

## Design Notes

- Build the URI by concatenating base + escaped segments (`Uri.EscapeDataString`) + escaped query, then compare scheme, host, port, and path prefix with the base before adding the header; assert `RequestUri.AbsoluteUri` in tests so .NET normalization cannot slip `%2F` or dot segments past.
- Timeout: `HttpClient.Timeout = Infinite`; a linked CTS with `CancelAfter(30 s)` covers send + `LoadIntoBufferAsync`. If it fired and the caller token did not, raise `Timeout`.
- Non-2xx bodies stay on the exception so the live check and `doctor` can show Jira's error text; the transport never puts them in `Message`.

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings, 0 errors
- `dotnet test` -- expected: all pass

## Implementation Notes

- Public factory is `AtlassianTransport.Create(settings, product, timeProvider?)` (a public static class beside the internal `HttpAtlassianTransport`). The `HttpClient` lives as long as the transport; `IAtlassianTransport` is not `IDisposable` so the interface keeps exactly one member. Fine for a one-invocation CLI; revisit if 1.6 registers it as a scoped service.
- `RelativeRequest` validates in its constructor (null/empty/`.`/`..` segments, empty query names, null values, undefined kind), so no transport is reached. The transport additionally checks the built URI sits under the base (scheme, host, port, no user info/fragment, path prefix) before attaching `Authorization`.
- `TransportResponse` has a public `(HttpStatusCode, Stream)` ctor and `TransportException` a public ctor so 1.6 can fake the transport from Infrastructure tests. `ResponseBody` is capped at 8 KiB of UTF-8 (whole characters). `RetryAfter` is read from any non-2xx response, clamped at zero.
- Caller cancellation always surfaces as a new `OperationCanceledException` carrying the caller's token (not the internal linked one), even when the handler reports it as a non-cancellation exception.
- `HttpAtlassianTransport.Handler` and `RequestTimeout` are internal, for the factory-wiring test only. `Create` validates the endpoint before building the handler, so a bad product leaks nothing.
- The network guard bans `HttpClient`, `HttpMessageInvoker`, `WebRequest`, `HttpWebRequest`, `WebClient`, `TcpClient`, `UdpClient`, and `Socket`, including inside generic arguments and base types. Only `HttpAtlassianTransport` may use them. A timeout reported as a status-less `HttpRequestException` can only arise in the body read: `HttpClient.SendAsync` converts handler failures to `TaskCanceledException` once its token fires.
- Error bodies are decoded as UTF-8 regardless of the response charset.
- URI building is behind internal `IRequestUriBuilder`; `RequestUriBuilder.Build` escapes segments and query and then calls `EnsureUnderBase`, which refuses any URI not strictly under the base URL, so the transport attaches credentials to whatever the builder returns. The builder is injectable only through the internal `HttpAtlassianTransport` ctor (tests); production always uses `RequestUriBuilder`, and the product is validated even when a builder is injected. `RequestUriBuilderTests` pins built URIs and calls `EnsureUnderBase` directly with outside-base URIs, since escaping makes the check unreachable through `Build` (human request after review, 2026-11-15; resolves triage #4).

## Spec Change Log

## Review Triage Log

| # | Source | Finding | Verdict | Evidence | Route |
|---|--------|---------|---------|----------|-------|
| 1 | verification-gap | `AtlassianTransport.Create` wiring (redirect-off handler, 30 s timeout) untested | medium | Pre-verified: passing `new SocketsHttpHandler()` or 5 min to the ctor keeps all tests green. | patch |
| 2 | verification-gap | `Translate` branches (caller cancel + non-OCE; timeout + `HttpRequestException`) unreached | medium | Pre-verified: deleting either branch keeps tests green. | patch |
| 3 | verification-gap, blind | Body cap tested only with ASCII; whole-character logic and pre-slice never exercised | low | Both cap tests use `'x'`/`'y'`; a char-count cap would pass. Pre-slice to Max+4 bytes cannot leak U+FFFD past Cap (an incomplete tail starts at byte ≥ 8193). Tests only. | patch |
| 4 | verification-gap | Under-base guard in `BuildUri` unreachable; deleting it keeps tests green | low | Escaping neutralises every input today; testing needs a seam. Revisit when a second URI path (4.2 `Download`) appears. | defer |
| 5 | blind, edge-case | No response size cap; up to ~2 GB buffered | low | Real, but Jira is the only host and responses are bounded in practice; a limit is a new policy decision with its own error path. | reject |
| 6 | blind, edge-case | `ReadAsByteArrayAsync` copies the buffered body again | low | Real but JSON bodies are small; avoiding the copy changes response ownership (the response is disposed in `GetAsync`). | reject |
| 7 | blind, edge-case | Raw `IOException`/`AuthenticationException`/status-bearing `HttpRequestException` escape | false | `SocketsHttpHandler` wraps TLS and socket failures in `HttpRequestException` (no status); `LoadIntoBufferAsync` wraps copy errors the same way; nothing calls `EnsureSuccessStatusCode`. | reject |
| 8 | blind, edge-case | `UriFormatException` for a URI over ~65 KB; interface doc says "any other outcome raises TransportException" | low | Real only for absurd queries; doc fix is direct: say invalid requests raise `ArgumentException`-family exceptions. | patch (doc) |
| 9 | blind | Caller cancellation OCE carries the linked token, not the caller's | medium | `Translate` returns null for an OCE, so `HttpClient`'s `TaskCanceledException` (linked token) propagates; 1.4's Ctrl+C check `ex.CancellationToken == ct` would fail. Direct correction. | patch |
| 10 | blind | Body-read cancellation test cancels before the response returns, may test the send phase | low | Handler cancels the caller before returning; the test cannot prove the body-read path. Move the cancel into `StallingContent`. | patch |
| 11 | blind, edge-case | `HttpClient` guard misses `HttpMessageInvoker` (arbitrary verbs), generic wrappers, `WebRequest`/`WebClient`/sockets | medium | `MentionsHttpClient` uses exact `FullName`; `HttpMessageInvoker.SendAsync` can POST and is not flagged. Widening to a banned-type list (generic args included) is a list change. | patch |
| 12 | blind | Cert-bypass list misses `ServicePointManager` callback, `CertificateChainPolicy`, revocation `NoCheck`; no proxy guard | low | `ServicePointManager` does not affect `HttpClient` on .NET Core; the rest is unlikely and widens the scanner. | reject |
| 13 | blind | URL/verb scan skips constructor parameters; `ResponseKind` names unchecked | low | Unlikely; adds a scan. | reject |
| 14 | blind | `ResponseKind` doc says it selects timeout and Accept header, but `Kind` is never read | low | Only `Json` exists; doc overstates today. Direct doc correction: say 4.2 must switch on it. | patch (doc) |
| 15 | blind | Redirect `Location` dropped | low | Diagnostic only; keeping it adds public surface. | reject |
| 16 | blind | Deferred LiveCheck tracked only in `deferred-work.md`; story can close with the epic AC open | medium | Real process risk from the split; not code. Raised to the human at presentation. | reject (raised to human) |
| 17 | blind | Spec `in-review` vs sprint `in-progress` | false | Workflow syncs sprint status at story close (same as 1.2 #10). | reject |
| 18 | blind | Timeout tests use real 100 ms timers | low | Flake risk only under heavy load; `FakeTimeProvider` adds a package. | reject |
| 19 | blind | No tests for 204, read-only body position, `Retry-After` on 503 | low | No defect named; behavior is straightforward. | reject |
| 20 | edge-case | OCE while neither caller nor timeout cancelled escapes raw | low | No known `HttpClient` path does this with an infinite client timeout; adds a branch. | reject |
| 21 | edge-case | Connection failure racing the timeout reported as Timeout | low | Requires a failure within the same instant as the 30 s deadline; adds a branch. | reject |
| 22 | edge-case | `SocketsHttpHandler` leaked when `AtlassianEndpoint.For` throws in the ctor | low | Real for a bad product argument; direct correction: resolve the endpoint before creating the handler. | patch |
| 23 | edge-case | Email containing `:` splits Basic credentials | low | Not a valid Atlassian account email in practice; adds a guard. | reject |
| 24 | edge-case | `TransportResponse` ctor accepts non-2xx | low | Only fakes construct it; adds a guard. | reject |
| 25 | edge-case | `TransportException` allows Status with 2xx, or Timeout with body/Retry-After | low | Only fakes construct it; adds guards. | reject |
