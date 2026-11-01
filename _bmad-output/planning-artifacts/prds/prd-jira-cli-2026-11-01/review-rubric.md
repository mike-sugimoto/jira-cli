# PRD Quality Review — jira-cli

## Overall verdict

The PRD has a clear thesis (a small, read-only, project-confined bridge that an autonomous agent can be trusted with) and every feature traces back to the loop it serves; scope and non-goals are honest. The main risks are in done-ness: the central safety promise "the agent cannot modify the config file" (FR-12) is not something the tool alone can guarantee, and a few FRs lack defaults and bounds that story creation will need. The first counter-metric also depends on logs that, by design, are local and disposable.

## Decision-readiness — adequate

Decisions are stated as decisions, and the one real trade-off (FR-16a: the agent may read the token, mitigated by a scoped read-only token) is named with what was given up. Open Questions are genuinely open and correctly deferred to architecture. One tension is not acknowledged: Counter-metric 1 ("Any write to Jira or any access outside the allow-list. Target: zero.") has no stated way to measure it, while FR-24 makes logs local and disposable.

### Findings

- **high** Counter-metric 1 cannot be measured as written (§5, FR-24) — Local, disposable logs cannot show department-wide that writes and out-of-allow-list access stayed at zero. *Fix:* State how it is checked: automated tests (NFR-3) prove it by construction, and Jira's own issue history would show any write made with an engineer's account; local logs are for individual troubleshooting only.

## Substance over theater — strong

No persona, innovation or vision theater. The Vision is specific to this product ("read-only by design", "confined to specific projects"). NFRs are product-specific; NFR-1 uses an adjective ("feel comparable to opening it in a browser") but this is an explicit, user-accepted choice.

### Findings

- **low** NFR-1 has no bound (§4 NFR-1) — Acceptable for an internal tool, but story acceptance cannot test it. *Fix:* Leave as is, or add a soft guide such as "tool overhead is small compared with Jira's own response time".

## Strategic coherence — strong

The thesis (trustworthy bridge for loop engineering) drives both scope (read-only, allow-list) and the success metrics (smooth agent use, faster loop). Counter-metrics are present and tied to the thesis. MVP scope is problem-solving, and the selection A/D/E/F/G/L/M/N/O follows from the loop in §2.

### Findings

- **medium** Success metrics have no timing or baseline (§5) — "Measured through individual surveys" does not say when, so "faster" has nothing to compare against. *Fix:* Name the survey points (e.g. before rollout as a baseline, then about 3 months after) and the key questions.

## Done-ness clarity — thin

Most FRs are testable. The weak spots are the ones downstream stories will hit first.

### Findings

- **high** FR-12 promises something the tool cannot enforce alone (§3 F6 FR-12) — "kept in a user configuration file that the agent cannot modify" depends on OS permissions and on how WSL, Docker and Windows are set up, not only on jira-cli. An engineer cannot tell what "done" means for the tool. *Fix:* Split responsibilities: (a) the tool reads the file only from a fixed user-level location outside any project workspace and never from a path the agent can redirect; (b) the diagnostic check (FR-22) warns when the file is writable by the user running the tool; (c) the setup guide documents how to protect the file on Windows, WSL and Docker.
- **medium** FR-1 has no default scope (§3 F1 FR-1) — The loop starts with "show open tickets", but FR-1 does not say what happens when no filter is given, or whether "open" means unresolved. *Fix:* State defaults: all allowed projects, unresolved tickets, sorted by last updated (or priority).
- **medium** FR-18 "stable" is undefined (§3 F8 FR-18) — Stability matters because the usage guide (FR-21) and Claude Code depend on field names. *Fix:* Define stable: fields are not renamed or removed without a version change, and the output states its format version.
- **medium** FR-2 has no default or maximum (§3 F1 FR-2) — Large lists can flood the agent's context. *Fix:* Add a default page size and an upper limit.
- **low** FR-10 lacks destination and overwrite behavior (§3 F4 FR-10). *Fix:* The caller chooses the directory; existing files are not silently overwritten; attachments over a size limit are skipped with a notice.

## Scope honesty — strong

Non-Goals do real work (no writes, no text generation, no JQL, no Server/DC, no Confluence in v1). Inline assumptions were all resolved with the user. One implicit assumption is not stated.

### Findings

- **medium** Single Jira site is assumed but not stated — The PRD never says whether one configuration covers one Jira Cloud site or several. *Fix:* Add "one Jira Cloud site per configuration" as a stated scope decision (or confirm multi-site is needed).

## Downstream usability — adequate

This PRD feeds architecture and story creation. IDs are unique, and cross-references resolve. Terms drift slightly: "ticket" vs Jira's "issue", "allow-list" vs "allowed projects", "agent" vs "Claude Code".

### Findings

- **medium** No glossary (whole document) — Architecture and stories will map "ticket" to Jira "issue" and need a single term for the protected configuration file. *Fix:* Add a short glossary: ticket (= Jira issue), allow-list, user configuration file, agent (= Claude Code), loop.

## Shape fit — strong

Internal tool with a single operator role plus an agent consumer: a capability spec with the loop as a lightweight journey is the right shape. No UJ overhead.

## Mechanical notes

- IDs FR-16a and FR-23a break the simple numbering; renumber to contiguous IDs before handing to architecture.
- The document has no H1 title; add one matching the frontmatter title.
- Open Question 2 mentions WSL and Docker but not Windows, which FR-16a's decision was made for.
- addendum.md uses bare URLs (markdown lint MD034); format as links during polish.
