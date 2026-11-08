---
id: SPEC-jira-cli
companions:
  - ../../planning-artifacts/architecture/architecture-jira-cli-2026-11-01/ARCHITECTURE-SPINE.md
  - glossary.md
  - agent-loop.md
  - setup-reference.md
sources:
  - ../../planning-artifacts/prds/prd-jira-cli-2026-11-01/prd.md
  - ../../planning-artifacts/prds/prd-jira-cli-2026-11-01/addendum.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# jira-cli

## Why

A pain to solve. Engineers in the department delegate implementation and bug fixing to Claude Code, but Claude Code cannot read Jira, so engineers copy tickets and ticket lists from the browser into the chat for every task. The Jira MCP server is ruled out by company policy, cost, and its broad write permissions. jira-cli is a read-only Jira Cloud CLI, confined to allowed projects, that an engineer can hand to an autonomous agent so it reads what it needs and carries a ticket through to a ready pull request.

## Capabilities

- **CAP-1** Ticket list (FR-1 to FR-3)
  - **intent:** The agent lists tickets in one or more named allowed projects, filtered by assignee (including "me"), status, issue type, priority, sprint, and labels, to choose one to work on.
  - **success:** `list` returns at most the page size of entries with key, issue type, summary, status, priority, assignee, and last updated; unresolved only (`statusCategory != Done`) when no status filter is given; most recently updated first; with an opaque cursor for the next page. A missing project, or any disallowed one among several, refuses the whole request with exit 5 before any HTTP call, with the allowed keys in the hint. A limit above `maxPageSize` exits 2.
- **CAP-2** Full ticket context (FR-4 to FR-6)
  - **intent:** The agent reads one ticket of any type with everything needed to implement it.
  - **success:** `get <key>` returns summary, description, issue type, status, priority, labels, components, fix versions, reporter, assignee, created and updated dates, and all comments oldest first. Description and comments are Markdown that keeps headings, lists, code blocks, tables, links, and mentions. Populated custom fields appear as an ordered `[{ name, value }]` rendered by schema type; other types are omitted unless the field is listed in `customFieldPassthrough`. The output carries no relations.
- **CAP-3** Related tickets (FR-7, FR-8, FR-13)
  - **intent:** The agent pulls, on request and one level at a time, the parent, children (subtasks and tickets under an epic), and linked tickets of any ticket.
  - **success:** `related <key>` lists each relation once per key and relationship, with link type; relations outside the allowed projects appear by key only with `accessible: false`; each disallowed child project adds one notice asking the engineer to add it, and the command still exits 0. `related <origin> --key <k>` returns `k`'s full content only when `k` is allowed and related; otherwise it exits 5 or 6.
- **CAP-4** Attachments (FR-9, FR-10)
  - **intent:** The agent lists a ticket's attachments and downloads selected ones or all of them.
  - **success:** `attachments list` shows file name, size, type, author, and date. `attachments download` by `--name`, by `--id` matched against the ticket, or all, saves into the managed per-ticket folder, replaces existing files, and reports each file's absolute path as `downloaded` or `failed`. Among duplicate names in a download of all, only the newest is saved. An ambiguous name exits 2 listing the candidates. No option writes anywhere else.
- **CAP-5** Change history (FR-11)
  - **intent:** The agent sees who changed which field, from what, to what, and when.
  - **success:** `history <key>` returns every change across all pages. From and to values appear only for allow-listed system fields and displayable custom fields; relation fields show keys; every other field shows its name and timestamp only.
- **CAP-6** Access control (FR-12 to FR-15, NFR-4)
  - **intent:** jira-cli itself enforces read-only access and the allowed projects, independent of token permissions.
  - **success:** Automated tests on Windows and Linux prove that only GET is ever sent; that tickets, moved tickets, list results, children, and embedded data from other projects never yield content; and that no option or environment variable changes the config path or the allowed projects, including hostile `HOME`, `XDG_*`, `USERPROFILE`, `APPDATA`, and `LOCALAPPDATA`.
- **CAP-7** Authentication (FR-16 to FR-18)
  - **intent:** jira-cli authenticates with the engineer's own email and API token, classic or scoped, and never exposes the token.
  - **success:** A classic token through the site URL and a scoped token through the `cloudId` gateway both authenticate, and credentials go only to that base URL. 401 and 403 exit 4 with a hint naming expiry, missing scope, and `cloudId`. In tests, a planted token never reaches stdout, stderr, or the log, including through `@file` arguments.
- **CAP-8** Agent-friendly interface (FR-19 to FR-22)
  - **intent:** The agent parses every result reliably, tells error kinds apart, and learns the tool from its help and a shipped usage guide.
  - **success:** Every invocation writes one JSON envelope with `formatVersion` to stdout; only `--help` and `--version` print plain text. Error categories map to the fixed exit codes 0 to 10; a rate-limit rejection exits 9 with `retryAfterSeconds` when Jira provides it. Golden tests pin each command's output. `--help` documents every command and option. The usage guide ships in the package and covers the topics AD-22 lists.
- **CAP-9** Setup diagnostics (FR-23)
  - **intent:** The engineer runs one check that says what is wrong with the setup and how to fix it.
  - **success:** `doctor` reports config completeness, whether credentials work (including an expired token), Jira reachability, each allowed project's accessibility, and the endpoint in use, with a fix hint per failure. It runs even when the config is invalid.
- **CAP-10** Work log (FR-24 to FR-26)
  - **intent:** Every invocation leaves a local record of what was accessed, without the content.
  - **success:** Each invocation except `--help` and `--version` appends one JSON line with timestamp, command, redacted arguments, tickets touched, origin, result category, and duration to a local UTC daily file. No response content is logged. `related <origin> --key <k>` records the origin. A log failure writes a stderr warning and leaves stdout and the exit code unchanged.
- **CAP-11** Install and setup (NFR-3, NFR-7, FR-12, FR-16)
  - **intent:** An engineer installs, configures, and protects jira-cli in WSL, Docker, or Windows from the README alone.
  - **success:** One `.nupkg` installs with `dotnet tool install -g jira-cli --source <folder>` and runs on Windows and Linux, x64 and arm64. The README covers everything AD-22 lists, plus the config location and every setting, proxies, and root certificates. `doctor` passes after following it.

## Constraints

- Architecture per the adopted spine: hexagonal on a Clean Architecture-style split, and AD-1 to AD-22 bind all work.
- Read-only by type: the transport can only send GET (FR-15, AD-5).
- Every Jira call goes through `JiraGateway`, which checks project access before and after the call (AD-4, AD-7).
- The config path is fixed and resolved from the OS user record, never from environment variables or options (FR-14, AD-11).
- Credentials never appear in output, errors, or logs (FR-18, AD-12).
- C# on .NET 10, Windows and Linux, x64 and arm64, including WSL and Docker (NFR-3).
- Standard .NET DI with interface-based, injectable services; test-first; tests never call real Jira (NFR-6, AD-20).
- `Atlassian.Common` holds no Jira concepts, so a Confluence client can reuse it (NFR-6, AD-2).
- Jira Cloud REST API v3 only, one site per config.
- Stateless: no client-side throttling and no automatic retries (NFR-5).
- JSON requests time out after 30 seconds; attachment downloads after 30 seconds waiting for the response or 30 seconds without progress (NFR-2, AD-19).
- Distributed as one framework-dependent .NET tool from a shared folder source; every environment has the .NET 10 SDK; no internal NuGet server exists (AD-21).
- One engineer per install, an internal tool, and agents never run as root.

## Non-goals

- Any write to Jira: creating, editing, commenting, assigning, transitioning, or logging work.
- Generating the root-cause and countermeasure text for Jira; Claude Code writes it.
- Saved filter presets, free-form JQL, and similar-ticket search.
- Discovering which fields a ticket type requires.
- More than one Jira site per config; Jira Server and Data Center.
- Confluence access in v1.
- A human-readable output format.
- Downloading attachments to a caller-chosen directory.
- Stopping an agent that reads the token and calls Jira by other means, or that changes the process environment, the Windows registry, or the managed folder during a download (AD-3).
- A runtime check that the config file is writable.
- A hard responsiveness target; retrieval should feel comparable to the browser (NFR-1).

## Success signal

- On a department Jira site, an engineer asks Claude Code to list unresolved tickets in a project and then work on one; Claude Code completes loop steps 1, 2, and 5 through jira-cli with no browser copy-paste, and the AD-20 safety suites pass on Windows and Linux.
- In surveys, engineers report that the request-to-PR loop is faster, that they run it more often, and that they no longer fall back to copying from the browser; no credential exposure occurs.

## Open Questions

- Do Jira's live behaviors match the published spec: `redirect=false` with Basic auth, attachment access with a scoped token, `parent =` across projects, `expand=schema` type names, comment `total`, and the `statusCategory` filter? Decide in the first Gateway and attachment stories.
- Where is the shared folder source that agent environments cannot write to, and which CI provider runs the release gate? Decide before department rollout.
- What are the survey timing, baseline, and questions? Owner: Mike, before department rollout.
