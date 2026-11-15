# Epic 1 Context: Safe connection and setup check

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

An engineer places the config file and runs `jira-cli doctor` to confirm the config is complete, the credentials work (classic or scoped token), Jira is reachable, and each allowed project is accessible, with a fix hint for every failure. On the way, this epic builds the safety core every later command relies on: a five-project structure guarded by architecture tests, a fixed config location the environment cannot redirect, a GET-only transport that sends the token only to the Jira base URL, one JSON envelope per invocation with fixed error categories and exit codes, secret redaction, the work log, and `JiraGateway` with project scope checks. The order puts the real-Jira connection early (Stories 1.3/1.3b) so surprises in authentication or endpoint shape surface before the upper layers are built.

## Stories

- Story 1.1: Solution skeleton with architecture guards
- Story 1.2: Fixed config location and config loading
- Story 1.3: GET-only transport with contained credentials and a live check
- Story 1.3b: Live check tool against real Jira (split from 1.3)
- Story 1.4: JSON envelope, error categories, and exit codes
- Story 1.5: Secret redaction and the work log
- Story 1.6: `JiraGateway` with scope checks
- Story 1.7: `doctor` checks config, credentials, reachability, and project access

## Requirements & Constraints

- **Read-only by construction.** No write request is ever expressible, whatever the token allows. Safety is enforced by the tool, not token scope, and covered by automated tests on Windows and Linux. Tests never call real Jira; the engineer-run live-check tool is not a test.
- **Allowed projects** come only from `allowedProjects` in the config file. No option or environment variable can widen them or change the config path.
- **Credentials** (email + API token) live in the config; the agent may read it. Classic tokens (site URL) and scoped tokens (`cloudId` gateway) both work. Tokens expire (1 day to 1 year), so an expired token needs a clear error and hint. The token and its Basic header value never appear in stdout, stderr, or the log.
- **Output:** every invocation except `--help`/`--version` writes exactly one JSON envelope with `formatVersion`; no human-readable mode. `--help` documents every command and option.
- **Exit codes:** 0 none, 1 `unexpected`, 2 `usage`, 3 `config`, 4 `authentication`, 5 `projectNotAllowed`, 6 `notFound`, 7 `network`, 8 `timeout`, 9 `rateLimited`, 10 `localIo`.
- **HTTP to category:** 400 `usage`; 401/403 `authentication` (hint: invalid or expired token, missing scope, scoped tokens need `cloudId`); 404 `notFound`; 429 `rateLimited` with `retryAfterSeconds` from `Retry-After`; timeout `timeout`; connection failure or 5xx `network`; 3xx or other `unexpected`.
- **Timeouts:** JSON requests 30 s total. No automatic retries, no client-side throttling.
- **`doctor`** reports config completeness, credentials, reachability, each allowed project, and the endpoint in use, with a fix hint per failure. It runs even when the config is invalid.
- **Work log:** one JSON line per invocation (including parse errors, excluding `--help`/`--version`): timestamp, command, redacted arguments, tickets touched, origin, result category, duration. Never retrieved content. Local only; loss is acceptable.

## Technical Decisions

- **Stack:** .NET 10, System.CommandLine 2.0.14, M.E.DependencyInjection 10.0.14, xunit.v3 4.0.2 (Microsoft.Testing.Platform runner via `global.json`), NetArchTest.eNhancedEdition 1.4.5, Jira Cloud REST API v3. All JSON via `System.Text.Json` source generation.
- **Layers (hexagonal):** `Cli` (commands, `Program` wrapper, composition root, config schema, `Contracts/`, `ExceptionMapper`) → `Application` (thin use cases) → `Domain` (value objects, `ProjectAccessPolicy`, `AccessSettings`, ports, Domain exceptions, `IInvocationContext`). `Infrastructure` (Gateway, Requests) → Domain, Common. `Atlassian.Common` (transport, endpoint, config load, user paths, envelope, tables, redaction, work log) references nothing and holds no Jira type, URL, JQL, or field name. `Cli` uses `Infrastructure` only via its single `Add…()` method. Hand-built DI, one scope per invocation, constructor injection only.
- **Config:** fixed path. Windows `<RoamingAppData>\jira-cli\config.json` via `SHGetKnownFolderPath` (`Environment.GetFolderPath` banned); Linux `<passwd home>/.config/jira-cli/config.json` via `getpwuid_r`. Environment ignored; lookup failure is `config`, no fallback. Loaded once per invocation before any HTTP call and before a parse failure is reported or logged. Load failure is a value: commands raise `config` (exit 3), `doctor` reports it as a check. Errors name the key, never the value. `ProjectKey`/`IssueKey` value objects; no raw key strings past Cli.
- **Transport:** `IAtlassianTransport.GetAsync(RelativeRequest, CancellationToken)` only; `RelativeRequest` = path segments + query pairs + `ResponseKind` (timeout profile only). Transport percent-encodes, refuses `.`/`..`, hard-codes GET, raises non-2xx as `TransportException`. Only the transport uses `HttpClient` and credentials. `AtlassianEndpoint` alone picks the base URL (`https://<site>.atlassian.net/` or `https://api.atlassian.com/ex/<product>/<cloudId>/`); auth header only there. No redirects, TLS validation always on, default proxy.
- **Envelope and errors:** `EnvelopeWriter` (Common), called only by the `Program` wrapper, writes `{ formatVersion, ok, data?, error? }` (UTF-8, no BOM); `error` = `{ category, message, hint, retryAfterSeconds? }`. `data` with `error` only for partial results (`doctor`); first failure sets `error` and exit code. Parser: `ResponseFileTokenReplacer = null`, directives cleared, parse errors → `usage`. `SatelliteResourceLanguages=en`; stderr only for warnings. `ExceptionMapper` is the sole exception-to-category mapper; tables live in Common. Contracts in `Cli/Contracts`, camelCase, `data` always an object, lists under `items`, `data.notices` as `{ code, message, hint }`, standard fields present and `null` when empty. Renaming/removing a field, changing its type, or an exit code bumps `formatVersion`; adding does not. Golden-file tests pin output.
- **Redaction and log:** `ISecretRedactor` replaces exact token and Basic header matches at every sink, on strings before JSON encoding. `WorkLogWriter` (Common), only from the wrapper, appends to a UTC daily file with shared access: Linux `<passwd home>/.local/state/jira-cli/logs/`, Windows `<LocalAppData>\jira-cli\logs\`. Symlink/reparse in the chain from `jira-cli` down is refused. Log failure is a stderr warning only. Config-load failure logs only option names and an argument count. Dates ISO 8601 UTC with `Z` via `TimeProvider`.
- **Gateway:** `JiraGateway` is the only `JiraCli.*` class depending on the transport; `IJiraGateway` and `IJiraRequest<T>` are `internal`. One pure `…Request` per endpoint (`Scope`, `Build()`, `Parse(Stream)`), created with `new`. Closed `RequestScope` set checked before and after the call via `ProjectAccessPolicy`; failure raises `ProjectNotAllowedException` with no content leaving. Epic 1 adds only `Unscoped` (exactly `GetMyselfRequest`) and `ProjectScope` (non-empty, all allowed, never filled in). After-call scopes refuse Requests without observation; `GetProjectRequest` fails closed on a missing or different key. `TransportException` is translated to Domain exceptions; requested keys go to `IInvocationContext`. A reflection test requires every Request in a reviewed Request-to-scope table.

## Cross-Story Dependencies

- 1.1 provides the structure and architecture-test harness every story extends.
- 1.2's user-path resolution is reused by 1.5 (log folder) and Epic 4 (attachment folder). 1.2 registers the first real services and pins `ValidateOnBuild`/`ValidateScopes`.
- 1.3 depends on 1.2's `ConnectionSettings`. The `Download` response kind and its timeout profile are deferred to Story 4.2.
- 1.3b adds `tools/JiraCli.LiveCheck` (never packaged, debugger-runnable): loads the real config via 1.2's loader, sends `myself` and one `project/{key}` per allowed project with both token kinds, prints endpoint, statuses, and bodies, never the token. It must be done before 1.6 starts; findings amend AD-12.
- 1.4's wrapper, `EnvelopeWriter`, and `ExceptionMapper` are reused by 1.5 and 1.7. Ctrl+C behavior is decided and tested in 1.4. 1.5's redactor needs the token from 1.2's config load.
- 1.6 depends on 1.3's transport and `TransportException`. 1.7 depends on 1.2, 1.3, 1.4, and 1.6, and repeats the live check through `doctor` with both token kinds; differences from 1.3b's findings amend AD-12.
- The remaining scope kinds come later: `IssueScope` (2.3), `VerifiedIssueScope` (2.4), `ChildKeysScope` (3.2), `AttachmentRefScope` (4.2). README coverage is Epic 5; the Windows/Linux release gate is Story 5.4.
