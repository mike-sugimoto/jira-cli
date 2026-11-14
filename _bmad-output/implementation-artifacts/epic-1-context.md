# Epic 1 Context: Safe connection and setup check

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

An engineer places the config file and runs `jira-cli doctor` to confirm that the config is complete, the credentials work (classic or scoped token), Jira is reachable, and each allowed project is accessible, with a fix hint for each failure. Along the way this epic builds the safety core every later command depends on: the five-project structure guarded by architecture tests, one JSON envelope per invocation with fixed error categories and exit codes, a fixed config location that the environment cannot redirect, secret redaction, the work log, a GET-only transport, and `JiraGateway` with scope checks. Later epics add commands on top of this core, so its guarantees must hold by construction rather than by convention.

## Stories

- Story 1.1: Solution skeleton with architecture guards
- Story 1.2: Fixed config location and config loading
- Story 1.3: GET-only transport with contained credentials and a live check
- Story 1.4: JSON envelope, error categories, and exit codes
- Story 1.5: Secret redaction and the work log
- Story 1.6: `JiraGateway` with scope checks
- Story 1.7: `doctor` checks config, credentials, reachability, and project access

## Requirements & Constraints

- **Read-only, always.** jira-cli never sends a write request to Jira, whatever the token allows. Safety comes from the tool itself, not from token scope, and is covered by automated tests on Windows and Linux. Tests never call real Jira; the engineer-run live-check tool is not a test.
- **Allowed projects** come only from `allowedProjects` in the user config file. No option or environment variable can widen them or change the config path. Any read outside them is refused with `projectNotAllowed`.
- **Credentials** (email + API token) live in the config file; the agent may read the file. Both classic tokens (site URL) and scoped tokens (`cloudId` gateway) must work. Tokens can expire (1 day to 1 year), so an expired token needs a clear error and hint. The token and its Basic header value never appear in stdout, stderr, or the log.
- **Output:** every invocation except `--help`/`--version` writes exactly one JSON envelope with `formatVersion` to stdout. No human-readable output mode. `--help` documents every command and option.
- **Errors** state what went wrong and how to fix it, using fixed categories and exit codes: 0 none, 1 `unexpected`, 2 `usage`, 3 `config`, 4 `authentication`, 5 `projectNotAllowed`, 6 `notFound`, 7 `network`, 8 `timeout`, 9 `rateLimited`, 10 `localIo`.
- **HTTP-to-category:** 400 `usage`; 401/403 `authentication` (hint names invalid or expired token, missing scope, and that scoped tokens need `cloudId`); 404 `notFound`; 429 `rateLimited` with `retryAfterSeconds` when `Retry-After` is present; timeout `timeout`; connection failure or 5xx `network`; 3xx or any other status `unexpected`.
- **Timeouts and retries:** JSON requests time out at 30 s total. No automatic retries, no client-side throttling.
- **`doctor`** reports config completeness, credential validity, reachability, each allowed project's accessibility, and the endpoint in use, each failure with a fix hint. It must run even when the config is invalid.
- **Work log:** one JSON line per invocation (except `--help`/`--version`, but including parse errors): timestamp, command, redacted arguments, tickets touched, origin, result category, duration. Retrieved content is never logged. Logs stay local; losing them is acceptable.
- **Maintainability:** test-first; shared parts reusable by a future Confluence client stay in `Atlassian.Common`, free of Jira concepts.

## Technical Decisions

**Stack:** .NET 10, System.CommandLine 2.0.14, Microsoft.Extensions.DependencyInjection 10.0.14, xunit.v3 4.0.2, NetArchTest.eNhancedEdition 1.4.5, Jira Cloud REST API v3. `global.json` sets `test.runner = Microsoft.Testing.Platform` (required by xunit.v3 4.x on SDK 10). All JSON via `System.Text.Json` source generation.

**Project split (hexagonal):**
- `JiraCli.Cli`: commands, `Program` wrapper, composition root, config schema, `Contracts/`, `ExceptionMapper`.
- `JiraCli.Application`: one thin use case per command, orchestration only.
- `JiraCli.Domain`: entities, value objects, policies (`ProjectAccessPolicy`), `AccessSettings`, ports (`I…`), Domain exceptions, `IInvocationContext`.
- `JiraCli.Infrastructure`: `JiraGateway`, Requests, verified tokens, port implementations.
- `Atlassian.Common`: transport, `ConnectionSettings`, `AtlassianEndpoint`, config loading, user paths, envelope writer, category/exit-code and HTTP-status tables, redaction, work log writer.
- References: Cli → Application, Domain, Common, and Infrastructure (composition root only, via its `Add…()` method); Application → Domain; Infrastructure → Domain, Common. `Domain` and `Common` reference no solution project. `Common` holds no Jira type, URL, JQL, or field name; app name, product URL segment, and config schema are caller-supplied parameters.
- Each project exposes exactly one `Add…()` registration method. Hand-built DI container in `Cli`, one scope per invocation, constructor injection only, implementations `internal` where possible.
- Naming: `…Command`, `…Request`, `…UseCase`, ports `I…` in Domain.

**Output and errors:**
- `EnvelopeWriter` (Common), called only from the `Program` wrapper, writes `{ formatVersion, ok, data?, error? }` as UTF-8 without BOM to `Console.OpenStandardOutput()` with the default ASCII-escaping encoder. `error` is `{ category, message, hint, retryAfterSeconds? }`. Commands return data and never write output. `data` accompanies `error` only for partial results (`doctor`, later `attachments download`); the first failure sets `error` and the exit code.
- Parser: `ResponseFileTokenReplacer = null`, directives cleared, parse errors → `usage`. `SatelliteResourceLanguages=en`. stderr only for non-result warnings. Only `--help`/`--version` print plain text.
- `ExceptionMapper` (Cli) is the single exception-to-category mapper; unhandled → `unexpected` (1). Tables live in Common.
- Contracts in `JiraCli.Cli/Contracts`, mapped from Domain (never serialize Domain types). camelCase, `data` always an object, lists under `items`, notices as `data.notices: [{ code, message, hint }]`, standard fields always present (`null` when empty), `formatVersion` a string. Renaming/removing a field, changing its type, or changing an exit code bumps `formatVersion`; adding a field does not. Golden-file tests pin each command's output.
- Only `EnvelopeWriter` writes stdout and only the warning writer writes stderr (architecture test).

**Config (fixed location):**
- Windows: `<RoamingAppData>\jira-cli\config.json` via `SHGetKnownFolderPath` with an explicit process token; `Environment.GetFolderPath` is banned for these paths. Linux/WSL/Docker: `<passwd home>/.config/jira-cli/config.json`, home from `getpwuid_r`. All environment variables ignored; lookup failure is `config` with no fallback. The tool only reads the file and does no runtime writability check.
- Loaded once per invocation (except `--help`/`--version`) before any HTTP call, and before a parse failure is reported or logged, via source-generated `Load(path, JsonTypeInfo<T>)`.
- Schema: top level `site` (single DNS label), `email`, `apiToken`, optional `cloudId` (GUID) → `ConnectionSettings` (Common). Section `jira-cli`: `allowedProjects` (non-empty), `customFieldPassthrough` (`^customfield_[0-9]+$`, default empty), `defaultPageSize` (20), `maxPageSize` (100) → `AccessSettings` (Domain). Validation errors name the key, never the value.
- Load failure is a value: `doctor` reports it as a check; every other command raises `config` (exit 3).
- Keys: `ProjectKey` (`^[A-Z][A-Z0-9_]+$`) and `IssueKey` (`^[A-Z][A-Z0-9_]+-[1-9][0-9]*$`) value objects, upper-cased with ordinal rules; no raw key strings past the Cli layer.

**Credentials and transport:**
- Only the transport uses credentials; the composition root passes `ConnectionSettings` through unread. `AtlassianEndpoint` alone picks the base URL: `https://<site>.atlassian.net/` or `https://api.atlassian.com/ex/<product>/<cloudId>/` when `cloudId` is set. The `Authorization` header goes on every request and only to that base URL.
- `IAtlassianTransport` exposes only `GetAsync(RelativeRequest, CancellationToken)`. `RelativeRequest` = path segments + query name/value pairs + `ResponseKind` (`Json`/`Download`, which only selects the timeout profile); no raw URL, no method. Returns a disposable `TransportResponse` with the body stream; non-2xx raised as `TransportException` (carrying status/timeout and `Retry-After`). The transport percent-encodes, refuses `.`/`..` segments, hard-codes GET. Only the transport implementation uses `HttpClient`.
- No redirects followed (3xx → `unexpected`), TLS validation never disabled, default .NET proxy behavior.
- `ISecretRedactor` replaces exact matches of the token and its Basic header value at every stdout, stderr, and log sink, on string values before JSON encoding.

**Work log:**
- `WorkLogWriter` (Common), called only from the `Program` wrapper, appends to a UTC-dated daily file with shared access. Linux `<passwd home>/.local/state/jira-cli/logs/`, Windows `<LocalAppData>\jira-cli\logs\`, resolved like the config path. Dates ISO 8601 UTC with `Z`; use `TimeProvider`.
- Refuses a log file or folder chain from `jira-cli` down containing a symlink or reparse point. Any log failure → stderr warning; stdout and exit code unchanged. When config load failed, log only option names and an argument count. Commands never log.

**Gateway and scopes:**
- `JiraGateway` is the single outbound path and the only `JiraCli.*` class depending on `IAtlassianTransport`. `IJiraGateway` and `IJiraRequest<T>` are `internal` to Infrastructure and are not ports.
- Each endpoint is one pure `…Request` class implementing `IJiraRequest<T>` with `Scope`, `Build()`, `Parse(Stream)`; `T` is an Infrastructure DTO mapped to Domain by adapters. Requests are created with `new`, not DI. API version and URL fragments appear only in Requests.
- `RequestScope` is a closed set checked before and after each call with `ProjectAccessPolicy`; failure raises `ProjectNotAllowedException` and no content leaves the Gateway. Epic 1 introduces only `Unscoped` (allowed only for exactly `GetMyselfRequest`) and `ProjectScope(keys)` (non-empty, every key allowed; empty is refused, never filled in). Scopes with an after-call check refuse Requests that do not implement observation. `GetProjectRequest` post-checks that the returned project key matches; missing or different fails closed.
- Gateway translates `TransportException` to Domain exceptions and records requested keys through `IInvocationContext` for the work log.
- A reflection test requires every `IJiraRequest<T>` to appear in a reviewed Request-to-scope table.

**Required tests in this epic:** architecture tests (project references, Common Jira-free, `JiraGateway`/`HttpClient` exclusivity, console use with a self-test fixture proving a violation inside an async lambda is flagged, `Environment.GetFolderPath` ban, Domain types not serialized); transport suite with a fake `HttpMessageHandler` (GET only, encoding, `.`/`..` refusal, base-URL-only auth, no redirects, timeouts); hostile-env config path tests with a decoy config under `HOME`, `XDG_*`, `USERPROFILE`, `HOMEDRIVE`, `HOMEPATH`, `APPDATA`, `LOCALAPPDATA`; `@file` never expanded; secret containing `+` redacted; symlinked log chain refused on both platforms; Gateway tests for mismatched/missing observations; Request-to-scope reflection test; golden output for `doctor`.

## Cross-Story Dependencies

- 1.1 provides the structure and architecture-test harness every later story extends.
- 1.2's user-path resolution (passwd home / known folders, env ignored) is reused by 1.5 for the log location and by Epic 4 for the attachment folder. Its load failure is a value: 1.4 turns it into `config` (exit 3) and 1.7 reports it as the `doctor` config check. 1.2 registers the first real services, so it pins `ValidateOnBuild`/`ValidateScopes` (deferred from 1.1).
- 1.3 depends on 1.2's `ConnectionSettings`. It adds `tools/JiraCli.LiveCheck`, an engineer-run live check against the real site with both token kinds; its findings amend AD-12 before 1.6 starts.
- 1.4's `Program` wrapper, `EnvelopeWriter`, and `ExceptionMapper` are reused by 1.5 (the wrapper also writes the log) and 1.7. Ctrl+C behavior is decided and tested in 1.4. Config loads before parse errors are reported or logged (1.4/1.5 interaction).
- 1.5's redactor needs the token from 1.2's config load.
- 1.6 depends on 1.3's transport and `TransportException`; 1.7 depends on 1.2, 1.3, 1.4, and 1.6 (`GetMyselfRequest`, `GetProjectRequest`).
- The remaining scope kinds are added later by the first story that needs each: `IssueScope` (2.3), `VerifiedIssueScope` (2.4), `ChildKeysScope` (3.2), `AttachmentRefScope` (4.2). Adding a scope kind or `Unscoped` entry requires changing the architecture decision.
- 1.7 repeats the live check through `doctor` with a classic token (site URL) and a scoped read-only token (`cloudId` gateway); any difference from 1.3's findings is recorded and amends AD-12.
- README coverage of config location, settings, and token setup lands in Epic 5; the release gate running all suites on Windows and Linux is Story 5.4.
