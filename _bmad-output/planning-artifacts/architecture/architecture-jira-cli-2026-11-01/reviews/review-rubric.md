# Rubric Review: jira-cli Architecture Spine

- **Reviewed:** `ARCHITECTURE-SPINE.md` (status: draft, 21 ADs)
- **Against:** `prd.md`, `addendum.md`, and `.memlog.md` for decision history
- **Date:** 2026-11-01
- **Test applied:** if two epics built this independently, could they choose incompatibly?

## Verdict

**Changes required before epics.** The shape is sound: the hexagonal split, the single Gateway, the GET-only transport, scope kinds, and the output contract fix the right main divergence points. Most ADs prevent what they say they prevent.

Six high-severity problems remain:

- One PRD requirement is contradicted without the spine saying so.
- One path can leak content from disallowed projects.
- The required test suite cannot detect failures in the safety-critical transport.
- Attachment download does not fit the Request and scope contract.
- The "only the Gateway creates it" type guarantee cannot be enforced across assemblies.
- The credential and config flow needs a dependency the diagram forbids.

All six can be fixed by editing ADs. None requires a redesign.

## Checklist Result

| Criterion | Result | Findings |
| --- | --- | --- |
| Fixes the real divergence points, misses none | Partial | R4, R5, R6, R12, R13 |
| Every Rule is enforceable and prevents its divergence | Partial | R2, R3, R5, R14, R20 |
| Nothing under Deferred can let two units diverge | Fails on 2 items | R4 (redirect), R12 (`data` layout) |
| Named tech is verified-current | Pass, with one terminology problem | R15; see Tech Verification |
| Covers the driving document's capabilities | Partial | R1, R2, R10 |
| Every dimension is decided, deferred, or open | Partial | R9, R11 |
| No bloat | Minor | R21 |
| Internally consistent | Fails | R6, R7, R8, R16 |

## High Severity

### R1. AD-9 contradicts PRD FR-1, and the spine does not say so

- **Where:** AD-9; frontmatter `binds: [FR-1..FR-26, ...]`; PRD FR-1.
- **Problem:** FR-1 says that with no filter, the list "covers unresolved tickets in all allowed projects". AD-9 requires `--project` and returns `projectNotAllowed` when it is missing. The memlog (line 24) records this as a deliberate user override, with "PRD FR-1 to be updated (offer at finalize)". The PRD is still `status: final` and unchanged. The spine claims to bind FR-1 and never mentions the override.
- **Impact:** The epic author reads two conflicting authorities. A story written from the PRD yields acceptance criteria that AD-9 forbids.
- **Related gap:** With `--project` mandatory, loop step 1 ("show unresolved tickets") requires the agent to know the allowed projects. The only sanctioned ways are reading the config file directly or running `doctor`, which makes network calls. The spine does not say how the agent finds them. An error `hint` listing the allowed projects, or a usage-guide rule, would close this.
- **Fix:** Update FR-1 in the PRD, or add an explicit override note to AD-9. Decide how the agent discovers allowed projects.

### R2. `get` can emit other-project content; AD-10 does not cover it

- **Where:** AD-16 ("any other shape to its JSON value"); AD-10; capability map row F2 (AD-7, AD-8, AD-16, not AD-10).
- **Problem:** AD-10 itself says Jira embeds summary and status in related-issue data, but it names `RelatedIssueProjection` only in connection with `related`. Nothing fixes which fields `GetIssueRequest` fetches, and nothing requires the projection on `get`. A story using `fields=*all` receives `parent`, `subtasks`, and `issuelinks` with embedded content from other projects.
- **Problem, custom fields:** AD-16's fallback passes unknown custom field shapes through as raw JSON. An issue-picker or app-provided field can carry another issue's data through that route. Whether it does depends on the site's field types and apps; I did not test this against a real site. No rule closes the route either way.
- **Impact:** This breaks FR-13 ("their content is never retrieved") and AD-3's guarantee that no Jira response can cause a read outside the allowed projects. The post-check does not help, because it inspects only the top-level ticket's project.
- **Fix:** Make the projection apply to every Request that returns issue data, not just `related`. Fix the `get` field set (explicit allow-list, or omit related data entirely). Make the custom field fallback fail closed on shapes containing an issue reference, and add a test for it in AD-20.

### R3. AD-20's required tests cannot detect transport failures

- **Where:** AD-20; AD-5; AD-12; AD-19.
- **Problem:** AD-20 says "Tests use a fake `IAtlassianTransport`" and requires a test that "runs every Command through a recording fake transport and asserts only GET". This test is vacuous. Under AD-5 the interface has only `GetAsync` and `RelativeRequest` has no method, so the fake cannot observe anything other than GET.
- **Impact:** The real risks are below the fake and have no required suite:
  - the hard-coded GET in the implementation (FR-15, NFR-4)
  - credentials sent only to the base URL (AD-12)
  - redirect handling (AD-12)
  - TLS validation never disabled (AD-12)
  - both 30-second timeouts (AD-19, NFR-2)
- **Fix:** Require a transport test suite in `Atlassian.Common` tests that uses a fake `HttpMessageHandler` and asserts method, auth header host, redirect behavior, and timeouts. Keep the Command-level suite, but restate its purpose: scope checks and the Gateway path.

### R4. Attachment download fits neither the Request contract nor the scope set, and its redirect behavior is deferred

- **Where:** AD-6; AD-7; AD-8; AD-12; AD-14; AD-19; Deferred row 2.
- **Problem, contract:** AD-6 says every endpoint is a Request with a pure `Parse(Stream)` and no I/O, returning `T`. AD-19 assumes unbounded streamed bodies. Saving a large body to disk cannot be a pure `Parse`. Nothing says what `T` is, who owns and disposes the stream, or how it passes through `SendAsync`, the port, and `IAttachmentStore`.
- **Problem, scope:** AD-8 says downloads accept `AttachmentRef`, but AD-7's closed set has only `VerifiedIssueScope(verifiedIssue)`. Either `AttachmentRef` holds a `VerifiedIssue`, or AD-7 needs a new kind. AD-20 says a new kind requires changing AD-7.
- **Problem, redirect:** Deferred says "AD-12 bounds the options", but AD-12 does not say whether a cross-host redirect is followed without credentials or refused. Third-party API docs describe `GET .../attachment/content/{id}` as returning a 303 to a download URL by default, with `redirect=false` returning the bytes directly. Under AD-14, an unfollowed 303 becomes `unexpected`. The transport (Common) and the attachment story can therefore pick incompatible behaviors, and this is the normal download case rather than an edge case.
- **Feasibility:** Attachment access with scoped tokens is also deferred, yet FR-16 and the README steer engineers to scoped tokens. I found only indirect evidence: a Confluence thread reporting 401 on a download link with a scoped token. If this fails, a core PRD combination (FR-10 with FR-16) fails.
- **Fix:** Add a streaming variant to the contract, for example `IJiraStreamRequest`, with stream ownership stated. Tie `AttachmentRef` to the AD-7 scope. Decide in AD-12: `redirect=false` is sent, and any cross-host redirect is refused. Run a short spike against a real site with a scoped token before attachment stories are written.

### R5. "Only the Gateway creates `VerifiedIssue`" cannot be enforced as placed

- **Where:** AD-8; AD-7 row 3; dependency diagram; AD-20.
- **Problem:** `AttachmentRef` must cross a port, because the use case picks attachments and hands them on, and `Application` sees only `Domain`. So it must live in `Domain`. `VerifiedIssue` has the same pressure if any port mentions it. But the creators (the Gateway and its verified responses) live in `Infrastructure`. `Infrastructure` can construct a `Domain` type only if the constructor is public, which lets anything forge one, or through `InternalsVisibleTo`, which AD-1 does not mention. The spine does not name the project for either type.
- **Impact:** Two epics can place these types differently. With a public constructor, the AD-7 pre-check for `VerifiedIssueScope` ("Argument came from a post-checked response") is only a convention. AD-20's architecture tests cover AD-1, AD-2, and AD-4, not AD-8.
- **Fix:** State the placement and the mechanism. For example: keep both in `Domain` with `internal` constructors and a single `InternalsVisibleTo` for Infrastructure, or make ports take `IssueKey` and an opaque handle. Add an architecture test that no type outside the Gateway calls the constructor.

### R6. The credential and config flow needs a dependency the diagram forbids

- **Where:** AD-12 ("Only the transport reads credentials"); AD-2; Conventions (Config schema); dependency diagram; AD-1.
- **Problem:** The config schema type, including `email` and `apiToken`, is in `Infrastructure`. The loader and the transport are in `Common`. `Common` references nothing, so the transport cannot read the schema type. Something else must read the token from it and pass it down: `Infrastructure` or the composition root. That breaks "only the transport reads credentials" as written.
- **Impact:**
  - The transport author and the config author will define different handoff types.
  - The same applies to `site` and `cloudId`, which `AtlassianEndpoint` (Common) needs.
  - `site`, `email`, `apiToken`, and `cloudId` are all Atlassian-level. Keeping them in the Jira schema makes a Confluence client re-declare them, against the AD-2 and NFR-6 intent.
  - The spine also does not say how `allowedProjects` reaches `ProjectAccessPolicy`, or which layer validates config.
- **Fix:** Define an `AtlassianConnection` type in `Common` (site, cloudId, email, token). The Jira schema extends or wraps it and adds `allowedProjects` and paging settings. Reword AD-12 as "only the transport uses credentials; the composition root passes them through unread". State where validation lives.

## Medium Severity

### R7. Two types are placed in two layers

- **Envelope:** The layer table and the seed (`Output/`) put the envelope in `Atlassian.Common`. AD-13, the F8 row, and the seed put `EnvelopeWriter` in `JiraCli.Cli`. The spine never says what `Common/Output` holds and what stays in `Cli`. Possibly the type is in Common and the writer is in Cli, but it needs saying.
- **Work log writer:** The layer table and the seed (`Infrastructure/WorkLog/`) put it in `Infrastructure`. AD-18 says "Invocation middleware in the Cli layer writes" the line, and the heading says "one writer". The F10 row names only middleware. No port or call relationship is defined.
- **Addendum conflict:** The addendum lists logging and output formatting among the shared concerns a Confluence client should reuse. Both writers currently sit in `JiraCli.*`, and nothing in the spine records that as a decision.
- **Fix:** State one owner each, and either move the mechanism to `Common` or record that reuse is deliberately dropped.

### R8. `AtlassianEndpoint` in `Common` breaks AD-2

- **Where:** AD-2 ("no Jira type, URL, JQL, or field name"); AD-12; layer table; F7 row.
- **Problem:** AD-12 has `AtlassianEndpoint`, in `Common`, choose `https://api.atlassian.com/ex/jira/<cloudId>/`. `/ex/jira/` is a Jira URL. A Confluence client needs a different product segment.
- **Enforcement gap:** The AD-20 architecture test cannot catch this, because NetArchTest checks type dependencies and not string literals. The URL and JQL parts of AD-2 therefore have no automated check.
- **Fix:** Pass the product segment as a caller parameter, like app name and schema.

### R9. Proxy and trust-store environment variables are undecided

- **Where:** AD-3 ("no flag, env var..."); AD-11 (env ignored only for config); AD-12; Deferred.
- **Problem:** `HttpClient` reads `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` by default on Linux. So `HTTPS_PROXY=... jira-cli` redirects traffic through an agent-chosen host, which AD-11's env distrust does not address. From my own knowledge, not confirmed in this review's searches: .NET on Linux also honors `SSL_CERT_FILE` and `SSL_CERT_DIR` for trust, and `DOTNET_STARTUP_HOOKS` injects code into a framework-dependent app. Together these undercut the absolute wording in AD-3 and "only to the configured base URL" in AD-12.
- **Operational side:** The reverse need is also real. A corporate proxy or TLS-inspecting network may require these variables, and the spine does not address it. Whether one exists in this department is unknown.
- **Why it matters:** The agent can already read the token (AD-3), so this is not a new privilege for a hostile agent. But the AD-3 sentence is false as written, and the operating environment is silent.
- **Fix:** Decide: use a proxy from config only, or honor env proxies and document it. Narrow AD-3's wording to exclude runtime and loader variables, placing them with the out-of-scope "other means" case.

### R10. Agent modification of the config file is outside AD-3's stated boundary, and protection is unrecorded

- **Where:** AD-3; AD-11; PRD §8 open question 2; FR-12.
- **Problem:** The PRD deferred two things to architecture: the fixed location and the protection steps. The spine decides only the location. The memlog records "agent modifying it is not [acceptable]" and lists `:ro` mounts, ownership, and deny rules. None of this is in the spine. By default the file is writable by the same user the agent runs as, so an edit widens `allowedProjects` with no flag or env var. AD-3 does not list this path, either as guaranteed or as out of scope.
- **Fix:** Add to AD-3: config integrity depends on external protection; the README must document the mount, ownership, and deny steps (FR-12). Add a docs obligation or a Deferred row.

### R11. CI and release gate are deferred, but AD-20 and NFR-3 depend on them

- **Where:** Deferred row 5; AD-20; AD-21; NFR-3; NFR-4.
- **Problem:** Deferred says "Packaging is unaffected", which is true. But nothing requires safety suites to pass before a `.nupkg` reaches the folder source, and nothing requires them to run on both OSes. AD-11's path tests are OS-specific. `getpwuid` behavior cannot be proven from Windows, and arm64 is an NFR-3 target with no test environment named.
- **Supply chain:** A tool whose value is trust is shared through a folder with no stated write control, signing, or checksum. If the agent's environment can write to that folder, the tool can be replaced. There is also no stated upgrade path for engineers, or policy for keeping the usage guide and `formatVersion` in step with installed versions.
- **Fix:** Keep the provider deferred, but add invariants: the release gate runs all suites on Windows and Linux, and the folder source is not writable from agent environments. State whether arm64 is tested or accepted untested. Record in AD-21 how the guide ships, since FR-22 says "ships with".

### R12. The Deferred per-command `data` layout can diverge on shared shapes

- **Where:** Deferred row 7; AD-13; AD-15; AD-19.
- **Problem:** The Rule is fine per command, but shared shapes are unfixed, and golden tests pin each command without making them consistent:
  - whether `data` is always an object (AD-19 needs `data.nextPageToken`, while other commands could return arrays)
  - the collection key name
  - the ticket summary shape shared by `list` and `related`
  - explicit `null` versus omission for empty fields such as an unassigned ticket (AD-16 decides only for custom fields)
  - whether `formatVersion` is an integer or a string
- **Impact:** The agent is the consumer, and FR-19 is about reliable parsing.
- **Fix:** Add one conventions row: `data` is always an object, the collection key, the null policy, and the `formatVersion` type.

### R13. The error tables have holes in common cases

- **Where:** AD-14 ("The tables below are fixed"); AD-8; AD-17.
- **Unmapped or poorly mapped cases:**
  - HTTP 400. Jira commonly returns this for JQL naming an unknown status, sprint, or similar value (from my own knowledge of Jira; not tested here). It lands in `unexpected` with exit 1, although the agent could fix it, so FR-20's "how to fix it" is lost.
  - Local filesystem failures during attachment save (disk full, access denied, missing directory). AD-13 requires a category, and none fits.
  - Ambiguous attachment name (AD-8 says "an error" with no category).
  - 3xx not followed (see R4).
- **Fix:** Add rows for these. Consider `usage` for 400 and for ambiguous names. Name a category for local I/O, noting that a new exit code affects `formatVersion` (AD-15).

### R14. The post-check does not fail closed on an empty observation

- **Where:** AD-6; AD-7.
- **Problem:** AD-6 has each Request supply `ObservedProjects(T)`. AD-7 says the reported project must be allowed. An empty set is legitimate for an empty `ProjectScope` list, but under `IssueScope` a Request that returns nothing, for example because `fields` omitted `project`, passes vacuously. Nothing forbids this, and AD-20's listed tests do not name it.
- **Fix:** Add to AD-7: `IssueScope` requires exactly one observed project; missing project data on any returned item is a failed check.

### R15. "Invocation middleware" has no framework support

- **Where:** AD-18; layer table; F10 row.
- **Problem:** A third-party summary of System.CommandLine 2.0 GA says `CommandLineBuilder` and `AddMiddleware` were removed. Microsoft's beta5 migration guide confirms the builder was changing, but I did not find an official statement for GA. The memlog notes only that the GA API differs from betas. A builder following the AD's wording looks for an API that may not exist. It must be a hand-written wrapper around parse and invoke.
- **Impact:** The wrapper must also cover parser errors and config failures for FR-24 ("every invocation"). The spine does not say whether `--help` and `--version` are logged.
- **Fix:** Name the mechanism in AD-18: a wrapper in `Program`, around parse and invoke. State what counts as an invocation.

### R16. AD-4 as written conflicts with the composition root, and `Cli`'s references are unrestricted

- **Where:** AD-4; DI convention; diagram edges `Cli --> Infra`, `Cli --> Common`.
- **Problem:** "`JiraGateway` is the only class in `JiraCli.*` that may depend on `IAtlassianTransport`" conflicts with a composition root in `JiraCli.Cli` that registers it, unless each project exposes its own registration extension. The convention "implementations `internal` where possible" also requires that, since `Cli` cannot name internal types. The spine does not state it.
- **Bypass:** The `Cli --> Infra` edge also lets a Command call Infrastructure directly, bypassing the use case. The safety path still holds, but AD-1's intent does not.
- **Fix:** State: each project exposes `Add…()` registration; `Cli` uses `Infrastructure` only in the composition root; add this to the architecture test.

### R17. `doctor` conflicts with config bootstrap

- **Where:** AD-11; AD-13; DI convention.
- **Problem:** Config is loaded on every invocation before anything else, and invalid config normally ends in `config`. But `doctor` must report config failures through its check list, and it needs the Gateway when config is valid. A container built from valid config cannot exist when config is invalid. The spine does not say how the composition root branches. The general bootstrap author and the `doctor` author can both reasonably own this.
- **Fix:** One sentence: config load returns a result, the composition root builds the Gateway path only when it is valid, and `doctor` receives the result.

## Low Severity

### R18. Other inconsistencies and gaps

- **Map omissions:** F4 omits AD-7, AD-12, and AD-19. AD-19 names large downloads but does not bind FR-10. F5 omits AD-7. F2 omits AD-10 (see R2).
- **Naming:** The F4 row has `AttachmentsCommand` while the CLI surface has two subcommands and the paradigm says one use case per Command.
- **Unenforced Binds:** NFR-1 and FR-22 are claimed by `binds` but have no AD, only a map entry at most. This is acceptable for NFR-1 (no target), but the frontmatter overstates. NFR-7 is bound by AD-21, which says nothing about onboarding.
- **NFR-2 reinterpretation:** "Each request ... times out after 30 seconds" became a header timeout plus a stall timeout with no total limit. This is sensible for downloads, but it silently loosens the requirement for ordinary calls and should be noted.
- **FR-25 wording:** The PRD says the log "always records" the origin. AD-10 concedes that plain `get` of a related ticket has none. This is a recorded user decision, so the PRD wording should be aligned when R1 is fixed.

### R19. Environment details worth a line

- **Stdout encoding:** AD-13 is silent. Windows consoles may not default to UTF-8, and the department likely has Japanese ticket content. `System.Text.Json` also escapes non-ASCII by default. A single writer limits divergence, but the log writer is separate. State UTF-8 without BOM and the encoder choice.
- **Parallel invocations:** Multiple agent processes append to one daily file. On Windows, sharing mode determines whether a line is lost, and the log could lose lines despite FR-24. "Daily" also has no stated time zone, although the convention says UTC.
- **Hostile-env test list:** AD-20 names `HOME` and `APPDATA`. It should also name `USERPROFILE`, `LOCALAPPDATA`, and `XDG_*`.
- **Known Folder fallback:** The Deferred row says "if it fails, use the Win32 API directly", but AD-11's rule already names the Known Folder API. State which .NET call is meant.
- **`RelativeRequest` validation:** AD-5 does not require it to reject absolute or protocol-relative values. A path such as `//host/x` could resolve to another host when combined with the base URI. `IssueKey` guards the normal route, but AD-12's guarantee should not rest on callers.

### R20. Rules that cannot be mechanically checked

These are acceptable as judgment rules, but the spine should not imply they are enforced:

- AD-1: "Business decisions live in `Domain`".
- AD-2: the URL, JQL, and field name parts (see R8).
- AD-13 and the Console convention: stdout and stderr writers. A NetArchTest rule banning `System.Console` outside the two writers is cheap and is not in AD-20's list.
- AD-1's project reference rule: NetArchTest sees type usage, not `.csproj` references. An unused forbidden reference passes.

## Bloat

### R21. Story-level detail

The spine is reasonably lean. These parts fail the two-units test because one epic owns the topic:

| Location | Keep | Move to the story |
| --- | --- | --- |
| AD-17 | Strip path components, stay within the target directory, atomic write | Timestamp comparison outcomes, duplicate-name rule, status words |
| AD-16 | Single renderer, ordered `[{ name, value }]` shape | Per-shape value mapping, except the leak rule from R2 |
| AD-19 | Timeouts, no retries, cursor exposed only by `list` | `--limit` behavior details |
| AD-9 | No raw JQL, single builder, mandatory project, top-level AND | `resolution = Unresolved` default, default order |
| AD-8 | Type-gated sub-resources, no raw attachment id | `--name` and `--id` selection, ambiguity behavior |
| Conventions | Everything else | `--assignee me` mapping |

Some rationale has also entered Rules: "because display names may repeat" in AD-16 and "Jira embeds summary and status..." in AD-10. These are minor.

## Tech Verification

Checked 2026-11-01.

| Item | Spine | Result |
| --- | --- | --- |
| .NET | 10 LTS | Current. Support to 2028-11-14. Support page snapshot lists 10.0.13 as the latest runtime patch |
| System.CommandLine | 2.0.14 | Latest stable on NuGet. 3.0.0 is at rc.3 only. See R15 |
| Microsoft.Extensions.DependencyInjection | 10.0.14 | Exists on NuGet. One patch ahead of the runtime version in the snapshot above, which may be stale; not a concern |
| xunit.v3 | 4.0.2 | Latest on NuGet |
| NetArchTest.Rules | 1.3.2 | Latest on NuGet by version ordering. Publish date and .NET 10 compatibility not checked; confirm when the test project is created |
| Jira REST v3 search | `GET /rest/api/3/search/jql` | Exists with `nextPageToken`. Old `/search` is deprecated. First-page bug JRACLOUD-94632 is reported, and AD-19 already omits the parameter |
| Attachment content | Deferred | Could not read Atlassian's official page (fetch truncated). 303 and `redirect=false` come from third-party docs. See R4 |

## What Holds Up

- AD-5's GET-only type is a strong structural guarantee. Its problem is only the test (R3).
- AD-7's scope table and AD-9's fail-closed list behavior are specific and testable.
- AD-11's passwd-based path, with no fallback, is a real answer to FR-14.
- AD-13, AD-14, and AD-15 fix the agent contract well, apart from R12 and R13.
- AD-21 closes the PRD's distribution question, and the deployment diagram covers the environment.
- Deferred rows for moved tickets, `dotnet tool update`, the usage guide form, AOT, log retention, and telemetry are all safe.
- The opaque `--page-token` is safe as an input, because AD-7's post-check still applies to every result.

## Sources

- [.NET and .NET Core Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [NuGet flat container: System.CommandLine](https://api.nuget.org/v3-flatcontainer/system.commandline/index.json)
- [NuGet flat container: Microsoft.Extensions.DependencyInjection](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.dependencyinjection/index.json)
- [NuGet flat container: xunit.v3](https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json)
- [NuGet flat container: NetArchTest.Rules](https://api.nuget.org/v3-flatcontainer/netarchtest.rules/index.json)
- [System.CommandLine migration guide 2.0.0-beta5](https://learn.microsoft.com/dotnet/standard/commandline/migration-guide-2.0.0-beta5)
- [dotnet-skills System.CommandLine summary (third-party)](https://www.claudepluginhub.com/skills/wshaddix-dotnet-skills/dotnet-system-commandline)
- [HttpClient.DefaultProxy](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.defaultproxy)
- [JRACLOUD-94632: nextPageToken on initial GET](https://jira.atlassian.com/browse/JRACLOUD-94632)
- [Enhanced search discussion, Atlassian developer community](https://community.developer.atlassian.com/t/jira-cloud-rest-api-v3-search-jql-slower-fetching-with-nextpagetoken-no-totalissues-any-workarounds/90176)
- [Get Attachment Content, third-party connector docs](https://withone.ai/knowledge/jira/conn_mod_def%3A%3AGJ4qWFr3DFo%3A%3ABCaIyzo8RUelVR-ICuQw9Q)
- [Confluence attachment download with scoped token, Atlassian community](https://community.atlassian.com/forums/Confluence-questions/Attachment-dowload-authentication/qaq-p/3142082)
