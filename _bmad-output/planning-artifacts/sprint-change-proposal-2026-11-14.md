---
status: approved
date: 2026-11-14
scope: moderate
trigger: Engineer request before starting Story 1.2
---

# Sprint Change Proposal: Build the Jira connection first in Epic 1

## 1. Issue Summary

Story 1.1 is done; Story 1.2 (JSON envelope) has not started. Mike wants to confirm that jira-cli can talk to the real department Jira, and to step through the transport under a debugger, before the upper layers (envelope, work log, Gateway, `doctor`) are built on top of it.

Today the first real Jira call happens only in Story 1.7, the last story of the epic. A surprise there (authentication behavior, endpoint shape, scoped-token gateway) would land after five stories that assume the documented behavior.

Issue type: a change in sequencing chosen to reduce risk. Requirements are unchanged.

## 2. Impact Analysis

**Epic impact.** Only Epic 1 changes: its stories are reordered and renumbered, and a few acceptance criteria move between stories. Epics 2 to 5 are unchanged apart from one cross-reference in Story 4.2.

**Dependencies the new order must resolve.**

| Dependency in the current plan | Resolution |
|---|---|
| The `doctor` config check (current 1.3) uses the envelope (current 1.2) | Move it to Story 1.7, which builds `doctor`. The config story returns a load failure as a value. |
| "Other commands raise `config` (exit 3)" (current 1.3) needs `ExceptionMapper` | Move it to the envelope story (new 1.4). |
| 3xx "maps to `unexpected`" (current 1.5) needs `ExceptionMapper` | The transport story raises a `TransportException` carrying the status. The mapping is verified in new 1.4 and 1.6. |

Two existing interactions get simpler: the config now exists before the envelope story, so "load config before reporting a parse error" and the redactor's need for the token both build on finished code.

**Artifact conflicts.**

- PRD: none. MVP unaffected.
- Architecture spine: AD-20 says "Tests never call real Jira." The new live-check tool is not a test, and one sentence states this. No decision changes.
- UX: none.
- Code: one comment in `tests/JiraCli.ArchitectureTests/ConsoleUseTests.cs` names "Story 1.2" for the envelope writer.
- `spec-1-1-solution-skeleton-with-architecture-guards.md` is a done-story record that uses the old numbers. It stays unchanged as history.
- `deferred-work.md` points the `ValidateOnBuild` pin at "Story 1.2". New 1.2 (config) is the first story that registers real services, so the pointer stays correct as written.

## 3. Recommended Approach

**Direct adjustment** inside Epic 1: reorder, renumber, move acceptance criteria, and add a manual live-check tool to the transport story.

| Old | New | Story |
|---|---|---|
| 1.1 | 1.1 | Solution skeleton with architecture guards (done) |
| 1.3 | **1.2** | Fixed config location and config loading |
| 1.5 | **1.3** | GET-only transport with contained credentials, plus the live-check tool |
| 1.2 | **1.4** | JSON envelope, error categories, and exit codes |
| 1.4 | **1.5** | Secret redaction and the work log |
| 1.6 | 1.6 | `JiraGateway` with scope checks |
| 1.7 | 1.7 | `doctor` checks config, credentials, reachability, and project access |

- Effort: low (document edits only; no code is undone).
- Risk: low. The live check moves the riskiest unknown (real authentication on both endpoint kinds) from the last story to the third.
- Timeline: unchanged; the same work happens in a different order, plus one small console tool.

Alternative considered: a throwaway spike before Story 1.2. Rejected by Mike in favor of debugging the real transport code.

## 4. Detailed Change Proposals

### 4.1 `epics.md`

Reorder the Epic 1 stories to the table above and renumber their headings. Story text not listed below is unchanged.

**New Story 1.2 (was 1.3).**

Title. OLD: `Fixed config location and the doctor config check`. NEW: `Fixed config location and config loading`.

Requirements. OLD: `FR-12, FR-14, FR-17, FR-23 (config check), AD-11`. NEW: `FR-12, FR-14, FR-17, AD-11`.

"So that" clause. OLD: `…and \`doctor\` to tell me what is wrong with it, So that the agent cannot redirect it to another config and I can fix setup mistakes quickly.` NEW: `…and to report exactly which key is wrong, So that the agent cannot redirect it to another config and setup mistakes are easy to fix.`

Linux lookup failure. OLD: `**And** if the lookup fails, the command fails with \`config\` (exit 3) and never falls back`. NEW: `**And** if the lookup fails, loading returns a \`config\` failure and never falls back`.

Last criterion. OLD:

```
**Given** a missing, unreadable, or invalid config
**When** I run `jira-cli doctor`
**Then** the envelope reports a failed config check with a fix hint instead of aborting the command
**And** a valid config reports the config check as passed, while other commands raise `config` (exit 3) on load failure
```

NEW:

```
**Given** a missing, unreadable, or invalid config
**When** it is loaded
**Then** loading returns a failure value, not an exception, naming the cause or the offending key and never its value
**And** Story 1.4 turns that value into `config` (exit 3) for commands, and Story 1.7 reports it as the `doctor` config check
```

Rationale: removes the dependency on the envelope and on `doctor`.

**New Story 1.3 (was 1.5).**

Title. OLD: `GET-only transport with contained credentials`. NEW: `GET-only transport with contained credentials and a live check`.

"I want" clause. Append: `, and to confirm against my real Jira that it connects`.

Redirect criterion. OLD: `**Then** no redirect is followed and it raises a \`TransportException\` that maps to \`unexpected\``. NEW: `**Then** no redirect is followed and it raises a \`TransportException\` carrying the status (Stories 1.4 and 1.6 map it to \`unexpected\`)`.

Added criterion:

```
**Given** the engineer's real config and the department Jira site (manual live check run by the engineer; never run by `dotnet test` or the release gate)
**When** the engineer runs `tools/JiraCli.LiveCheck`, a console app in the solution that is never packaged, once with a classic token on the site URL and once with a scoped read-only token through the `cloudId` gateway
**Then** it loads the config through Story 1.2's loader, sends `myself` and one `project/{key}` request per allowed project through `IAtlassianTransport`, and prints the endpoint in use, each status, and each response body, never the token or the `Authorization` header
**And** it runs under the debugger with no extra setup
**And** any difference from documented behavior is recorded and amends AD-12 before Story 1.6 starts
```

Rationale: Mike's manual debugging entry point. The tool lives under `tools/`, which the architecture tests (scoped to `src/`) do not scan, and it is not product code.

**New Story 1.4 (was 1.2).** Added criterion:

```
**Given** a config load failure from Story 1.2
**When** any command other than `doctor` runs
**Then** the envelope has category `config` and the process exits 3
**And** the config is loaded before a parse failure is reported
```

Rationale: these move here from old 1.3, because this story owns `ExceptionMapper`.

**New Story 1.5 (was 1.4).** OLD: `resolved as in Story 1.3`. NEW: `resolved as in Story 1.2`.

**Story 1.7.**

Title. OLD: `` `doctor` checks credentials, reachability, and project access ``. NEW: `` `doctor` checks config, credentials, reachability, and project access ``.

Added criterion (moved from old 1.3):

```
**Given** a missing, unreadable, or invalid config
**When** I run `jira-cli doctor`
**Then** the envelope reports a failed config check with a fix hint instead of aborting the command
**And** a valid config reports the config check as passed
```

Live-check criterion. OLD: `**And** both authenticate and every allowed project is reported accessible, and any difference from documented behavior is recorded and amends AD-12`. NEW: `**And** both authenticate and every allowed project is reported accessible, and any difference from the Story 1.3 live-check findings is recorded and amends AD-12`.

**Story 4.2.** OLD: `resolved as in Story 1.3`. NEW: `resolved as in Story 1.2`.

### 4.2 `sprint-status.yaml`

Replace the Epic 1 story keys, in this order:

```yaml
  1-1-solution-skeleton-with-architecture-guards: done
  1-2-fixed-config-location-and-config-loading: backlog
  1-3-get-only-transport-with-contained-credentials-and-a-live-check: backlog
  1-4-json-envelope-error-categories-and-exit-codes: backlog
  1-5-secret-redaction-and-the-work-log: backlog
  1-6-jiragateway-with-scope-checks: backlog
  1-7-doctor-checks-config-credentials-reachability-and-project-access: backlog
```

### 4.3 `epic-1-context.md`

Update the story list to the new numbers and titles, and replace the "Cross-Story Dependencies" bullets with:

- 1.1 provides the structure and architecture-test harness every later story extends.
- 1.2's user-path resolution (passwd home / known folders, env ignored) is reused by 1.5 for the log location and by Epic 4 for the attachment folder. Its load failure is a value: 1.4 turns it into `config` (exit 3) and 1.7 reports it as the `doctor` config check. 1.2 registers the first real services, so it pins `ValidateOnBuild`/`ValidateScopes` (deferred from 1.1).
- 1.3 depends on 1.2's `ConnectionSettings`. It adds `tools/JiraCli.LiveCheck`, an engineer-run live check against the real site with both token kinds; its findings amend AD-12 before 1.6 starts.
- 1.4's `Program` wrapper, `EnvelopeWriter`, and `ExceptionMapper` are reused by 1.5 (the wrapper also writes the log) and 1.7. Ctrl+C behavior is decided and tested in 1.4. Config loads before parse errors are reported or logged (1.4/1.5 interaction).
- 1.5's redactor needs the token from 1.2's config load.
- 1.6 depends on 1.3's transport and `TransportException`; 1.7 depends on 1.2, 1.3, 1.4, and 1.6 (`GetMyselfRequest`, `GetProjectRequest`).
- The remaining scope kinds bullet is unchanged.
- 1.7 repeats the live check through `doctor` with both token kinds; any difference from 1.3's findings is recorded and amends AD-12.

In "Requirements & Constraints", OLD: `Tests never call real Jira.` NEW: `Tests never call real Jira; the engineer-run live-check tool is not a test.`

### 4.4 Architecture spine (AD-20)

After `Tests never call real Jira.` add: `The engineer-run live-check tool under \`tools/\` is not a test: it is never packaged and never run by the test suites or the release gate.`

### 4.5 Code comment

`tests/JiraCli.ArchitectureTests/ConsoleUseTests.cs:16`. OLD: `Story 1.2 adds the envelope writer and the warning writer.` NEW: `Story 1.4 adds the envelope writer and the warning writer.`

## 5. Implementation Handoff

- **Scope:** Moderate (backlog reorganization). No PM or Architect replan needed.
- **Developer agent:** apply sections 4.1 to 4.5, then start the new Story 1.2 with `bmad-build`.
- **Mike:** run the Story 1.3 live check against the department Jira with both token kinds, and record any findings.
- **Git:** the empty branch `story/1-2-json-envelope` no longer matches its story; rename it to `story/1-2-config-loading`.

**Success criteria:** the Epic 1 order in `epics.md`, `sprint-status.yaml`, and `epic-1-context.md` agree; no story depends on a later story; `dotnet build` and `dotnet test` stay green after the comment change; Mike can step through the transport against real Jira at the end of Story 1.3.
