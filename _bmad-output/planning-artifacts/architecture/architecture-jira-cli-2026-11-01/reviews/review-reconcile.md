---
type: reconcile-review
spine: ../ARCHITECTURE-SPINE.md
decision_log: ../.memlog.md
inputs:
  - ../../../prds/prd-jira-cli-2026-11-01/prd.md
  - ../../../prds/prd-jira-cli-2026-11-01/addendum.md
date: '2026-11-01'
---

# Reconcile Review: jira-cli Spine vs PRD and Addendum

## Verdict

**Amend the spine, then proceed.** The hard guarantees landed well: read-only (FR-15), the allow-list (FR-12 to FR-14), credential containment, and the output contract are all structurally enforced. Every non-goal is respected, and two of the three open questions are closed or correctly out of scope.

The gaps are in what surrounds those guarantees:

- The user's `--project` override has one unresolved consequence for the first step of the loop.
- One explicit addendum constraint (shared logging and output) did not land.
- Several user-stated conditions live only in the decision log, where story authors will not see them.

Counts: 1 high, 5 medium, 7 low.

Not reported as new, as instructed: the FR-1 no-filter override and the FR-10 overwrite refinement. Their knock-on effects are covered in R1, R8, and the walk below.

## Findings

| ID | Severity | Finding | Source |
| --- | --- | --- | --- |
| R1 | High | After `--project` became mandatory, the agent has no defined way to learn the allowed projects | FR-1 override, §3 step 1, §6, FR-17, FR-20 |
| R2 | Medium | Shared logging and output formatting are not in `Atlassian.Common`; the spine also disagrees with itself on `EnvelopeWriter` | Addendum constraints, NFR-6 |
| R3 | Medium | The README protection steps and "the agent must not modify the config" did not land; the open question is only half closed | FR-12, PRD §8 OQ2, addendum, memlog line 34 |
| R4 | Medium | FR-25's "always" depends on agent behavior, and the user's condition for accepting that is missing from the spine; `history` and `attachments` carry no origin | FR-25, FR-22, memlog line 39 |
| R5 | Medium | FR-13 key-only protection does not cover custom fields, and the F2 row does not bind AD-10 | FR-13, FR-6, AD-3, AD-16 |
| R6 | Medium | The required architecture review and the department explainer have no home in the spine (`companions: []`) | Addendum, memlog line 11 |
| R7 | Low | `--project` not being a parser-required option means generated help may present it as optional | FR-21, AD-9 |
| R8 | Low | The unresolved default was widened to "no `--status`", which silently hides resolved tickets on filtered lists | FR-1, AD-9 |
| R9 | Low | AD-19 has no total time limit, but NFR-2 says each request times out after 30 seconds; the PRD text is not updated | NFR-2, AD-19 |
| R10 | Low | Redaction is not specified for stderr warnings | FR-18, AD-12, AD-13 |
| R11 | Low | "Every invocation is logged" is unspecified for parser errors, `--help`, and concurrent runs | FR-24, AD-18 |
| R12 | Low | The authentication hint does not cover the known scoped-token mistake (no `cloudId`) | FR-16, FR-20, FR-23, addendum |
| R13 | Low | `related --key` outcome is ambiguous for a disallowed key; two memlog assumptions became rules without confirmation | FR-13, AD-10, AD-9, AD-17 |

---

### R1 (High): The agent cannot discover which projects to pass

**PRD.** §3 step 1: "The engineer asks Claude Code to show unresolved tickets. Claude Code lists them." §6: Claude Code completes the Jira steps on its own.

**Spine.** AD-9 requires `--project`. The CLI surface is `list`, `get`, `related`, `attachments list`, `attachments download`, `history`, `doctor`. None of them is defined to return the allowed projects. AD-14 fixes the error category but not what the hint contains.

**Why it is a real gap.** The agent's only sources for the project key are these, and none is specified:

- **Reading the config file.** This puts the API token into the conversation. FR-17 tolerates that as a risk, but it should not become the routine path. The addendum's Claude Code `deny` rule could also block it, depending on how the README words the rule (see R3).
- **`doctor`.** It reports on allowed projects, but its layout is deferred, it makes several network calls, and it is a diagnostic rather than a discovery step.
- **Asking the engineer on every first list.** This adds a step to the loop the override did not intend to change, and it counts against "smooth agent use".
- **The usage guide or per-repo CLAUDE.md.** Workable, but the shipped guide cannot know each engineer's projects.

The memlog entry for the override (line 24) records the requirement but not how the agent satisfies it.

**Fix.** Pick one and state it in AD-9:

1. The `projectNotAllowed` hint always names the allowed keys and the `--project` option. This is smallest, and it satisfies FR-20's "how to fix it" for the missing-option case. Allowed keys are not secret, since FR-17 already accepts the agent reading them.
2. Add a dedicated local command, such as `projects`, that makes no HTTP call. This needs a CLI surface change.

Then have FR-22's guide teach the flow.

**Other PRD text affected by the override:**

| PRD text | Effect | Action |
| --- | --- | --- |
| FR-1, "any combination of: project, ..." and "With no filter ..." | Project moves from filter to required; the no-filter case no longer exists | Update the PRD (memlog line 24 promised the offer at finalize; PRD is still `status: final` with the old text) |
| §3 step 1 | Still reads as a no-argument request. It remains valid from the engineer's view only if R1 is resolved | Verify after the R1 decision |
| §6 Smooth agent use, and the browser-fallback counter-metric | Directly exposed by R1 | None beyond R1 |
| FR-13, "outside the allowed projects is refused" | A missing project is not "outside", yet it uses the same category. Acceptable, but the message must distinguish missing from disallowed | Require this in AD-9 |
| FR-20, "how to fix it" | The missing-project hint must be actionable | Covered by the R1 fix |
| FR-21 | See R7 | R7 |
| FR-22 | The guide must teach `--project` | See R4 |
| FR-2, FR-3, FR-14, FR-23, addendum | Not affected | None |

---

### R2 (Medium): Shared logging and output formatting did not land in the shared project

**Addendum (restated in memlog line 12).** "Shared concerns (HTTP client, auth, config, logging, output formatting) should be modular so a future Confluence client can reuse them." NFR-6 agrees.

**Spine.**

- **Logging.** The work log writer sits in `JiraCli.Infrastructure` (layer table, seed `WorkLog/`). The middleware is in `JiraCli.Cli` (AD-18). `IInvocationContext` is a `Domain` port. Only the log path resolution is shared (AD-11). A Confluence client would have to rewrite the writer, the line format, and the middleware.
- **Output.** The layer table lists "envelope" under `Atlassian.Common`, and the seed has `Common/Output/`. AD-13 and the seed put `EnvelopeWriter` in the Cli layer, and the layer table places the exception-to-envelope mapping there too. It is not stated which part is shared, so builders can diverge. The exit code table is equally reusable, and its home is not stated.

**Fix.** State the split in AD-2 or AD-18. For example:

- Common holds the envelope type, the writer, the base error categories, the log line format and writer, and an invocation-context abstraction with a generic "resources touched" field.
- Cli holds only Jira mapping and the composition.

If the user prefers to leave logging in `JiraCli.*` for v1, record that as an explicit decision that amends the addendum constraint.

---

### R3 (Medium): Config protection and "the agent must not modify it" were dropped

**Inputs.**

- FR-12 requires the README to document "how to protect the file from changes by the agent".
- PRD §8 OQ2 explicitly defers "the protection steps the README describes" to architecture.
- The addendum lists four ideas: outside the workspace, Docker `:ro`, other-user ownership, and Claude Code `deny` rules.
- Memlog line 34 records the user constraint: reading by the agent is acceptable, modifying is not.

**Spine.** AD-11 closes the location half of OQ2. The protection half appears nowhere. "The tool only reads this file" is about the tool, not the agent. AD-3 covers an agent bypassing the tool with the credentials, but not an agent editing `allowedProjects`, which is the actual threat this requirement names. As written, AD-3 could be read as putting that out of scope.

**Knock-on effects.**

- **The `deny` rule wording matters.** The user's constraint implies denying edits and writes. Denying reads as well would remove one discovery route in R1. The README should state which is intended.
- **AD-11 and containers.** Having no passwd entry produces a `config` failure with no fallback. A container started with an arbitrary `--user` UID hits this, and the `:ro` mount target depends on the same home. The memlog raised this as an open question (line 47), then decided it (line 52). The decision is sound for FR-14, but the README needs to cover it and `doctor` needs a specific hint, or NFR-3 and NFR-7 suffer in the Docker case. The Deferred table does not list it.

**Fix.** Add one sentence to AD-3 or AD-11 stating that preventing agent modification is the engineer's setup responsibility through the README, not something the tool enforces, and list the required steps per environment. Record the addendum's rejected alternative too, so no one adds a writable-file warning to `doctor`: "No runtime check for a writable config file."

---

### R4 (Medium): FR-25 relies on the agent, and the condition for that is missing

**PRD.** FR-25: when a ticket is retrieved by following a relationship, "the log always records the originating ticket key". The addendum accepts that the request must carry the origin.

**Spine.** AD-10 implements this through `related <origin> --key <k>`. Plain `get` is logged as direct access. So if the agent reads a linked ticket with `get`, the log shows no origin, and FR-25's "always" is not met. No tool mechanism can detect this, because the CLI is stateless.

**Quiet condition dropped.** Memlog line 39: "Condition from user: Claude Code must be able to follow this; usage guide (FR-22) teaches it." AD-10 does not mention it. No AD binds FR-22. The Deferred table says the guide "cannot cause divergence", which is true of its form but not of its required content. That content is currently scattered or held only in the memlog:

- Use `related` for traversal (memlog line 39).
- `--project` is required, and how to obtain the keys (R1).
- Download to a ticket-dedicated directory (AD-17).
- The threat boundary (AD-3, if it applies to the guide).
- The unresolved default (R8).

**Further gap: other sub-resources.** The loop reads related tickets, attachments, and history (§3 step 2). `history <k>` and `attachments ... <k>` for a related ticket have no origin parameter, so these reads are logged as direct access even when `k` was reached through a relationship. Either FR-25 means ticket content only, or these commands need an origin. The spine should say which.

**Fix.**

- Add to AD-10 that FR-25 is guaranteed only for traversal through `related`, and that the usage guide is the mechanism for the rest. Ideally have the PRD acknowledge this by softening "always".
- Add a required-content list for the guide, keeping the form deferred.
- Decide the `history` and `attachments` origin question.

---

### R5 (Medium): FR-13 protection does not reach custom fields

**PRD.** FR-13: related tickets in other projects appear by key only, and "their content is never retrieved". AD-3 makes this absolute: no Jira response can make the tool read outside the allowed projects.

**Spine.** AD-10's `RelatedIssueProjection` covers parent, subtask, and link data. AD-16 maps any unrecognized custom field shape "to its JSON value". A custom field holding an issue reference, such as an issue-picker field or a hierarchy parent field, could carry an embedded summary or status from a disallowed project and pass straight through. This is plausible from general Jira knowledge, but the exact payloads are unverified and need checking against a real site.

**Related seam.** The capability map's F2 row is governed by AD-7, AD-8, and AD-16, not AD-10. Yet `GetIssueRequest` is where parent, subtask, and link data arrive. If `get` includes these (the spine does not say; FR-7 only says "show"), a builder following the map could miss the projection.

**Fix.**

- Add AD-10 to the F2 row, and state whether `get` includes relationships.
- In AD-16, reduce any custom field value recognized as an issue reference through `RelatedIssueProjection`, or restrict the JSON fallback.
- Add a post-check test case to AD-20's required suite.

(The projection itself necessarily receives embedded data from Jira before stripping it. Reading FR-13's "never retrieved" as "never leaves `Domain`" is reasonable and not reported as a contradiction.)

---

### R6 (Medium): Architecture review gate and explainer have no home

**Inputs.**

- Addendum: "Architecture-level decomposition must be reviewed before implementation starts."
- Memlog line 11, a user direction: a department-facing explainer in Markdown with mermaid diagrams, "especially class diagram and architecture diagram", for veteran developers.

**Spine.** `companions: []`, and the folder holds only the spine and the memlog. The spine has flow, sequence, and deployment diagrams but no class diagram. It has no statement of a review gate, though `status: draft` is compatible with one.

This may simply be scheduled for after this review. It is reported so that it is not lost. The explainer is also the natural home for the NFR-6 "any engineer can add a feature" walkthrough (which files a new command touches), which the spine does not provide.

**Fix.** List the explainer as a companion before calling the spine final, and record who reviews it before implementation starts.

---

### Low findings

**R7. Help versus required `--project`.** AD-9 forbids marking `--project` as parser-required so that the error category stays `projectNotAllowed`. Generated help would then likely show it as optional, which misleads an agent relying on FR-21. Fix: require the option description to state that it is required, and pin this with a test.

**R8. Unresolved default widened.** FR-1 applied the unresolved default to "no filter". AD-9 applies it whenever `--status` is absent (memlog line 52, a distill-time addition, not a recorded user decision). This is the sensible reading after the override, but `list --project X --label foo` silently excludes resolved tickets, nothing in the output says so, and listing both requires naming every status. An agent could wrongly conclude no such ticket exists. Fix: state it in help and the guide, and confirm with the user.

**R9. NFR-2 text.** NFR-2: "Each request to Jira times out after 30 seconds." AD-19: 30 seconds to headers plus 30 seconds per stalled read, "no total limit". The memlog (line 41) records this as an attachment decision, but AD-19 applies it to all requests. A deliberate refinement, but unlike FR-1 and FR-10 it was not in the known list. Fix: update the PRD, and confirm that the wider application is intended.

**R10. Redaction on stderr.** AD-12 applies `ISecretRedactor` at the envelope writer and work log writer. AD-13 and the conventions define a separate warning writer, which is not covered. FR-18 includes command output and error messages, and a credential exposure is a named counter-metric. Fix: add the warning writer to AD-12.

**R11. FR-24 boundaries.** "One JSON line per invocation" does not say whether parser errors, `--help`, and `--version` are logged, or how they pass through the middleware. Concurrent invocations appending to one daily file could also lose lines, particularly under Windows file locking, and AD-18 turns such a failure into a warning only. Claude Code may run commands in parallel. Also, as recalled and unverified, System.CommandLine 2.0 GA has removed its built-in middleware API, so this would be a wrapper around parse and invoke. Confirm this during the story. Fix: one sentence each on scope and append mode.

**R12. Scoped token without `cloudId`.** The addendum says scoped tokens must go through the gateway. With no `cloudId`, AD-12 uses the site URL, so the failure presumably arrives as `authentication`. The AD-14 hint names "invalid or expired token, or missing scope", not this setup mistake. FR-16 and FR-23 make this a likely first-run failure. Fix: add it to the hint and to the `doctor` check. Rotation guidance for expiring tokens (addendum) also belongs in the README list.

**R13. Precedence and unconfirmed assumptions.**

- AD-10 says `related <origin> --key <k>` "otherwise fails with `notFound`". For a disallowed `k`, FR-13 and AD-7 point to `projectNotAllowed`. State that the pre-check runs first.
- Multi-project all-or-nothing refusal (memlog line 26) and newest-wins for duplicate attachment names (memlog line 44) are tagged `(assumption)` in the log but appear as flat rules in AD-9 and AD-17. Confirm with the user or retag them.

---

## Spine vs PRD contradictions

| Item | Status |
| --- | --- |
| FR-1 no-filter default vs AD-9 | Known and intentional; PRD text still stale |
| FR-10 overwrite vs AD-17 | Known refinement. "Never overwritten silently" still holds because `updated` is reported |
| NFR-2 vs AD-19 | **New**, low, deliberate (R9) |
| FR-25 "always" vs AD-10 direct `get` | **New**, medium (R4) |
| Addendum shared logging vs AD-18 | **New**, medium (R2) |
| FR-13 "by key only" vs AD-16 JSON fallback | **New**, medium, edge-dependent (R5) |
| Addendum distribution lean (self-contained binary) vs AD-21 | Not a contradiction: the lean was explicitly left to architecture, and the user decided (memlog lines 37-38) |
| Addendum `--add-source` vs AD-21 `--source` | Not a contradiction: deliberate, with the reason recorded |

**PRD updates owed:** FR-1, FR-10, NFR-2, and FR-25 if softened. §8 can also close OQ1 and OQ2 once R3 is done.

## Coverage walk

"OK" means landed, or legitimately left to stories because it cannot cause divergence.

### Functional requirements

| Req | Spine | Result |
| --- | --- | --- |
| FR-1 | AD-9, conventions | Override known; see R1, R7, R8. The filter set is not enumerated, and only `--project`, `--status`, `--assignee` are named. OK for one story, but sprint value semantics (name or id) and non-"me" assignee need a story decision |
| FR-2 | AD-19, config schema | OK |
| FR-3 | F1 map, deferred layout | OK |
| FR-4 | AD-8, AD-19 | OK. Chronological order is left to the story and golden test |
| FR-5 | AD-16 | OK |
| FR-6 | AD-16 | OK except R5. Minor: AD-16 renders user fields as a display name while the conventions say `{ displayName, accountId }`; AD-16 reads as the deliberate exception for custom field values |
| FR-7 | AD-10 | OK. Epic relationship relies on `parent`; confirm for the site. Whether `get` includes it: R5 |
| FR-8 | AD-10 | OK |
| FR-9 | AD-8, F4 map | OK |
| FR-10 | AD-17 | Known refinement, OK. Watch: a filesystem with coarse timestamps could truncate the saved time below Jira `created`, reporting `updated` on every re-download |
| FR-11 | AD-8, AD-19 | OK |
| FR-12 | AD-11 | Location OK; protection R3 |
| FR-13 | AD-7, AD-8, AD-10 | OK except R5, R13 |
| FR-14 | AD-11, AD-20 | OK |
| FR-15 | AD-5, AD-20 | OK, strong |
| FR-16 | AD-12 | OK except R12. README token instruction is stated in the FR itself |
| FR-17 | AD-3, AD-11, AD-12 | OK |
| FR-18 | AD-12, AD-18 | OK except R10 |
| FR-19 | AD-13, AD-15 | OK. No human-readable format is implied by the single envelope |
| FR-20 | AD-14 | OK. All six named categories have distinct exit codes |
| FR-21 | AD-13 | OK except R7 |
| FR-22 | F8 map, Deferred | Form OK; content R4 |
| FR-23 | AD-7, AD-11, AD-13 | OK. Hints R3, R12 |
| FR-24 | AD-18 | OK except R11 |
| FR-25 | AD-10, AD-18 | R4 |
| FR-26 | AD-18, Deferred | OK |

### Non-functional requirements

| Req | Spine | Result |
| --- | --- | --- |
| NFR-1 | None | OK: no hard target, nothing to diverge on. Startup cost is noted in Deferred |
| NFR-2 | AD-19 | R9 |
| NFR-3 | AD-21, AD-11 | OK, with the R3 container note. No arm64 verification is stated; CI is deferred, so cover it there |
| NFR-4 | AD-3 to AD-9, AD-20 | OK, strong |
| NFR-5 | AD-14, AD-19 | OK. `Retry-After` may be seconds or a date; handle in the story |
| NFR-6 | AD-1, AD-2, AD-6, AD-20 | Test-first OK; shared parts R2; add-a-feature R6. Specs next to code is already the repo practice |
| NFR-7 | AD-21, F9 | OK |

### Non-goals

| Non-goal | Result |
| --- | --- |
| Any write to Jira | Respected, AD-5 |
| Generating root-cause text | Respected, nothing in the spine |
| Saved presets, free-form JQL, similar-ticket search | Respected, AD-9 refuses raw JQL |
| Discovering required fields | Respected. `expand=names` is display names only |
| More than one site per config | Respected, single `site` in schema |
| Jira Server / Data Center | Respected, AD-12 and stack |
| Confluence in v1 | Respected, AD-2 keeps the seam |

### Addendum

| Item | Result |
| --- | --- |
| C#/.NET, Windows and Linux, x64 and arm64 | OK, AD-21, stack |
| Standard DI, interface-based services | OK, conventions. Requests created with `new` are plain objects, not services, so no conflict |
| TDD-first | OK, AD-20 |
| Review before implementation | R6 |
| Shared concerns modular | HTTP, auth, config OK; logging and output R2 |
| Stateless, request carries origin | OK, AD-10 |
| Config contents | OK, conventions; `cloudId` added for the gateway |
| 30-second timeout | R9 |
| API v3, classic and gateway base URLs, Basic auth | OK, AD-12, stack. The verify-against-official-docs caveat is partly carried by Deferred |
| Read-only scope names, token rotation, dedicated-account workaround | README content; not in spine. Include in the R3 and R12 README list |
| Defense in depth | OK, AD-3, AD-7, memlog line 25 |
| Config protection ideas | R3 |
| Rejected alternative: writable-file warning | Absent from the spine, consistent with the decision, but not recorded (R3) |
| Distribution options | Closed by AD-21 |

### Open questions

| Question | Result |
| --- | --- |
| Distribution method | Closed, AD-21. Folder location deferred with a revisit point |
| Config location and protection steps | Location closed by AD-11; protection open (R3) |
| Survey timing, baseline, questions | Correctly out of architecture scope; owner Mike, before rollout |

### Deferred items with PRD exposure

The deferral is reasonable, but the requirement is at risk if the outcome is bad:

- **Attachment redirect (FR-10, FR-16).** From memory and unverified, the Jira attachment content endpoint commonly redirects to a separate media host. AD-12 as written would not follow it, so if `redirect=false` fails, AD-12 needs amending. Scoped-token access to attachments is checked at the same time. Consider an early spike, since attachments are part of loop step 2.
- **`dotnet tool update --source` (NFR-7).** A fallback exists, so the risk is low.
