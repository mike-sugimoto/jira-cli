---
status: final
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-jira-cli-2026-11-01/prd.md
  - _bmad-output/planning-artifacts/prds/prd-jira-cli-2026-11-01/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-jira-cli-2026-11-01/ARCHITECTURE-SPINE.md
  - _bmad-output/specs/spec-jira-cli/SPEC.md
  - _bmad-output/specs/spec-jira-cli/glossary.md
  - _bmad-output/specs/spec-jira-cli/agent-loop.md
  - _bmad-output/specs/spec-jira-cli/setup-reference.md
---

# jira-cli - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for jira-cli, decomposing the requirements from the PRD, the SPEC, and the Architecture spine into implementable stories. Where the PRD and the SPEC or spine differ, the SPEC and spine win (they are the canonical contract).

## Requirements Inventory

### Functional Requirements

FR-1: List tickets in the projects the caller names with `list --project <key>...` (required, one or more). The allowed projects themselves come only from `allowedProjects` in the user configuration file, which the engineer maintains (missing or empty there is a `config` error, exit 3). Every `--project` key must be in `allowedProjects`. A request with no `--project`, or with any key not in `allowedProjects`, is refused with `projectNotAllowed` (exit 5) before any HTTP call, and the hint lists the allowed projects and states whether the option was missing. Filters: assignee (including "me"), status, issue type, priority, sprint, labels, in any combination. With no status filter, only unresolved tickets (`statusCategory != Done`). Ordered by most recently updated first. (PRD's no-project default overridden by AD-9.)
FR-2: Return a limited number of results with paging. Default page size from config `defaultPageSize` (initial 20); a `--limit` above `maxPageSize` (initial 100) is a `usage` error (exit 2). Paging uses Jira's opaque cursor (`data.nextPageToken` / `--page-token`), with no total.
FR-3: Each list entry shows key, issue type, summary, status, priority, assignee, last updated.
FR-4: `get <key>` retrieves one ticket of any type with summary, description, issue type, status, priority, labels, components, fix versions, reporter, assignee, created and updated dates, and all comments oldest first. Output carries no relations.
FR-5: Rich text (description, comments) is rendered as Markdown that keeps headings, lists, code blocks, tables, links, and mentions.
FR-6: Populated custom fields appear under their display names as an ordered `[{ name, value }]`, rendered by schema type. Unsupported types are omitted unless the field is listed in `customFieldPassthrough`, in which case they are output as raw JSON.
FR-7: `related <key>` shows, for any ticket type, its parent or epic, its children (subtasks and tickets under an epic), and its linked tickets with link type, one entry per key per relationship. Relations outside the allowed projects appear key-only with `accessible: false`; each disallowed child project adds one notice asking the engineer to add it to `allowedProjects`; the command still succeeds (exit 0).
FR-8: Related tickets are retrieved on request, one level at a time: `related <origin> --key <k>` returns `k`'s full content only when `k` is allowed (else exit 5) and related to the origin (else exit 6).
FR-9: `attachments list` shows each attachment's file name, size, type, author, and date.
FR-10: `attachments download` by `--name`, by `--id` matched against the ticket, or all, saves into the managed per-ticket folder, replaces existing files, and reports each file's absolute path as `downloaded` or `failed`. An ambiguous name is a `usage` error listing candidates. In a download of all with duplicate names, only the newest is saved. No option writes anywhere else.
FR-11: `history <key>` returns every change across all pages: who, which field, from, to, when. From and to values appear only for allow-listed system fields and displayable custom fields; relation fields show keys only; every other field shows name and timestamp only.
FR-12: Access is limited to the allowed projects in the user configuration file, read only from a fixed user-level location outside any workspace. The README documents the location, every setting, and how to protect the file.
FR-13: Any request for a ticket or list outside the allowed projects is refused with a clear error. Related tickets in other projects appear by key only; their content is never retrieved.
FR-14: No command-line option or environment variable can widen the allowed projects or change the configuration file path.
FR-15: jira-cli never sends a write request to Jira, whatever the credentials allow.
FR-16: Authenticate with account email plus API token; both classic tokens (site URL) and scoped tokens (`cloudId` gateway) work. The README recommends a scoped, read-only token.
FR-17: Credentials are kept in the user configuration file; the agent may read it.
FR-18: Credentials never appear in command output, error messages, or work logs.
FR-19: Every invocation writes one structured JSON envelope with `formatVersion` to stdout. Fields are not renamed or removed without a version change. No human-readable format.
FR-20: Errors state what went wrong and how to fix it, use distinct categories (authentication, project not allowed, not found, network, timeout, rate limit, plus usage, config, localIo, unexpected), and return the fixed exit codes 0 to 10.
FR-21: Built-in `--help` documents every command and option.
FR-22: A usage guide for Claude Code ships in the package and teaches the agent how and when to use the tool.
FR-23: `doctor` reports config completeness, whether credentials work (including an expired token), Jira reachability, each allowed project's accessibility, and the endpoint in use, with a fix hint per failure. It runs even when the config is invalid.
FR-24: Every invocation except `--help` and `--version` is logged as one JSON line: timestamp, command, redacted arguments, tickets touched, result category, duration. Retrieved content is never logged.
FR-25: Tickets retrieved through `related <origin> --key <k>` are logged with the originating key. Direct reads by key are logged with no origin.
FR-26: Logs stay on the local machine; losing them when a container is recreated is acceptable.

### NonFunctional Requirements

NFR-1: Responsiveness — retrieving a ticket, list, or attachment should feel comparable to the browser. No hard target.
NFR-2: Timeout — each JSON request times out after 30 seconds total (`timeout` error). Attachment downloads time out after 30 seconds waiting for headers or 30 seconds with no body progress, with no total limit.
NFR-3: Platforms — Windows and Linux, including WSL and Docker, on x64 and arm64.
NFR-4: Safety by construction — read-only behavior and the allowed-projects restriction are enforced by the tool itself, independent of token scope, and covered by automated tests on Windows and Linux.
NFR-5: Rate limits — stateless, no client-side throttling, no automatic retries. A rate-limit rejection is `rateLimited` (exit 9) with `retryAfterSeconds` when Jira provides it.
NFR-6: Maintainability — any engineer can add a feature; specs maintained with BMad next to the code; test-first; parts shared with a future Confluence client stay in `Atlassian.Common`, free of Jira concepts.
NFR-7: Onboarding — the README plus `doctor` are sufficient to set up; no setup-time target.

### Additional Requirements

**Starter / project setup (affects Epic 1 Story 1)** — no starter template; use the Structural Seed:
- Solution with `src/JiraCli.Cli`, `src/JiraCli.Application`, `src/JiraCli.Domain`, `src/JiraCli.Infrastructure`, `src/Atlassian.Common`; one test project per src project plus `tests/JiraCli.ArchitectureTests`; `docs/`.
- `global.json` with `test.runner = Microsoft.Testing.Platform` (required by xunit.v3 4.x on SDK 10).
- Stack: .NET 10, System.CommandLine 2.0.14, Microsoft.Extensions.DependencyInjection 10.0.14, xunit.v3 4.0.2, NetArchTest.eNhancedEdition 1.4.5, Jira Cloud REST API v3.
- Project references follow AD-1 only; each project exposes one `Add…()` registration method; hand-built DI container in `Cli`, one scope per invocation, constructor injection only.

**Structure and safety (AD-1 to AD-10, AD-20)**
- `Domain` and `Atlassian.Common` reference no other solution project; ports live in `Domain`; `Atlassian.Common` contains no Jira type, URL, JQL, or field name (AD-1, AD-2).
- `JiraGateway` is the single outbound path and the only `JiraCli.*` class depending on `IAtlassianTransport`; only the transport uses `HttpClient` (AD-4).
- `IAtlassianTransport` exposes only `GetAsync(RelativeRequest, …)`; `RelativeRequest` has path segments, query pairs, and `ResponseKind` (`Json`/`Download`); transport percent-encodes, refuses `.`/`..` segments, hard-codes GET, raises non-2xx as `TransportException` (AD-5).
- One pure `…Request` class per endpoint implementing `IJiraRequest<T>` with `Scope`, `Build()`, `Parse(Stream)`; issue-returning Requests implement observation (AD-6).
- Closed `RequestScope` set (`IssueScope`, `ProjectScope`, `VerifiedIssueScope`, `AttachmentRefScope`, `ChildKeysScope`, `Unscoped`) checked before and after each call via pure `ProjectAccessPolicy`; post-check fails closed (AD-7).
- Sub-resources (comments, changelog, attachment content) require a `VerifiedIssue` or `AttachmentRef` minted by this Gateway instance; attachment bytes fetched by id with `redirect=false`, never following the metadata `content` URL (AD-8).
- `TicketQuery` (Domain) with typed filters; `JqlBuilder` (Infrastructure) is the only JQL producer, escapes every value, always ANDs the project restriction; raw JQL never accepted (AD-9).
- `RelationSet` built only from `parent`, `subtasks`, `issuelinks`, and child keys from `GetChildKeysRequest` (`parent = <key>`, `fields=project`, paged to completion); `RelatedIssueProjection` reduces other-project items to key-only (AD-10).
- Required test suites: architecture tests (AD-1, AD-2, AD-4, console use, with self-test fixture); transport suite with fake `HttpMessageHandler`; reflection test of a reviewed Request-to-scope table; Gateway tests (moved tickets, missing observations, forged tokens); planted other-project data tests; hostile JQL/key/page-token values; `@file` never expanded; hostile env config path tests; symlink/reparse tests; secret with `+` redacted; golden output tests. Tests never call real Jira. Release gate runs all suites on Windows and Linux (AD-20).

**Config, credentials, paths (AD-11, AD-12)**
- Config path fixed: Windows `<RoamingAppData>\jira-cli\config.json` via `SHGetKnownFolderPath` (no `Environment.GetFolderPath`); Linux `<passwd home>/.config/jira-cli/config.json` via `getpwuid_r`; all environment variables ignored; lookup failure is `config`, no fallback.
- Config loaded once per invocation (except `--help`/`--version`) before any HTTP call, via source-generated `Load(path, JsonTypeInfo<T>)`; `site` a single DNS label, `cloudId` a GUID; mapped to `ConnectionSettings` (Common) and `AccessSettings` (Domain); load failure is a value (`doctor` reports, others raise `config`).
- `AtlassianEndpoint` alone chooses the base URL (site or `api.atlassian.com/ex/jira/<cloudId>/`); auth header only to that base URL; no redirects followed (3xx = `unexpected`); TLS validation never disabled; default .NET proxy behavior.
- `ISecretRedactor` replaces token and Basic header value at every stdout, stderr, and log sink; config errors name the key, never the value.

**Output and errors (AD-13 to AD-16)**
- `EnvelopeWriter` (Common), called only from the `Program` wrapper, writes one UTF-8 (no BOM) JSON document `{ formatVersion, ok, data?, error? }`; `error` is `{ category, message, hint, retryAfterSeconds? }`; parser `ResponseFileTokenReplacer = null`, directives cleared, parse errors → `usage`; `SatelliteResourceLanguages=en`; stderr only for warnings.
- `ExceptionMapper` (Cli) is the only exception-to-category mapper; fixed category/exit-code and HTTP-status tables in `Common`; 401/403 hint names expiry, missing scope, `cloudId`.
- Output contract types in `JiraCli.Cli/Contracts`, camelCase, `data` always an object, lists under `items`, shared ticket summary type for `list` and `related`, `data.notices` as `{ code, message, hint }`, standard fields always present (`null` when empty), versioning rules for `formatVersion`.
- `AdfMarkdownConverter` is the only ADF renderer: pure, Markdown output, never fails on unknown nodes. One shared custom-field mapper by schema type (`expand=names,schema`).

**Attachments, log, timeouts (AD-17 to AD-19)**
- Managed folder: Linux `<passwd home>/.cache/jira-cli/attachments/<key>/`, Windows `<LocalAppData>\jira-cli\attachments\<key>\`; refuse `.`, `..`, `:`, reserved Windows names, both separators; refuse symlinks/reparse points from `jira-cli` down; temp file with `CreateNew` plus atomic rename.
- `WorkLogWriter` (Common), called only from the `Program` wrapper, appends to a UTC daily file with shared access; Linux `<passwd home>/.local/state/jira-cli/logs/`, Windows `<LocalAppData>\jira-cli\logs\`; refuses symlinked chains; Gateway records requested and canonical keys through `IInvocationContext`; log failure is a stderr warning only.
- No automatic retries or throttling; comments, changelog, child keys, and relation summaries paged to completion inside adapters.

**Distribution and docs (AD-21, AD-22)**
- One framework-dependent `.nupkg` (`PackAsTool`, id and command `jira-cli`, `net10.0`) carrying the usage guide; installed with `--source <folder>` only; SemVer; all JSON via `System.Text.Json` source generation.
- Usage guide covers: `--project` and the `projectNotAllowed` hint; `related` and `related <origin> --key <k>`; passing `data.notices` on; reading attachments by absolute path and the Claude Code permission for the managed folder; never editing the config. Form (CLAUDE.md excerpt or skill) decided in its story.
- README covers: read-only scoped token (`read:jira-work`, `read:jira-user`), `cloudId` lookup, token rotation, config location and every setting, config protection per environment (file and ancestors), passwd entry and non-root agent in containers, proxies and root certificates, install/update commands.

**Deferred items that land in stories**
- Live checks against a real site (`redirect=false` with Basic auth, scoped-token attachment access, comment `total`, `statusCategory` filter, `parent =` across projects, `expand=schema` type names) are the first task of the Gateway and attachment stories; a failure amends the governing AD.
- Ctrl+C behavior is decided in the `Program` wrapper story.
- Per-command `data` fields are decided in each command's story under AD-13 and AD-15.

### UX Design Requirements

Not applicable: jira-cli is a CLI with no UI. The agent-facing interface (JSON envelope, help, usage guide) is covered by FR-19 to FR-22 and AD-13 to AD-15, AD-22.

### FR Coverage Map

FR-1: Epic 2 - `list` requires `--project`, each key in `allowedProjects`; typed filters; unresolved by default
FR-2: Epic 2 - page size from config, `maxPageSize` limit, opaque cursor
FR-3: Epic 2 - ticket summary fields in list entries
FR-4: Epic 2 - `get <key>` with all fields and comments oldest first
FR-5: Epic 2 - ADF to Markdown for description and comments
FR-6: Epic 2 - custom fields by schema type, `customFieldPassthrough`
FR-7: Epic 3 - `related <key>` with parent, children, links; other projects key-only with notices
FR-8: Epic 3 - `related <origin> --key <k>` one level at a time
FR-9: Epic 4 - `attachments list`
FR-10: Epic 4 - `attachments download` into the managed folder
FR-11: Epic 3 - `history <key>` with value redaction rules
FR-12: Epic 1 - fixed config location and `allowedProjects` (README part in Epic 5)
FR-13: Epic 1 - Gateway pre/post checks; applied per command in Epics 2, 3, 4
FR-14: Epic 1 - no option or environment variable changes config path or allowed projects
FR-15: Epic 1 - GET-only transport
FR-16: Epic 1 - classic and scoped token auth (README part in Epic 5)
FR-17: Epic 1 - credentials read from the config file
FR-18: Epic 1 - secret redaction at every sink
FR-19: Epic 1 - JSON envelope with `formatVersion`; contracts extended per command in Epics 2, 3, 4
FR-20: Epic 1 - error categories and exit codes 0 to 10
FR-21: Epic 1 - built-in help; extended per command in Epics 2, 3, 4
FR-22: Epic 5 - usage guide for Claude Code shipped in the package
FR-23: Epic 1 - `doctor`
FR-24: Epic 1 - work log line per invocation
FR-25: Epic 3 - origin recorded for `related <origin> --key <k>`
FR-26: Epic 1 - local log location

NFR-1: Epics 2, 3, 4 - no hard target; checked informally in live checks
NFR-2: Epic 1 (JSON timeout), Epic 4 (download timeouts)
NFR-3: Epic 5 - release gate on Windows and Linux, x64 and arm64 smoke tests
NFR-4: Epics 1 to 4 - safety suites written with each feature; all run in the Epic 5 release gate
NFR-5: Epic 1 - `rateLimited` with `retryAfterSeconds`, no retries
NFR-6: Epic 1 - project split and architecture tests; upheld in every epic
NFR-7: Epic 5 - README plus `doctor`

## Epic List

### Epic 1: Safe connection and setup check

An engineer places the config file and runs `jira-cli doctor` to confirm that the config is complete, the credentials work (classic or scoped token), Jira is reachable, and each allowed project is accessible, with a fix hint for each failure. Every invocation already returns one JSON envelope with fixed error categories and exit codes, never exposes the token, and writes a work log line. This epic lays the safety core every later command uses: GET-only transport, the fixed config path, and `JiraGateway` with its scope checks. It includes the first live checks against a real Jira site.
**FRs covered:** FR-12, FR-13 (Gateway), FR-14, FR-15, FR-16, FR-17, FR-18, FR-19, FR-20, FR-21, FR-23, FR-24, FR-26; NFR-2 (JSON), NFR-5, NFR-6

### Epic 2: List and read tickets

The agent lists unresolved tickets in named allowed projects with filters and paging, then reads one ticket in full: fields, description and comments as Markdown, and displayable custom fields. This covers loop steps 1, 2 (core), and 5.
**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-13 (list and get)

### Epic 3: Related tickets and change history

The agent follows a ticket's parent, children, and links one level at a time, reads related tickets' content with the origin recorded in the log, and reads the change history. Other-project relations stay key-only, with notices asking the engineer to allow them.
**FRs covered:** FR-7, FR-8, FR-11, FR-13 (relations and history), FR-25

### Epic 4: Attachments

The agent lists a ticket's attachments and downloads selected ones or all of them into the managed per-ticket folder, getting each file's absolute path. Includes the live check of attachment access with a scoped token.
**FRs covered:** FR-9, FR-10, FR-13 (attachments); NFR-2 (download timeouts)

### Epic 5: Department rollout

An engineer installs jira-cli from the shared folder source and sets it up and protects it from the README alone, and Claude Code learns to use it from the shipped usage guide. The release gate runs all suites on Windows and Linux.
**FRs covered:** FR-22, FR-12 (README), FR-16 (README); NFR-3, NFR-4 (release gate), NFR-7

**Dependencies:** Epic 1 → Epic 2 → Epics 3 and 4 (either order) → Epic 5.

## Epic 1: Safe connection and setup check

An engineer places the config file and runs `jira-cli doctor` to confirm that the setup works, with a fix hint for each failure. Every invocation returns one JSON envelope with fixed error categories and exit codes, never exposes the token, and writes a work log line. This epic builds the safety core every later command uses.

### Story 1.1: Solution skeleton with architecture guards

As an engineer maintaining jira-cli,
I want the solution split into the five projects with their dependency rules enforced by tests,
So that every later story starts from a structure that cannot drift.

**Requirements:** NFR-6, AD-1, AD-2, AD-20 (architecture tests), FR-21 (help base)

**Acceptance Criteria:**

**Given** a fresh clone
**When** I run `dotnet build` and `dotnet test` on .NET 10
**Then** the solution contains `JiraCli.Cli`, `JiraCli.Application`, `JiraCli.Domain`, `JiraCli.Infrastructure`, `Atlassian.Common`, one test project per src project, and `JiraCli.ArchitectureTests`, using the Stack versions in the spine
**And** `global.json` sets `test.runner` to `Microsoft.Testing.Platform`

**Given** the architecture test suite
**When** it runs
**Then** it fails if any project reference breaks AD-1 (`Domain` and `Atlassian.Common` reference no solution project; `Cli` uses `Infrastructure` only through its `Add…()` method)
**And** it fails if `Atlassian.Common` contains a Jira type, URL, JQL, or field name (AD-2)
**And** it fails if any type other than the designated console writers uses `System.Console` for output
**And** a self-test fixture proves the console rule flags a violation inside an async lambda

**Given** the built tool
**When** I run `jira-cli --version` or `jira-cli --help`
**Then** it prints plain text only, with exit code 0, and does not read the config file
**And** each project exposes exactly one `Add…()` registration method, wired in a hand-built DI container in `Cli` with one scope per invocation

### Story 1.2: Fixed config location and config loading

As an engineer setting up jira-cli,
I want the tool to read its config only from one fixed user-level location, and to report exactly which key is wrong,
So that the agent cannot redirect it to another config and setup mistakes are easy to fix.

**Requirements:** FR-12, FR-14, FR-17, AD-11

**Acceptance Criteria:**

**Given** Windows
**When** the config path is resolved
**Then** it is `<RoamingAppData>\jira-cli\config.json` from `SHGetKnownFolderPath` with an explicit process token, and `Environment.GetFolderPath` is never used for it (architecture test)

**Given** Linux, WSL, or Docker
**When** the config path is resolved
**Then** it is `<passwd home>/.config/jira-cli/config.json` with the home from `getpwuid_r`
**And** if the lookup fails, loading returns a `config` failure and never falls back

**Given** a decoy config with wider `allowedProjects` reachable through hostile `HOME`, `XDG_*`, `USERPROFILE`, `HOMEDRIVE`, `HOMEPATH`, `APPDATA`, or `LOCALAPPDATA`
**When** any command runs
**Then** the decoy is never read, and no option or environment variable can change the path (FR-14)

**Given** a config file
**When** it is loaded once per invocation through source-generated `Load(path, JsonTypeInfo<T>)`
**Then** top-level `site` (single DNS label), `email`, `apiToken`, and optional `cloudId` (GUID) map to `ConnectionSettings`, and the `jira-cli` section (`allowedProjects` non-empty, `customFieldPassthrough` matching `^customfield_[0-9]+$`, `defaultPageSize` 20, `maxPageSize` 100) maps to `AccessSettings`
**And** a validation error names the offending key and never its value

**Given** `allowedProjects` entries
**When** they are loaded
**Then** each becomes a `ProjectKey` value object (`^[A-Z][A-Z0-9_]+$`, upper-cased with ordinal rules), an invalid entry is a `config` error, and no raw project key string passes the Cli layer

**Given** a missing, unreadable, or invalid config
**When** it is loaded
**Then** loading returns a failure value, not an exception, naming the cause or the offending key and never its value
**And** Story 1.4 turns that value into `config` (exit 3) for commands, and Story 1.7 reports it as the `doctor` config check

### Story 1.3: GET-only transport with contained credentials and a live check

As an engineer handing jira-cli to an agent,
I want the HTTP layer to be unable to send anything but GET, and to send my token only to my Jira base URL, and to confirm against my real Jira that it connects,
So that no bug or hostile input can write to Jira or leak the token to another host.

**Requirements:** FR-15, FR-16, NFR-2 (JSON), NFR-5, AD-5, AD-12, AD-19

**Acceptance Criteria:**

**Given** `IAtlassianTransport`
**When** its public surface is inspected
**Then** it exposes only `GetAsync(RelativeRequest, CancellationToken)`, where `RelativeRequest` holds path segments, query name and value pairs, and a `ResponseKind`, with no raw URL string and no method
**And** only the transport implementation uses `HttpClient` (architecture test)

**Given** a fake `HttpMessageHandler`
**When** the transport suite sends requests
**Then** every request is GET, path segments and query values are percent-encoded by the transport, and `.` and `..` segments are refused
**And** the `Authorization` header goes on every request and only to the base URL that `AtlassianEndpoint` chooses: `https://<site>.atlassian.net/`, or `https://api.atlassian.com/ex/jira/<cloudId>/` when `cloudId` is set

**Given** a 3xx response
**When** the transport receives it
**Then** no redirect is followed and it raises a `TransportException` carrying the status (Stories 1.4 and 1.6 map it to `unexpected`)
**And** TLS validation is never disabled, and proxy selection stays the .NET default

**Given** a JSON request
**When** no complete response arrives within 30 seconds, or Jira returns a non-2xx status
**Then** a `TransportException` carries the timeout or status (and `Retry-After` for 429), and no retry is attempted (NFR-2, NFR-5)

**Given** the engineer's real config and the department Jira site (manual live check run by the engineer; never run by `dotnet test` or the release gate)
**When** the engineer runs `tools/JiraCli.LiveCheck`, a console app in the solution that is never packaged, once with a classic token on the site URL and once with a scoped read-only token through the `cloudId` gateway
**Then** it loads the config through Story 1.2's loader, sends `myself` and one `project/{key}` request per allowed project through `IAtlassianTransport`, and prints the endpoint in use, each status, and each response body, never the token or the `Authorization` header
**And** it runs under the debugger with no extra setup
**And** any difference from documented behavior is recorded and amends AD-12 before Story 1.6 starts

### Story 1.4: JSON envelope, error categories, and exit codes

As the agent calling jira-cli,
I want every result as one JSON document with a fixed error category and exit code,
So that I can parse every outcome reliably and tell error kinds apart.

**Requirements:** FR-19, FR-20, AD-13, AD-14, AD-15

**Acceptance Criteria:**

**Given** any invocation other than `--help` and `--version`
**When** it completes, succeeds or fails
**Then** stdout carries exactly one JSON document `{ formatVersion, ok, data?, error? }`, UTF-8 without BOM, written only by `EnvelopeWriter` from the `Program` wrapper
**And** `error` is `{ category, message, hint, retryAfterSeconds? }` and `formatVersion` is a string

**Given** an unknown command, unknown option, or malformed argument
**When** the parser fails
**Then** the envelope has category `usage` and the process exits 2
**And** an argument such as `@secrets.txt` is never expanded (`ResponseFileTokenReplacer = null`, directives cleared)

**Given** the category and exit-code table in AD-14
**When** `ExceptionMapper` maps each Domain exception type and an unhandled exception
**Then** each maps to exactly the fixed category and exit code 0 to 10, and an unhandled exception yields `unexpected` with exit 1
**And** the tables live in `Atlassian.Common`, and `ExceptionMapper` is the only place that maps exceptions to categories

**Given** the output contract (AD-15)
**When** any command returns data
**Then** output types live in `JiraCli.Cli/Contracts` and are mapped from Domain models; Domain types are never serialized directly (architecture test)
**And** keys are camelCase, `data` is always an object, lists use the key `items`, non-fatal messages use `data.notices` as `{ code, message, hint }`, and standard fields are always present and `null` when empty
**And** the versioning rule is recorded next to `formatVersion`: renaming or removing a field, changing its type, or changing an exit code increments it; adding a field does not

**Given** the process receives Ctrl+C
**When** the `Program` wrapper handles cancellation
**Then** the behavior decided in this story is documented in the code and covered by a test (spine Deferred item)
**And** message language is fixed to English (`SatelliteResourceLanguages=en`), and stderr carries only warnings

**Given** a config load failure from Story 1.2
**When** any command other than `doctor` runs
**Then** the envelope has category `config` and the process exits 3
**And** the config is loaded before a parse failure is reported

### Story 1.5: Secret redaction and the work log

As an engineer,
I want every invocation logged locally without content, and the token never shown anywhere,
So that I can audit what the agent accessed and no credential leaks.

**Requirements:** FR-18, FR-24, FR-26, AD-12 (redaction), AD-18

**Acceptance Criteria:**

**Given** a config whose token contains characters such as `+`
**When** the token or its Basic header value would appear in stdout, stderr, or the log
**Then** `ISecretRedactor` replaces every exact match, on string values before JSON encoding

**Given** any invocation except `--help` and `--version`, including parser errors
**When** it finishes
**Then** the `Program` wrapper appends one JSON line through `WorkLogWriter` to a UTC-dated daily file: timestamp, command, redacted arguments, tickets touched, origin, result category, duration (ISO 8601 UTC with `Z`)
**And** the file is opened in append mode with shared access, at `<passwd home>/.local/state/jira-cli/logs/` on Linux and `<LocalAppData>\jira-cli\logs\` on Windows, resolved as in Story 1.2

**Given** config load failed
**When** the invocation is logged
**Then** the log records only option names and an argument count

**Given** the log file or any folder from `jira-cli` down is a symlink or reparse point, or writing fails
**When** the log is written
**Then** the write is refused, a warning goes to stderr, and stdout and the exit code are unchanged
**And** commands themselves never write to the log

### Story 1.6: `JiraGateway` with scope checks

As an engineer,
I want every Jira call to pass through one gateway that checks project access before and after the call,
So that no command can read outside the allowed projects, now or as features are added.

**Requirements:** FR-13, NFR-4, AD-4, AD-6, AD-7, AD-14

**Acceptance Criteria:**

**Given** the Infrastructure project
**When** the architecture tests run
**Then** `JiraGateway` is the only `JiraCli.*` class depending on `IAtlassianTransport`, and `IJiraGateway` and `IJiraRequest<T>` are `internal` to `Infrastructure`

**Given** the pure `ProjectAccessPolicy` in `Domain` and the first two `RequestScope` kinds, `Unscoped` and `ProjectScope`
**When** the Gateway sends a Request
**Then** it applies the before-call check of AD-7 for the Request's scope, and on failure raises `ProjectNotAllowedException` with no HTTP call
**And** a `ProjectScope` with no keys is refused rather than filled in
**And** a scope with an after-call check refuses a Request that does not implement observation
**And** `Unscoped` is accepted only for exactly `GetMyselfRequest`
**And** the remaining AD-7 kinds are added by the first story that needs each one (`IssueScope` in 2.3, `VerifiedIssueScope` in 2.4, `ChildKeysScope` in 3.2, `AttachmentRefScope` in 4.2), so the set stays closed and matches AD-7 by the end of Epic 4

**Given** `GetProjectRequest` for an allowed project
**When** Jira returns a project whose key differs or is missing
**Then** the post-check fails closed and no content leaves the Gateway

**Given** every `IJiraRequest<T>` in the assembly
**When** the reflection test runs
**Then** each appears in the reviewed Request-to-scope table, and an unlisted Request fails the test

**Given** a `TransportException`
**When** it reaches the Gateway
**Then** it is translated to the Domain exception for its category (AD-14), and requested keys are recorded through `IInvocationContext` for the work log

### Story 1.7: `doctor` checks config, credentials, reachability, and project access

As an engineer setting up jira-cli,
I want `doctor` to verify my config, my token, the connection, and each allowed project,
So that I know the setup works before handing the tool to the agent.

**Requirements:** FR-16, FR-20, FR-21, FR-23, AD-7, AD-13, AD-14

**Acceptance Criteria:**

**Given** a missing, unreadable, or invalid config
**When** I run `jira-cli doctor`
**Then** the envelope reports a failed config check with a fix hint instead of aborting the command
**And** a valid config reports the config check as passed

**Given** a valid config
**When** I run `jira-cli doctor`
**Then** it reports the endpoint in use (site URL or `cloudId` gateway) and the results of the credentials check (`GetMyselfRequest`), the reachability check, and one check per allowed project (`GetProjectRequest`), each with a fix hint on failure
**And** the envelope carries all check results in `data`; on any failure `ok` is `false`, and the first failure sets `error` and the exit code

**Given** an invalid or expired token, or a missing scope
**When** Jira returns 401 or 403
**Then** the credentials check fails with `authentication` and a hint naming an invalid or expired token, a missing scope, and that scoped tokens need `cloudId`

**Given** a timeout, connection failure, 5xx, or 429
**When** `doctor` runs
**Then** the check reports `timeout`, `network`, or `rateLimited` (with `retryAfterSeconds` when Jira provides it) with its fix hint

**Given** the `doctor` output
**When** the golden-file test runs
**Then** the output shape is pinned, and `--help` documents `doctor`

**Given** a real department Jira site (live check, run by the engineer, not in automated tests)
**When** `doctor` runs with a classic token on the site URL and with a scoped read-only token through the `cloudId` gateway
**Then** both authenticate and every allowed project is reported accessible, and any difference from the Story 1.3 live-check findings is recorded and amends AD-12

## Epic 2: List and read tickets

The agent lists unresolved tickets in named allowed projects with filters and paging, then reads one ticket in full: fields, description and comments as Markdown, and displayable custom fields. This covers loop steps 1, 2 (core), and 5.

### Story 2.1: `list` tickets in named allowed projects

As the agent,
I want to list unresolved tickets in the projects I name, filtered by assignee, status, issue type, priority, sprint, and labels,
So that the engineer and I can choose a ticket to work on.

**Requirements:** FR-1, FR-3, FR-13, FR-21, AD-7, AD-9, AD-15, AD-18, NFR-1 (informal check during the live check)

**Acceptance Criteria:**

**Given** `allowedProjects` is `["ABC", "DEF"]`
**When** I run `jira-cli list --project ABC` (or `--project ABC --project DEF`)
**Then** the envelope lists matching tickets in `data.items`, each with key, issue type, summary, status, priority, assignee (`{ displayName, accountId }`), and last updated (ISO 8601 UTC), using the ticket summary contract type that `related` will reuse
**And** results are ordered by `updated DESC`

**Given** no `--project`, or any `--project` key not in `allowedProjects`
**When** I run `list`
**Then** the whole request is refused with `projectNotAllowed` (exit 5) before any HTTP call
**And** the hint lists the allowed keys and states whether the option was missing
**And** the parser does not mark `--project` as required (which would yield `usage`), and `--help` states that it is required

**Given** no `--status` filter
**When** the query is built
**Then** it adds `statusCategory != Done`, and `--help` says so

**Given** filters `--assignee` (including `me`, mapped to `currentUser()`), `--status`, `--type`, `--priority`, `--sprint`, and `--label`, in any combination
**When** the query is built
**Then** `TicketQuery` (Domain) carries them as typed values and `JqlBuilder` (Infrastructure) is the only JQL producer, escaping every value and always emitting the project restriction as a top-level AND
**And** raw JQL is never accepted, and hostile filter values cannot change the query structure (tests)

**Given** a search response
**When** the Gateway post-checks it under `ProjectScope`
**Then** every item must yield exactly one non-null `fields.project.key`, allowed and matching its key prefix; any mismatch, missing value, or count mismatch fails closed with no content in the output (tests with moved tickets)

**Given** a `list` invocation
**When** it is logged
**Then** the work log records the requested project keys and the result count, never the returned ticket keys or content (AD-18)

**Given** the `list` output
**When** the golden-file test runs
**Then** the output shape is pinned
**And** a live check on a real site confirms that the `statusCategory` filter behaves as documented; a difference is recorded and amends AD-9

### Story 2.2: Page through `list` results

As the agent,
I want a bounded page of results and a cursor to fetch the next page,
So that large projects do not flood my context.

**Requirements:** FR-2, AD-19

**Acceptance Criteria:**

**Given** no `--limit`
**When** I run `list`
**Then** at most `defaultPageSize` entries are returned (initial 20)

**Given** `--limit` greater than `maxPageSize` (initial 100), or not a positive integer
**When** I run `list`
**Then** it fails with `usage` (exit 2) before any HTTP call

**Given** more results exist
**When** a page is returned
**Then** `data.nextPageToken` carries Jira's opaque cursor, and it is `null` on the last page; no total is reported

**Given** `--page-token <value>` from a previous `nextPageToken`
**When** I run `list` with the same filters
**Then** the next page is returned
**And** the token is passed only as the cursor value, and hostile values cannot alter the request path or query (tests)

### Story 2.3: `get` a ticket with its fields and Markdown description

As the agent,
I want to read one ticket of any type by key with its fields and a readable description,
So that I understand what to implement.

**Requirements:** FR-4, FR-5, FR-13, FR-21, AD-7, AD-8, AD-10, AD-16

**Acceptance Criteria:**

**Given** an allowed ticket key of any issue type
**When** I run `jira-cli get ABC-123`
**Then** the envelope returns summary, description, issue type, status, priority, labels, components, fix versions, reporter, assignee, and created and updated dates
**And** the output carries no relations (parent, subtasks, links)

**Given** a key argument
**When** the Cli layer parses it
**Then** it becomes an `IssueKey` value object (`^[A-Z][A-Z0-9_]+-[1-9][0-9]*$`, upper-cased with ordinal rules), and no raw key string passes the Cli layer

**Given** a key whose project is not allowed, or a malformed key
**When** I run `get`
**Then** it fails with `projectNotAllowed` (exit 5) or `usage` (exit 2) before any HTTP call
**And** a key that does not exist returns `notFound` (exit 6)

**Given** a ticket that moved to a disallowed project (response project or key prefix differs)
**When** the Gateway post-checks the `IssueScope` response
**Then** it fails closed with `projectNotAllowed` and no content in the output
**And** the requested and canonical keys are recorded for the work log, even when the check fails

**Given** a description in ADF
**When** it is rendered
**Then** `AdfMarkdownConverter` (the only ADF renderer, pure) produces Markdown that keeps headings, lists, code blocks, tables, links, and mentions
**And** an unknown node keeps its text and never fails the command

**Given** other-project data planted in embedded fields of the issue response (parent, subtasks, issue links, embedded `comment`)
**When** `get` runs
**Then** none of it reaches stdout (tests), and relation data is exposed only as `RawRelation`, which Contracts cannot map

**Given** the `get` output
**When** the golden-file test runs
**Then** the output shape is pinned, and `--help` documents `get`

### Story 2.4: Comments in `get`

As the agent,
I want all comments on a ticket, oldest first, as Markdown,
So that I see the discussion and decisions behind the ticket.

**Requirements:** FR-4, FR-5, AD-8, AD-19

**Acceptance Criteria:**

**Given** an allowed ticket with comments spanning several pages
**When** I run `jira-cli get ABC-123`
**Then** the output includes every comment, oldest first, each with author (`{ displayName, accountId }`), created date, and body rendered by `AdfMarkdownConverter`
**And** comments are fetched only by `GetCommentsRequest` under `VerifiedIssueScope`, using the `VerifiedIssue` from the `get` call and paged to completion inside the adapter

**Given** a forged or foreign `VerifiedIssue` token
**When** a comment Request is sent
**Then** the Gateway refuses it by object identity (tests)

**Given** a ticket with no comments
**When** I run `get`
**Then** the comments field is present and empty or `null` per the contract

**Given** a real site (live check)
**When** comments are paged
**Then** the meaning of the comment `total` matches the paging logic; a difference is recorded and amends AD-19

### Story 2.5: Custom fields in `get`

As the agent,
I want populated custom fields under their display names,
So that I see team-specific information such as acceptance notes or environments.

**Requirements:** FR-6, FR-13, AD-16

**Acceptance Criteria:**

**Given** a ticket with populated `customfield_*` values
**When** I run `get`
**Then** the output has an ordered array `[{ name, value }]` using display names from `expand=names,schema`
**And** null and empty values are omitted

**Given** a custom field whose schema type is a system string, number, date, datetime, option, user, ADF text, or an array of those
**When** it is rendered
**Then** one shared mapper renders it by the field's schema type, never by the value's shape

**Given** a custom field of any other schema type
**When** its id is not in `customFieldPassthrough`
**Then** it is omitted, including an issue-shaped array or a shape-sniffed object carrying other-project data (tests)

**Given** a field listed in `customFieldPassthrough`
**When** it is rendered
**Then** it is output as raw JSON

**Given** a real site (live check)
**When** `expand=schema` is requested
**Then** the schema type names match the mapper's accepted types; a difference is recorded and amends AD-16

## Epic 3: Related tickets and change history

The agent follows a ticket's parent, children, and links one level at a time, reads related tickets' content with the origin recorded in the log, and reads the change history. Other-project relations stay key-only, with notices asking the engineer to allow them.

### Story 3.1: `related` shows parent, subtasks, and links

As the agent,
I want to see a ticket's parent, subtasks, and linked tickets with their link types,
So that I can decide which related tickets I need to read.

**Requirements:** FR-7, FR-13, FR-21, AD-10, AD-15

**Acceptance Criteria:**

**Given** an allowed ticket of any issue type
**When** I run `jira-cli related ABC-123`
**Then** `data.items` lists its parent or epic, subtasks, and linked tickets, each with `relationship` (for example `parent`, `subtask`, or the link type such as `blocks` or `is blocked by`) and `accessible`
**And** items use the ticket summary type shared with `list`, extended with `relationship` and `accessible`
**And** there is one entry per key per relationship after removing duplicates

**Given** the origin ticket
**When** relations are collected
**Then** the Domain `RelationSet` is built only from `parent`, `subtasks`, and `issuelinks` of the verified origin

**Given** relations in allowed projects
**When** their summaries are fetched
**Then** they come from one `ProjectScope` search paged to completion, which is skipped when no relation is allowed

**Given** a relation in a project not in `allowedProjects`
**When** it is output
**Then** `RelatedIssueProjection` reduces it to `{ key, relationship, accessible: false }` with summary fields `null`, and none of its embedded data reaches stdout (tests with planted other-project summaries)

**Given** `related <origin>`
**When** it succeeds
**Then** `data.notices` is present and `null` when empty

**Given** the `related` output
**When** the golden-file test runs
**Then** the output shape is pinned, and `--help` documents `related`

### Story 3.2: `related` shows children, including those in other projects

As the agent,
I want the children of any ticket, such as the tickets under an epic, and a notice when some live in projects I cannot read,
So that I see the full breakdown and can ask the engineer to allow a missing project.

**Requirements:** FR-7, FR-13, AD-7, AD-9, AD-10, AD-22

**Acceptance Criteria:**

**Given** an allowed ticket of any issue type
**When** I run `related` on it
**Then** child keys come from `GetChildKeysRequest` under `ChildKeysScope` with the verified origin, using the fixed JQL `parent = <canonical key>` and `fields=project`, paged to completion
**And** its `Parse` reads only `key` and `fields.project.key`, and the lookup adds nothing to the Gateway's issued record (tests)

**Given** `ChildKeysScope`
**When** the Gateway checks it
**Then** the Request type must be exactly `GetChildKeysRequest`, the token must be issued by this Gateway, the `jql` value must contain the token's canonical key, and every item must have a non-null project
**And** only `GetChildKeysRequest` may call `JqlBuilder.ChildrenOf` (architecture test)

**Given** children in allowed projects
**When** their summaries are fetched
**Then** the search is `parent = <canonical key> AND project in (<allowed projects among the children>)` under `ProjectScope`, paged to completion

**Given** children in one or more disallowed projects
**When** `related` runs
**Then** each such child is output key-only with `accessible: false`, and each disallowed project adds one entry to `data.notices` from a fixed template whose only variable part is the project key, asking the engineer to add it to `allowedProjects`
**And** `ok` stays `true` and the exit code is 0, and planted project names never reach stdout (tests)

**Given** a child whose key prefix and project disagree, a child absent from the summary search, or a ticket with no allowed siblings
**When** `related` runs
**Then** the command still succeeds, and that child is output key-only with `accessible: false`

**Given** a real site (live check)
**When** `parent =` is used on a ticket with children in another project
**Then** the lookup returns them as documented; a difference is recorded and amends AD-10

### Story 3.3: Read a related ticket with `related <origin> --key <k>`

As the agent,
I want to read a related ticket's full content through its origin,
So that the work log records how I reached it.

**Requirements:** FR-8, FR-13, FR-25, AD-10, AD-18

**Acceptance Criteria:**

**Given** `related ABC-1 --key ABC-2`, where `ABC-2` is related to `ABC-1` and allowed
**When** I run it
**Then** the output is `ABC-2`'s full content in the same shape as `get` (fields, description, comments, custom fields)
**And** the work log line records `ABC-1` as origin and `ABC-2` among tickets touched

**Given** `k` is in a disallowed project
**When** I run `related <origin> --key <k>`
**Then** the origin is recorded first, the policy is applied to `k` before any HTTP call, and the command fails with `projectNotAllowed` (exit 5)

**Given** `k` is allowed but not in the origin's `RelationSet`
**When** I run it
**Then** the origin is fetched, the relation check fails, and the command returns `notFound` (exit 6) without fetching `k`

**Given** a plain `get ABC-2`
**When** it is logged
**Then** it is logged as direct access with no origin (FR-25)

### Story 3.4: `history` shows the change history safely

As the agent,
I want a ticket's change history,
So that I understand how the ticket evolved and who changed what.

**Requirements:** FR-11, FR-13, FR-21, AD-8, AD-10, AD-19

**Acceptance Criteria:**

**Given** an allowed ticket with changes spanning several pages
**When** I run `jira-cli history ABC-123`
**Then** the output lists every change, each with author (`{ displayName, accountId }`), timestamp (ISO 8601 UTC), field name, and from and to values where allowed
**And** the changelog comes only from `GetChangelogRequest` under `VerifiedIssueScope`, paged to completion inside the adapter

**Given** a change to an allow-listed system field or a custom field that AD-16 would display
**When** it is output
**Then** from and to values are shown

**Given** a change to a relation field (parent, link)
**When** it is output
**Then** values are reduced to keys

**Given** a change to any other field, including `project` and an unlisted custom field
**When** it is output
**Then** only the field name and timestamp are shown, and planted other-project values never reach stdout (tests)

**Given** a disallowed or moved ticket
**When** I run `history`
**Then** it fails with `projectNotAllowed` (exit 5) as in `get`, with no changelog fetched

**Given** the `history` output
**When** the golden-file test runs
**Then** the output shape is pinned, and `--help` documents `history`

## Epic 4: Attachments

The agent lists a ticket's attachments and downloads selected ones or all of them into the managed per-ticket folder, getting each file's absolute path. Includes the live check of attachment access with a scoped token.

### Story 4.1: `attachments list`

As the agent,
I want to see a ticket's attachments,
So that I can decide which files to download.

**Requirements:** FR-9, FR-13, FR-21, AD-8

**Acceptance Criteria:**

**Given** an allowed ticket with attachments
**When** I run `jira-cli attachments list ABC-123`
**Then** `data.items` lists each attachment with id, file name, size, MIME type, author (`{ displayName, accountId }`), and created date (ISO 8601 UTC)
**And** a ticket with no attachments returns an empty list

**Given** the issue response
**When** attachment metadata is read
**Then** it comes only from `GetIssueRequest` through the `IssueScope` post-check, and the Gateway alone mints each `AttachmentRef` into `VerifiedIssue.Attachments`
**And** `IAttachmentReader` returns Domain `AttachmentInfo`; no token or `AttachmentRef` crosses into `Application`

**Given** a disallowed or moved ticket
**When** I run `attachments list`
**Then** it fails with `projectNotAllowed` (exit 5) with no content in the output

**Given** the `attachments list` output
**When** the golden-file test runs
**Then** the output shape is pinned, and `--help` documents `attachments list`

### Story 4.2: Download one attachment into the managed folder

As the agent,
I want to download a selected attachment by name or id and get its absolute path,
So that I can read the file, such as a log or screenshot, while implementing.

**Requirements:** FR-10, FR-13, NFR-2 (downloads), AD-5, AD-8, AD-17, AD-19

**Acceptance Criteria:**

**Given** `attachments download ABC-123 --name log.txt` or `--id <id>`
**When** the attachment exists on that ticket
**Then** it is saved to `<passwd home>/.cache/jira-cli/attachments/<canonical key>/` on Linux or `<LocalAppData>\jira-cli\attachments\<canonical key>\` on Windows, resolved as in Story 1.2
**And** the result lists the file as `downloaded` with its absolute path, and an existing file with the same name is replaced

**Given** a `--name` matching several attachments
**When** I run the download
**Then** it fails with `usage` (exit 2) and the error lists the candidates (Domain `AttachmentSelection`)
**And** a `--id` that is not one of the ticket's attachments is refused, and an unminted `AttachmentRef` is refused by the Gateway (tests)

**Given** the download request
**When** bytes are fetched
**Then** they are requested by path from the attachment id with `redirect=false` under `AttachmentRefScope`; the absolute `content` URL in metadata is never followed
**And** the Gateway owns the response lifetime through `IJiraGateway.DownloadAsync(AttachmentRef, sink)`, and no stream crosses `Application`

**Given** the transport `Download` profile
**When** no headers arrive within 30 seconds, or a body read makes no progress for 30 seconds
**Then** the download fails with `timeout`, with no limit on total time and no retry (NFR-2)

**Given** a file name that is `.` or `..`, contains `/`, `\`, or `:`, or is a reserved Windows name
**When** the store saves it
**Then** the write is refused on every platform

**Given** `jira-cli`, `attachments`, or `<canonical key>` under the base folder is a symlink or reparse point (including ancestor-plus-inner-link cases)
**When** the store saves a file
**Then** it refuses to write, checked component by component; links above `jira-cli` are unaffected (tests on Windows and Linux)

**Given** a download that fails midway
**When** the store writes
**Then** writing goes to a randomly named temp file opened with `FileMode.CreateNew` and is atomically renamed, so any existing file stays untouched, and the file is reported as `failed` with `ok: false`
**And** a local file system failure maps to `localIo` (exit 10)

**Given** a real site (live check)
**When** a classic token and a scoped token download an attachment with `redirect=false` and Basic auth
**Then** both succeed as documented; a difference is recorded and amends AD-8 or AD-12

### Story 4.3: Download all attachments

As the agent,
I want to download all of a ticket's attachments in one call,
So that I get every file I may need without listing and selecting each.

**Requirements:** FR-10, FR-21, AD-13, AD-17

**Acceptance Criteria:**

**Given** `attachments download ABC-123 --all`
**When** I run it
**Then** every attachment is saved into the managed per-ticket folder, each reported as `downloaded` with its absolute path
**And** `--all` cannot be combined with `--name` or `--id`, and a download with none of the three is refused, both as `usage` (exit 2); `--help` documents `--all`

**Given** several attachments with the same name, compared after Windows name normalization and with an ordinal case-insensitive rule on Windows
**When** I download all
**Then** only the newest is downloaded

**Given** some files fail and others succeed
**When** the command finishes
**Then** `data` lists each file as `downloaded` or `failed`, `ok` is `false`, and the first failure sets `error` and the exit code (partial result per AD-13)

**Given** no option for a target directory exists
**When** `--help` is inspected
**Then** `attachments download` offers no way to write outside the managed folder

**Given** the `attachments download` output
**When** the golden-file test runs
**Then** the output shapes for single, all, and partial-failure results are pinned

## Epic 5: Department rollout

An engineer installs jira-cli from the shared folder source and sets it up and protects it from the README alone, and Claude Code learns to use it from the shipped usage guide. The release gate runs all suites on Windows and Linux.

### Story 5.1: Package jira-cli as a .NET tool

As an engineer in the department,
I want to install and update jira-cli with one `dotnet tool` command from a shared folder,
So that I get the same tool on Windows, WSL, and Docker without pulling anything from a public feed.

**Requirements:** NFR-3, AD-21

**Acceptance Criteria:**

**Given** `dotnet pack`
**When** the package is built
**Then** it produces one framework-dependent `.nupkg` (`PackAsTool`, package id and command `jira-cli`, `net10.0`) with a SemVer version, and `jira-cli --version` prints that version

**Given** a folder containing the `.nupkg`
**When** I run `dotnet tool install -g jira-cli --source <folder>` and later `dotnet tool update -g jira-cli --source <folder>`
**Then** the tool installs and updates on Windows and Linux (including WSL and a Docker container) with the .NET 10 SDK, and `jira-cli doctor` runs

**Given** the code base
**When** the architecture tests run
**Then** all JSON serialization uses `System.Text.Json` source generation, so a later move to self-contained or Native AOT stays a build configuration change

**Given** the package contents
**When** they are inspected
**Then** they include the usage guide file location reserved for Story 5.3 (the file itself may be a placeholder until then)

### Story 5.2: README for setup and protection

As an engineer setting up jira-cli,
I want one README that tells me how to install, configure, and protect the tool,
So that `doctor` passes and the agent cannot change my config, without asking anyone.

**Requirements:** FR-12, FR-16, NFR-7, AD-3, AD-22

**Acceptance Criteria:**

**Given** the README in `docs/`
**When** I read it
**Then** it covers: install and update with `--source` only, and why `--add-source` or no source is unsafe; the config location on Windows and Linux and every setting with its default; read-only scoped token setup with `read:jira-work` and `read:jira-user`; `cloudId` lookup; token rotation and the expired-token error
**And** it covers config protection per environment for the file and every ancestor directory: Docker read-only mount, WSL and Linux ownership, Claude Code `deny` rules
**And** it covers containers: the passwd entry for the agent's UID, running the agent as non-root, and `/etc/passwd` not writable by it
**And** it covers corporate proxies and installing a root certificate in WSL and Docker
**And** it states the out-of-scope threats from AD-3

**Given** a fresh WSL, Docker, and Windows environment
**When** an engineer follows only the README
**Then** `jira-cli doctor` passes in each (manual check by the engineer)

### Story 5.3: Usage guide for Claude Code

As the agent,
I want a usage guide shipped with the tool,
So that I use jira-cli correctly and safely without the engineer explaining it each time.

**Requirements:** FR-22, FR-25, AD-22

**Acceptance Criteria:**

**Given** the usage guide form
**When** this story starts
**Then** the form (CLAUDE.md excerpt or Claude Code skill) is decided and recorded, and the guide ships inside the `.nupkg` with install instructions in the README

**Given** the usage guide
**When** it is read
**Then** it teaches: pass `--project`, learning keys from the `projectNotAllowed` hint, never by reading the config; `list` covers unresolved tickets unless `--status` is given; read relations with `related` and their content with `related <origin> --key <k>`; pass `data.notices` on to the engineer, and do not take a missing notice as proof that no other-project child exists; read attachments from the returned absolute path and copy them into the repository only if needed, with the Claude Code permission setting for the managed folder; never edit the config
**And** it lists the error categories and exit codes with what the agent should do for each

**Given** a department Jira site and Claude Code with the guide installed (manual check, success signal)
**When** the engineer asks Claude Code to list unresolved tickets in a project and then work on one
**Then** Claude Code completes loop steps 1, 2, and 5 through jira-cli with no browser copy-paste

### Story 5.4: Release gate on Windows and Linux

As an engineer releasing jira-cli,
I want one gate that runs every test suite on Windows and Linux before a package is shared,
So that the safety guarantees hold on every platform the department uses.

**Requirements:** NFR-3, NFR-4, AD-20, AD-21

**Acceptance Criteria:**

**Given** the release gate
**When** it runs
**Then** it executes all suites required by AD-20 (architecture, transport, Request-to-scope reflection, Gateway, planted data, hostile input, config path, link refusal, redaction, golden output) on Windows and Linux, and a package is shared only when all pass

**Given** no CI provider has been chosen yet
**When** the gate is defined
**Then** it is one documented command or script that runs the same way locally and can later be wired into CI (CI provider stays an open question in the SPEC)

**Given** the published `.nupkg`
**When** it is smoke-tested on win-arm64 and linux-arm64 (glibc)
**Then** `--version`, `--help`, and `doctor` run (NFR-3)

**Given** a release
**When** its version is set
**Then** it follows SemVer, and any change to `formatVersion` or an exit code is called out in the release notes
