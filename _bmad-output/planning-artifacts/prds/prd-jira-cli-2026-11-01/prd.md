---
title: jira-cli PRD
status: final
created: 2026-11-01
updated: 2026-11-03
---

## 1. Vision

### Problem

Engineers in the department use Claude Code for implementation and bug fixing, but Claude Code cannot read Jira. Today engineers copy ticket content, and ticket lists, from the browser and paste them into the chat by hand. This repeats for every ticket and every list, and it breaks the flow of delegating work to Claude Code.

The Jira MCP server is not an option: company policy and cost rule it out, and its permission scope is broad enough that accidental writes to Jira are a real concern.

### Vision

Accelerate loop engineering: an engineer asks Claude Code in chat to work on a ticket, and Claude Code reads what it needs from Jira by itself and carries the work through to a ready pull request.

jira-cli is the small, trustworthy bridge that makes this possible. It is **read-only by design** and **can be confined to specific projects**, so engineers and the organization can hand it to an autonomous agent without worrying about what it could change.

### Product shape

- A command-line tool that Claude Code calls from its shell.
- Runs where Claude Code runs: inside WSL or Docker containers (Linux), and also on Windows.
- Published within the department; each engineer installs it and uses it with their own Jira Cloud credentials.
- One configuration connects to one Jira Cloud site.

## 2. Glossary

- **Ticket** — a Jira issue of any type (bug, story, task, subtask, epic).
- **Agent** — Claude Code, the caller of jira-cli.
- **Allowed projects** — the Jira projects jira-cli may read, listed in the user configuration file.
- **User configuration file** — the file holding the Jira site, credentials, allowed projects, and settings such as the default list size. Maintained by the engineer, not by the agent.
- **Related ticket** — a ticket reached from another ticket through a parent, subtask, or issue link.
- **Loop** — the request-to-merged-PR workflow described in §3.

## 3. The Loop jira-cli Serves

The target workflow, end to end. jira-cli owns only the steps marked **(jira-cli)**; the rest belongs to Claude Code, git, and the engineer.

1. The engineer asks Claude Code to show unresolved tickets. Claude Code lists them **(jira-cli)**.
2. The engineer picks one. Claude Code reads the full ticket, its related tickets, attachments, and history **(jira-cli)**.
3. Claude Code asks the engineer the clarifying questions it needs.
4. Claude Code implements the change and opens a pull request.
5. After merge, Claude Code drafts the text for the Jira ticket (root cause, countermeasure), re-reading the ticket if needed **(jira-cli)**. The engineer pastes it into Jira by hand, and the task is done.

Any change to Jira (assignment, status transitions, comments) stays manual.

## 4. Features and Functional Requirements

### F1. Ticket list

- **FR-1** List tickets in one or more explicitly requested allowed projects. A request with no project, or with any project that is not allowed, is refused, and the error names the allowed projects. Results can also be filtered by any combination of: assignee (including "me"), status, issue type, priority, sprint, and labels. With no status filter, the list covers unresolved tickets. Results are most recently updated first.
- **FR-2** Return a limited number of results and page through larger result sets. The default page size comes from the user configuration file (initial value: 20); a request can never exceed an upper limit (initial value: 100).
- **FR-3** Each list entry shows enough to choose a ticket: key, issue type, summary, status, priority, assignee, last updated.

### F2. Full ticket context

- **FR-4** Retrieve one ticket by key with everything needed to implement it: summary, description, issue type, status, priority, labels, components, fix versions, reporter, assignee, created and updated dates, and all comments in chronological order.
- **FR-5** Present rich text (description, comments) as readable plain text that keeps its structure: headings, lists, code blocks, tables, links, and mentions.
- **FR-6** Include populated custom fields under their display names. A value whose shape the tool cannot render safely is omitted, unless the engineer lists that field in the user configuration file, in which case it is output as raw structured data.

### F3. Related tickets

- **FR-7** Show a ticket's parent or epic, its subtasks, and its linked tickets with the link type (for example "blocks", "duplicates").
- **FR-8** Retrieve related tickets on request rather than automatically, so the agent pulls only the depth it needs.

### F4. Attachments

- **FR-9** List a ticket's attachments: file name, size, type, author, date.
- **FR-10** Download selected attachments, or all of them, to a per-ticket folder managed by the tool, and report each file's location. A file already in that folder is replaced by the new download.

### F5. Change history

- **FR-11** Show a ticket's change history: who changed which field, from what, to what, and when.

### F6. Access control

- **FR-12** Access is limited to the allowed projects listed in the user configuration file. jira-cli reads this file only from a fixed user-level location outside any project workspace. The README documents the location, every setting, and how to protect the file from changes by the agent.
- **FR-13** Any request for a ticket or list outside the allowed projects is refused with a clear error. Related tickets that live in other projects appear by key only, marked as not accessible; their content is never retrieved.
- **FR-14** No command-line option or environment variable can widen the allowed projects or point jira-cli to a different configuration file.
- **FR-15** jira-cli never sends a write request to Jira, whatever permissions the credentials carry.

### F7. Authentication

- **FR-16** Authenticate to Jira Cloud with the engineer's own credentials: account email plus API token. Both classic API tokens and scoped API tokens are supported. The README tells engineers to use a scoped, read-only token.
- **FR-17** Credentials are kept in the user configuration file. The agent may be able to read this file; the read-only token scope limits the impact if it does.
- **FR-18** Credentials never appear in command output, error messages, or work logs.

### F8. Agent-friendly interface

- **FR-19** Output is structured so the agent can parse it reliably, and it states its format version. Fields are not renamed or removed without a version change. A separate human-readable format is not provided.
- **FR-20** Errors state what went wrong and how to fix it, distinguish categories (authentication, project not allowed, not found, network, timeout, rate limit), and return distinct exit codes.
- **FR-21** The tool describes its own commands and options through built-in help that the agent can read.
- **FR-22** jira-cli ships with a usage guide for Claude Code (for example a CLAUDE.md excerpt or a Claude Code skill) that teaches the agent how and when to use it.

### F9. Setup diagnostics

- **FR-23** A diagnostic check reports whether the configuration is complete, the credentials work (including an expired token), Jira is reachable, and each allowed project is accessible, with a fix hint for each failure.

### F10. Work log

- **FR-24** Every invocation is logged: timestamp, command and arguments, tickets touched, result (success or error category), and duration. Retrieved content itself is not logged.
- **FR-25** When a ticket is retrieved through related-ticket retrieval from another ticket (parent, subtask, link), the log always records the originating ticket key. Tickets, history, and attachments read directly by key are logged as direct access, with no originating key.
- **FR-26** Logs stay on the local machine. Losing them when a container is recreated is acceptable.

## 5. Non-Functional Requirements

- **NFR-1 Responsiveness.** Retrieving a ticket, a list, or an attachment should feel comparable to doing the same in a browser. No hard target.
- **NFR-2 Timeout.** Each request to Jira times out after 30 seconds and is reported as a timeout error (FR-20). An attachment download instead times out after 30 seconds waiting for the response or 30 seconds without progress, with no limit on total time.
- **NFR-3 Platforms.** Runs on Windows and Linux, including inside WSL and Docker containers, on both x64 and arm64.
- **NFR-4 Safety by construction.** Read-only behavior (FR-15) and the allowed-projects restriction (FR-12 to FR-14) are enforced by the tool itself, independent of token scope, and are covered by automated tests. This is a design guarantee; it is not tracked as a usage metric.
- **NFR-5 Rate limits.** jira-cli is stateless and does no client-side throttling in v1. When Jira rejects a request for rate limiting, the tool reports it as a distinct error, including the wait time if Jira provides one. The tool does not retry automatically.
- **NFR-6 Maintainability.** Any engineer in the department can add a feature. Specifications are maintained with the BMad method next to the code, development is test-first, and parts shared with a future Confluence client stay separate from Jira-specific parts.
- **NFR-7 Onboarding.** No specific setup-time target; the README plus the diagnostic check (FR-23) are sufficient.

## 6. Success Metrics

- **Smooth agent use.** Claude Code completes the Jira steps of the loop on its own; engineers no longer copy ticket content from the browser.
- **Faster loop engineering.** Engineers report that the request-to-PR loop is faster and that they run it more often.

Both are gathered through individual surveys of the engineers who use the tool. Survey timing and questions are deferred (see §8).

### Counter-metrics

- Engineers falling back to copying from the browser (a sign of missing data or poor output). To be covered by a survey question asking whether, and when, they still copy from the browser.
- Any incident where a credential is exposed.

Writes to Jira and access outside the allowed projects are prevented by design (NFR-4) rather than measured.

## 7. Non-Goals

- Any write to Jira: creating, editing, commenting, assigning, transitioning, or logging work.
- Generating the root-cause and countermeasure text for Jira; Claude Code writes it.
- Saved filter presets, free-form JQL search, and similar-ticket search (candidates for a later version).
- Discovering which fields a ticket type requires.
- Connecting to more than one Jira site per configuration.
- Jira Server / Data Center.
- Confluence access in v1. Shared parts should stay reusable so a Confluence client can follow.

## 8. Open Questions

- Distribution method (single-file binary, .NET tool, or dev-container image). Deferred to architecture; options in the addendum.
- Fixed user-level location of the configuration file on Windows, WSL, and Docker, and the protection steps the README describes. Deferred to architecture; ideas in the addendum.
- Survey timing, baseline, and questions for the success metrics. Deferred; owner: Mike; revisit before the department rollout.
