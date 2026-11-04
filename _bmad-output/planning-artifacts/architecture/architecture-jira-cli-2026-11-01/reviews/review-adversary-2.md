---
title: Second adversarial review of the jira-cli architecture spine
reviewed: ../ARCHITECTURE-SPINE.md (status final, updated 2026-11-03)
context: ../../../prds/prd-jira-cli-2026-11-01/prd.md, reviews/review-adversary.md, spine .memlog.md, architecture-explainer.md
focus: ChildKeysScope and GetChildKeysRequest; the fixes applied after the first review
date: 2026-11-03
---

# Second Adversarial Review: jira-cli Architecture Spine

## Verdict

**Fix before building. All holes can be closed with AD text and tests, and no redesign is needed.**

The core held. I could not send a non-GET, steer the child lookup through arguments, reuse `ChildKeysScope` from another Request type, or redirect the config through options or environment. The holes are:

- **The new exception (F2, F3):** the child lookup can carry a project's name into `data.notices`, and nothing stops the Gateway minting a token from an item that skipped the allow-list.
- **An incomplete first-review fix (F1):** `AttachmentRef` still has no issuer, no scope, and no possible `VerifiedIssueScope` path check. A careless `--id` build reads other-project attachment bytes with no check after the call.
- **The managed folder (F4, F5):** "resolves through symlinks" is defeated with two junctions and no race. A race also works on both platforms.

| ID | Severity | Hole | Evidence | ADs to change | User decision |
| --- | --- | --- | --- | --- | --- |
| F1 | High | `AttachmentRef` has no issuer and no scope; `--id` can reach other-project bytes | Documented | AD-6, AD-7, AD-8, AD-20 | No |
| F2 | High | `project.name` can enter the DTO and `data.notices` | Inferred | AD-10, AD-15, AD-20 | No |
| F3 | High | `SendIssueAsync` can mint a token from a child lookup item | Inferred | AD-7, AD-8, AD-20 | No |
| F4 | High | Linked managed root or key folder passes the "inside the root" check | Tested | AD-17, AD-20 | No |
| F5 | Medium | Swapping the key folder during a download writes outside the root | Tested | AD-3 or AD-17 | **Yes** |
| F6 | Medium | The work log appends through a link, with agent-controlled text | Tested | AD-18, AD-20 | No (follows F5) |
| F7 | Medium | Child classification and the summary search have no defined failure behavior | Documented and inferred | AD-7, AD-10, AD-19, AD-20 | No |
| F8 | Medium | `related` item shape conflicts between AD-10 and AD-15; duplicate relations | Inferred | AD-10, AD-15 | No |
| F9 | Medium | Arrays of objects and shape-sniffed objects get through the custom field mapper | Inferred | AD-16, AD-20 | No |
| F10 | Medium | Changelog text bypasses AD-16 | Inferred | AD-10, AD-16, AD-20 | **Yes** |
| F11 | Medium | A root agent in a container can edit passwd and redirect the config | Tested | AD-3, AD-22 | **Yes** |
| F12 | Medium | The post-check depends on an optional member, so it can fail open | Inferred | AD-6, AD-7, AD-20 | No |
| F13 | Medium | AD-5's transport signature cannot express AD-19's two timeout rules | Inferred | AD-5, AD-19 | No |
| F14 | Medium | Download and selection have two possible owners | Inferred | AD-8, AD-17 | No |
| F15 | Medium | AD-9's sole JQL producer conflicts with the child lookup's JQL | Inferred | AD-7, AD-9, AD-10 | No |
| F16 | Low | A temp file opened without exclusive create writes through a planted link | Tested | AD-17 | No |
| F17 | Low | Redacting serialized JSON misses a value containing `+` | Tested | AD-12, AD-20 | No |
| F18 | Low | Parser errors can log a token because the redactor is not armed yet | Inferred | AD-11, AD-12, AD-18 | No |
| F19 | Low | Names colliding on Windows both report `downloaded` | Tested in part | AD-17 | No |
| F20 | Low | "Tickets touched" is undefined for searches and child lookups | Inferred | AD-18 | No |
| F21 | Low | The Deferred row names the wrong ADs if a live check fails | Documented in the spine | Deferred table | No |

**Evidence labels:**

- *Tested* means I ran it, in a lab under the session scratchpad. No jira-cli code exists yet, so each test exercises the file system or library behavior a store built to the AD would rely on, not the product. Windows tests used .NET 10.0.403 on the machine named in the environment. Linux tests used Python in WSL Ubuntu, which has no .NET, so they show the same system calls and not .NET's own behavior on Linux.
- *Documented* means a source supports it. Where the source is third-party, the finding says so.
- *Inferred* means it follows from the spine text, or from my memory of Jira's API where stated, and has not been run.

**The threat model for F4, F5, F6, F11, and F16.** An agent with an unrestricted shell under the same account can write those files itself, so these give it nothing new. They matter when the agent's own tools are narrower than the user's, such as Claude Code deny rules, an allow-listed `jira-cli` command, or an approval prompt on file edits, and it is steered by ticket text. The write then comes from inside an approved tool, and the attachment's bytes are chosen by whoever attached the file. The first review's S2 rests on the same reasoning.

---

## Attack 1: the child lookup and `ChildKeysScope`

### F2 (High): `project.name` can reach `data.notices`

**Obeys:** AD-7, AD-10 ("Its DTO holds only key and project", "naming the project"), and AD-15 (`message` and `hint` are free strings).

1. `fields=project` returns a project object. From my memory of Jira's API it carries `key`, `name`, and possibly `projectCategory`. I could not confirm this from an official page (*Inferred; verify*).
2. "Project" in the DTO is not defined as the key. A builder holds the object, or adds `name`, because AD-10 says the notice is "naming the project".
3. The notice reads: `Child in project "Apollo Acquisition" is not accessible`. A name can be sensitive content, and it is free text that can carry a prompt injection into the very message the agent is told to pass on.

AD-20's test checks that the Request asks only for `project`. The name arrives inside that field, so the test passes. FR-7 and FR-13 say key only. AD-15 also puts no limit on any later notice, such as one quoting an exception message.

**Closing rule:** `GetChildKeysRequest.Parse` reads only `key` and `fields.project.key` into `IssueKey` and `ProjectKey`, and every notice's `message` and `hint` come from a fixed template whose only variable parts are those value objects.

**Can notices carry content?** Not once this rule is in place.

### F3 (High): `SendIssueAsync` can mint a token from a child

**Obeys:** AD-7 and AD-8 ("`SendIssueAsync` returns `(VerifiedIssue, T)` after the post-check").

AD-8 does not restrict which scopes `SendIssueAsync` accepts, and `ChildKeysScope`'s post-check does not apply the allow-list. `GetChildKeysRequest` exposes `ObservedIssues` like every issue-returning Request.

1. A builder routes the child request through `SendIssueAsync`, or writes a generic method that issues a token for each observed issue so later calls can skip a re-fetch.
2. `SECRET-9`, a child of `ALLOWED-1`, gets an issued token. The record of issued tokens now contains it.
3. `GetCommentsRequest(token)` uses `VerifiedIssueScope`, whose post-check is "None", and its comments come back.

This takes a builder's mistake, but no AD forbids it, AD-20's forged-token test does not catch it because the token is genuine, and the result is a full FR-13 bypass.

**Closing rule:** The Gateway issues tokens only after a passed `IssueScope` post-check, `SendIssueAsync` refuses every other scope, and a test proves a child lookup adds nothing to the issued-token record.

### F7 (Medium): Child classification and the summary search

**Obeys:** AD-7's `ChildKeysScope` row, AD-9, AD-10, and AD-19.

AD-7's general rule requires the project to match the key's prefix, but the row for this scope requires only a non-null project. AD-10 says only "Summaries of allowed children come from a separate `ProjectScope` search". That leaves these open:

- **Classification:** for an item with key `ALLOWED-7` and project `SECRET`, as a stale index might return, Unit A classifies by key prefix and Unit B by project. Neither leaks, because content still goes through checked scopes, but the output differs.
- **Query form:** Unit A uses `key in (...)`. A deleted or nonexistent key makes the whole query return 400 (*Documented*, Atlassian developer community), which AD-14 maps to `usage`, telling the agent its own command was wrong. A long list also meets URL length limits on a GET-only transport (*Inferred*). Unit B uses `parent = X AND project in (...)`, which avoids both.
- **Paging:** AD-19 pages "comments, changelog, and child keys" to completion, and does not list summaries. An epic with 500 children gets 100 summaries in one build.
- **No allowed children:** the adapter calls `ProjectScope` with no keys, the Gateway refuses it as AD-9 requires, and `related` fails with `projectNotAllowed`. FR-7 says this case succeeds.
- **Missing summary:** a child that moves after the key lookup is absent from the summaries. One unit drops it, one prints nulls, and one marks it inaccessible.

**What the agent sees for a child that moved:**

| Case | Result | Assessment |
| --- | --- | --- |
| Moved to `SECRET` before the lookup | `SECRET-7`, key only, plus a notice | Correct |
| Agent retries the old key: `related P --key ALLOWED-7` | Passes the policy on the old prefix, misses `RelationSet`, returns `notFound` | Safe; no pointer to the new key |
| Agent retries the old key: `get ALLOWED-7` | Post-check fails, `projectNotAllowed`, no content | Safe; differs from the row above |
| Moved from `SECRET` to `ALLOWED` | `ALLOWED-8`, with summary | Correct |
| Moved between key lookup and summary search | **Undefined**, as above | This finding |
| Moved between `related` and `--key` | `IssueScope` post-check refuses | Correct |

**Closing rule:** Summaries use `parent = <canonical key> AND project in (<allowed projects among the children>)`, paged to completion and skipped when that set is empty, and any child whose key prefix and project disagree, or that the summary search does not return, is output key-only with `accessible: false`.

### F8 (Medium): `related` item shape

**Obeys:** AD-10 and AD-15, which do not agree.

- AD-15 has `list` and `related` sharing one ticket summary type, with standard fields always present and `null` when empty. AD-10 reduces other-project items to `{ key, relationship, accessible: false }`. Unit A uses one type with nulls, and Unit B uses two item types. The golden files differ. Deferring "per-command `data` fields" to stories does not help, because `list` and `related` are separate stories sharing the type.
- `parent = X` also returns subtasks, which the embedded `subtasks` field already lists. Unit A prints them twice, and Unit B removes duplicates. A ticket that is both a child and a link has the same problem, since `relationship` holds a single value.
- AD-15 says "Lists use the key `items`", yet `data.notices` is a list. It is also unclear whether `notices` is `null`, empty, or absent on commands that never produce one, and whether `related --key` emits notices.
- `issuelinks` embeds summary and status but not assignee or updated, so allowed links need either nulls or their own search. AD-10 defines a search only for children.

**Closing rule:** `related` items are one type extending the ticket summary with `relationship` and `accessible`, with summary fields `null` for inaccessible items, one entry per key per relationship after removing duplicate child and subtask entries, summaries for allowed parents and links coming from the same search as children, and `notices` present and `null` when empty only on `related <origin>`.

### F15 (Medium): AD-9's sole JQL producer and the child lookup

**Obeys:** AD-9 ("`JqlBuilder` is the only JQL producer... The only exception is AD-10's child key lookup") and AD-10 ("fixed JQL").

The sentence can mean either of two things:

- **Unit A** writes the JQL inside `GetChildKeysRequest`, and an architecture test forbids `jql` strings outside `JqlBuilder`, so A fails it.
- **Unit B** adds a public `JqlBuilder.ChildrenOf(key)`. Any Request can now get JQL with no project restriction. Its own post-check would catch other-project items, but the only rule protecting "always top-level AND" has a public way around it.

`VerifiedIssueScope` also binds its token to the path. `ChildKeysScope` binds nothing, so the token and the JQL's key can differ.

**Closing rule:** `GetChildKeysRequest` takes only a `VerifiedIssue`, gets its query from an `internal JqlBuilder.ChildrenOf(VerifiedIssue)` that an architecture test allows only that Request to call, and the Gateway confirms the `jql` value contains the scope token's canonical key.

### What held

- **Steering the parent or JQL through arguments:** `related` has no page token, the child cursor is never exposed, the origin passes the key regex, and the query uses the canonical key from a post-checked response. No argument reaches the JQL.
- **Another Request using `ChildKeysScope`:** the exact-type check refuses other types and subclasses. The remaining ways in are F3 and F12, not this check.
- **Reading other-project content by widening `fields`:** the AD-20 test covers it, and the DTO has nowhere to put it once F2's rule is in place.
- **`related --key` for a disallowed child:** refused before any HTTP call.
- **Hidden projects:** AD-22 correctly tells the agent that a missing notice proves nothing.
- **PRD fit:** FR-7's notice and `ok: true` are met. FR-13's "content is never retrieved" holds once F2's rule keeps only the key.

---

## Attack 2: the first-review fixes

### Fix-by-fix results

| Fix | ADs | Result |
| --- | --- | --- |
| Issued-token record, internal tokens | AD-4, AD-8 | Holds for `VerifiedIssue`; fails for `AttachmentRef` (F1) and new paths to minting (F3) |
| Fail-closed post-check | AD-7 | Rule holds; enforcement can be skipped (F12) |
| Structured `RelativeRequest`, key regexes | AD-5, conventions | Holds against injection; signature conflicts with AD-19 (F13) |
| Config mapping | AD-11 | Holds; `site` and `cloudId` validation closes the host attacks |
| `customFieldPassthrough` | AD-16 | The option holds; the mapper rule underneath it has gaps (F9) |
| Managed folder, always replace | AD-17 | Name rules and final-name replacement hold; folder links and races do not (F4, F5, F16) |
| No redirects | AD-12 | Holds; any 3xx fails safe. If `redirect=false` misbehaves live, downloads fail without leaking |
| Windows path resolution | AD-11 | Holds per the earlier tech review's test, which I did not repeat; the registry is excluded by AD-3 |
| Work log and envelope moved to Common | AD-13, AD-18 | Placement holds; the log has its own link problem (F6) |

### F1 (High): `AttachmentRef` has no issuer and no scope

**Obeys:** AD-6, AD-7, AD-8, and AD-17.

The first review's B2 said a Request method must expose raw attachments so the Gateway can create refs. The spine kept only part of that:

- **No data path:** `ObservedIssues` yields "one key and project", so the Gateway never sees attachment ids. The ids are in `T`, which the adapter holds. The explainer gives `VerifiedIssue` an `Attachments` list, but nothing fills it.
- **No scope:** AD-7's table has no attachment row. `VerifiedIssueScope` requires the key as an exact path segment, and the endpoint is `/rest/api/3/attachment/content/{id}` (*Documented*, third-party references and the earlier tech review's reading of the OpenAPI), which contains no issue key. The explainer's Request list has no attachment Request at all.
- **No runtime check:** AD-8's record covers "the tokens it issued". If refs are built by the adapter, the Gateway did not issue them.

**Two units that do not fit:**

- **Unit A** declares a `GetAttachmentContentRequest` with `VerifiedIssueScope`. Every download fails the path check.
- **Unit B** has `DownloadAsync` check only `ref.Issue` against the record, and the adapter builds refs from the DTO.

**The attack on Unit B:** a builder implements `--id` as `new AttachmentRef(verified, id)`, which AD-8's "reachable only from a `VerifiedIssue`" does not prevent inside `Infrastructure`.

```text
jira-cli attachments download ALLOWED-1 --id 99999
```

`ALLOWED-1` verifies, the token is genuine, and the bytes of attachment `99999`, from any project the credentials can read, go to disk with no check after the call. AD-8 requires `--id` to be matched against the ticket's attachments, but the type does not enforce it and no test is named for it.

**Closing rule:** `GetIssueRequest` exposes attachment metadata to the Gateway, which alone mints each `AttachmentRef` into `VerifiedIssue.Attachments` and records it, `DownloadAsync` refuses any ref not in that record by object identity, and AD-7's table gains an `AttachmentRefScope` row whose exact path segment is the ref's id.

### F4 (High): A linked root or key folder passes the "inside the root" check

**Obeys:** AD-17 ("resolves the target through symlinks and refuses any path outside the managed root").

The rule does not say whether the root itself is resolved, and .NET has no `realpath`. Three builds follow:

- **Unit A, simple check:** `FileInfo.ResolveLinkTarget(true)` on the target returned `null` when the parent folder was a junction (*Tested*). It looks at the final component only, so the check passes and the file goes elsewhere.
- **Unit B, walks every component and compares with a resolved root:** a junction replacing `attachments` with an ancestor, plus a junction named `ALLOWED-1` inside that ancestor pointing at the victim folder, gave `passesInsideCheck=True`, and the lab's `config.json` then held the attachment's text (*Tested*). No race is needed.
- **Unit C, compares with an unresolved root:** this refuses the attack, but it also refuses every download for a user whose home or `~/.cache` is legitimately a link (*Inferred*).

Windows junctions needed no privilege here. File symlinks failed with `IOException` on this machine, so a Windows agent has folder links but not file links, unless Developer Mode or elevation is on. The lab's config sat in an unprotected folder. A config protected as the README describes, including its ancestors, would not be overwritten, but `~/.claude`, repository hooks, and shell profiles are not protected that way.

**Closing rule:** The store refuses to write unless `jira-cli`, `attachments`, and `<canonical key>` under the base folder are each real directories and not symlinks or reparse points, checked component by component, leaving links above `jira-cli` unaffected, and AD-20 tests a linked root, a linked key folder, and an ancestor-plus-inner-link pair on both platforms.

### F5 (Medium, needs a decision): Swapping the key folder during a download

**Obeys:** AD-17, and F4's rule too, since any path-based check has this window.

A check followed by a path-based temp file and rename has a gap, and the real gap is long, because the network response comes between them. A second process swapped the key folder for a link while a simulated store ran checks and writes, with and without a 50 ms delay standing in for the wait for Jira (*Tested*):

| Platform | Delay | Writes completed | Completed outside the root |
| --- | --- | --- | --- |
| Windows, .NET | none | 3,229 | 95 |
| Windows, .NET | 50 ms | 10 | 10 |
| Linux, Python | none | 63,564 | 376 |
| Linux, Python | 50 ms | 139 | 67 |

The attacker in these runs was a tight loop. Only the fact that it succeeds matters, not the rates. Many attempts also ended in I/O errors, which the store would report as `failed`.

**Options for the user:**

- **A. Accept and document (recommended).** Add to AD-3's out-of-scope list: "a process under the same account changing the managed folder while a download runs". The agent needs a running background process to do this, and F4's rule already stops the simple static attack.
- **B. Close it.** Use handle-based creation and rename: `openat` and `renameat` with `O_NOFOLLOW` on Linux, and handle-relative open and rename on Windows. .NET's file APIs do not offer this, so it means platform-specific P/Invoke and its own tests on both platforms.

**Closing rule, option A:** AD-3 lists concurrent same-account changes to the managed folder as out of scope, and the store creates the temp file and repeats F4's check immediately before the rename to shorten the window.

**Closing rule, option B:** The store holds a no-follow directory handle from `jira-cli` down to the key folder and performs temp creation and rename relative to that handle.

### F6 (Medium): The work log appends through a link

**Obeys:** AD-18, which has no equivalent of AD-17's link rule, and which logs "command and redacted arguments, including parser errors".

The file name is predictable, the agent can link it to anything, and the arguments are the agent's own text. On Linux, with the log file linked to a stand-in for a shell profile (*Tested*):

```text
PROFILE
{"args":["$(echo PWNED > .../marker)"]}
```

When a shell ran that file, it created `marker`. JSON escaping does not help, since `$(...)` inside double quotes is still evaluated. On Windows, file symlinks needed privilege here, and a junction only moves the folder, so the fixed file name limits this attack there (*Tested* for the link failure; *Inferred* for the limit).

**Closing rule:** `WorkLogWriter` refuses a log file or log folder chain from `jira-cli` down that contains a symlink or reparse point, and treats that as a log failure with a stderr warning, with the concurrent-swap case following the F5 decision.

### F9 (Medium): Arrays of objects and shape-sniffed objects in the mapper

**Obeys:** AD-16: "JSON scalars and arrays of scalars as they are. Any object it cannot map is omitted."

- **Arrays of objects:** an array is neither a scalar array nor "an object". Multi-select and multi-user fields are arrays of objects, from my memory of Jira's API, so builders must decide something. Unit A maps each element, Unit B omits the field and loses FR-6 data, and Unit C treats it as a value and prints it, which turns a multi-issue picker's `[{ key, fields: { summary } }]` into the leak the first review's S1 described.
- **Shape sniffing:** AD-16 requests only `expand=names`, so the mapper must guess "option" or "user" from an object's shape. An app-defined issue picker with a `value` or `displayName` member is rendered as an option or a user, and its text comes out. This field is not listed in `customFieldPassthrough`, so AD-3's exclusion does not apply.

**Closing rule:** The mapper chooses by each field's `schema` from `expand=names,schema`, accepting only the system types `string`, `number`, `date`, `datetime`, `option`, `user`, ADF text, and arrays of those, and omits every other type unless passthrough lists it.

*Verify* `expand=schema` and the type names in the first Gateway story. This is from my memory of Jira's API.

### F10 (Medium, needs a decision): Changelog text bypasses AD-16

**Obeys:** AD-8, AD-10 ("Changelog items for relation fields are reduced to keys"), and AD-16, which covers issue fields only.

Changelog requests use `VerifiedIssueScope`, with no post-check. Items hold `fromString` and `toString`:

- "Relation fields" has no list. Unit A covers `Link`, Unit B adds `Parent` and `Epic Link`, and neither knows about app-defined fields.
- A custom issue picker that AD-16 omits from `get` still shows its text in `history`.
- A ticket moved in from another project has a `project` item, whose text is the other project's name (from my memory of Jira's API).

AD-20's planted-data test passes for whichever fields the builder thought to plant.

**Closing rule:** History shows from and to values only for an allow-list of system fields and for custom fields AD-16 would display, reduces relation fields to keys, and for every other field, including `project`, shows the field name and timestamp with the values omitted.

**Decision:** this narrows FR-11's "from what, to what" for unusual fields. The alternative is to accept the risk and extend AD-3's documented exclusions to changelog text. I recommend the rule, which matches the choice already made for FR-6.

### F11 (Medium, needs a decision): A root agent can redirect the config through passwd

**Obeys:** AD-3 ("config path resolution" is in scope), AD-11, and AD-22.

AD-11 trusts `getpwuid_r`. AD-3's protection duty covers "the file and every ancestor directory", not the passwd entry. An agent running as root in a container, which AD-22's "passwd entry containers need" suggests is a real setup, can do this while the mounted config remains read-only:

1. Edit `/etc/passwd` to set its home to a folder it owns.
2. Put a config there with wider `allowedProjects`.
3. Run jira-cli, which loads the decoy.

In a private mount namespace with a bind-mounted copy of passwd, `getpwuid` returned `/decoy-home`, and a hostile `HOME` was still ignored (*Tested* with Python; the real `/etc/passwd` was unchanged). The agent can read the token per FR-17, so the decoy can reuse it.

**Options for the user:**

- **A.** The README requires containers to run the agent as non-root, with passwd not writable by it (recommended).
- **B.** AD-3 names a writable passwd database as out of scope, alongside the registry.

**Closing rule:** AD-22's README obligations extend the protected set to `/etc/passwd` and `/etc/nsswitch.conf` and require the agent to run as non-root in containers, or AD-3 lists a writable passwd database as out of scope.

### F12 (Medium): The post-check depends on an optional member

**Obeys:** AD-6 ("Issue-returning Requests *also* expose `ObservedIssues`") and AD-7.

`IJiraRequest<T>` has no `ObservedIssues`, so the Gateway has to test for it. `GetProjectRequest` uses `ProjectScope` but returns no issue, and it has no key prefix to compare, so AD-7 special-cases it in a sentence.

- **Unit A:** `if (request is IObservesIssues o) Check(o)`. A new or edited search Request that omits the member compiles and skips the post-check, with `ProjectScope` still declared. The reflection test compares only type and scope.
- **Unit B:** a separate `SendProjectAsync`. `GetProjectRequest` does not fit A's Gateway.

**Closing rule:** Any scope with an after-call check accepts only Requests implementing a required observation interface, `GetProjectRequest` implementing it with its own project-only observation, and the Gateway raises `ProjectNotAllowedException` when a Request with such a scope does not implement it.

### F13 (Medium): AD-5's signature cannot express AD-19

**Obeys:** AD-5 ("exposes only `GetAsync(RelativeRequest, CancellationToken)`"; `RelativeRequest` holds only segments and query pairs), AD-19 (two timeout rules), and AD-20 (the transport suite tests timeouts).

The transport must apply 30 seconds total for JSON, and 30 seconds to headers plus a 30-second stall limit for downloads, yet nothing in its input tells it which.

- **Unit A** adds a `GetStreamAsync`, which AD-5 forbids.
- **Unit B** adds a flag to `RelativeRequest`, which AD-5's definition does not include.
- **Unit C** has the Gateway impose the JSON limit by cancellation, which fails AD-20's transport timeout tests.

The return type also differs between units. The explainer says `TransportResponse`, and AD-5 is silent on it and on who disposes it.

**Closing rule:** `RelativeRequest` carries a `ResponseKind` of `Json` or `Download` that selects the timeout profile and nothing else, and `GetAsync` returns a disposable `TransportResponse` exposing the body stream, with non-2xx already raised as `TransportException`.

### F14 (Medium): Download and selection have two possible owners

**Obeys:** AD-8 (ports take a key and selector), AD-17 ("`IAttachmentStore` saves... Downloads use `DownloadAsync`"), and AD-1 (business decisions in Domain).

- **Unit A:** the store is the whole download adapter, holding `IJiraGateway`, and calls it with a sink. `IAttachmentStore.SaveAsync(key, selector)`.
- **Unit B:** the explainer's shape. A use case holds `IAttachmentReader` and `IAttachmentStore`, and the store is file-only: `SaveAsync(name, Stream)`. A `Stream` from the Gateway then crosses `Application`, and failures in the sink have to get back through it, which AD-14 requires the Gateway to translate.

These interfaces do not fit. Selection is also unplaced: the ambiguous-name and duplicate decisions are business rules for Domain, but `AttachmentRef` is internal to `Infrastructure`.

**Closing rule:** `IAttachmentReader` lists Domain `AttachmentInfo`, a Domain `AttachmentSelection` applies the selector and raises the ambiguous-name error, and the single port call `IAttachmentStore.SaveAsync(IssueKey, AttachmentSelection)` runs in `Infrastructure`, where it resolves ids to refs, calls `DownloadAsync` and writes, so no stream crosses `Application`.

### F16 (Low): Temp file without exclusive create

AD-17 says "a temp file in the target folder" with no rule on how it opens. A truncating open through a planted link wrote through to the victim on Linux, and an exclusive open was refused with the victim unchanged (*Tested*). It needs a predictable temp name, such as `report.pdf.tmp`.

**Closing rule:** Temp files have random names and open with `FileMode.CreateNew`.

### F17 (Low): Redaction after JSON encoding

AD-12 redacts at "every sink", and AD-13 uses the default encoder. `+` serializes as `+`, so an exact match on the serialized text missed both a sample Basic value and a sample token containing `+` (*Tested*, fake values). `/` and `=` were not escaped. Base64 Basic values usually contain `+`. Atlassian tokens may not. No path that emits such a value is known, so this is defense in depth.

**Closing rule:** Redaction runs on string values before JSON encoding, and AD-20 tests a secret containing `+`.

### F18 (Low): Parser errors before the redactor is armed

AD-18 logs parser errors, and AD-11 loads config after the `--help` and `--version` exemption, with nothing saying where a parse failure falls. If parsing fails first, `jira-cli get --token ATATT...`, from an agent guessing at an option, is written to the log unredacted, against FR-18.

**Closing rule:** Config load runs before a parse failure is reported or logged, and when load fails, the log records only option names and an argument count.

### F19 (Low): Name collisions on Windows

AD-17's duplicate rule compares "after sanitizing", but the first review's case-rule wording did not carry over. `Report.pdf` and `report.pdf` are one file on Windows, and `a.pdf.` and `a.pdf ` normalize to `a.pdf` (*Tested* for normalization). Both report `downloaded` with the same path, and the agent reads one under both names.

**Closing rule:** Duplicates are compared after Windows name normalization, using the ordinal case-insensitive rule on Windows.

### F20 (Low): "Tickets touched" for searches and child lookups

AD-18 records "the requested key" and "the canonical key". A search has neither. One unit logs every key returned, including `SECRET-9` from a child lookup, which then looks like access in the log. Another logs none, which leaves FR-24's "tickets touched" empty for `list`.

**Closing rule:** The log records tickets touched only for `IssueScope` and token-scoped calls, with searches and child lookups recording the query's origin or project keys and a result count.

### F21 (Low): Deferred row names the wrong ADs

The Deferred row includes the `statusCategory` filter and `parent =` child lookup, and it says "a failure amends AD-8 or AD-12". Those two would amend AD-9 and AD-10. If `parent =` does not cross projects live, FR-7's key-only children disappear with no one assigned to notice.

**Closing rule:** The row reads "a failure amends the AD governing that item (AD-8, AD-9, AD-10, or AD-12)".

---

## Attacks that failed

- **Sending a non-GET:** AD-5 has no method or header slot, and only the transport may use `HttpClient`. Changing that requires changing the AD.
- **Query or path injection:** segments and pairs are encoded by the transport alone, `.` and `..` are refused, and the regexes limit keys. `--page-token` is a value only, and a replayed cursor still meets the post-check.
- **Redirecting the config through options or environment:** no option exists. The Linux test above confirms `HOME` is ignored by the passwd lookup, and the earlier tech review tested the Windows token path. F11 and the registry are separate routes.
- **Sending credentials elsewhere:** `site` is a single label, `cloudId` a GUID, and no redirect is followed. Proxy exposure is a recorded user decision under AD-3.
- **Replacing a link at the final name:** rename replaces a planted symlink or hardlink rather than writing through it, leaving the victim unchanged (*Tested*: symlink and hardlink on Linux, hardlink on Windows).
- **Attachment names that escape:** `.. `, `... `, and `..  .` normalize on Windows to the key folder itself rather than its parent (*Tested*), so a rename onto it fails and reports `failed`. They slip past a literal `.` and `..` check, but cannot escape. Separators and `:` are refused.
- **Forged `VerifiedIssue`:** types are internal and the record refuses unknown tokens. A forged copy with an already verified key gains nothing.
- **Contradictions with the PRD:** FR-1, FR-6, FR-10, and FR-25 now match the spine. F10 is the only new tension, with FR-11.

## Tests to add to AD-20

- A `--id` outside the ticket's attachments is refused, and an adapter-built `AttachmentRef` is refused (F1).
- A planted `project.name` and `projectCategory` never reach stdout (F2).
- A child lookup adds nothing to the issued-token record (F3).
- A linked root, a linked key folder, and the ancestor-plus-inner-link pair are refused on both platforms (F4); a linked log file is refused (F6).
- A child in a mismatched project, absent from summaries, or with no allowed siblings still gives `ok: true` (F7).
- An array of issue-shaped objects and an object with a `value` member in an unlisted custom field never reach stdout (F9).
- Changelog text for an unlisted custom field and the `project` field never reaches stdout (F10).
- A `ProjectScope` Request lacking observation is refused (F12).
- A secret containing `+` is redacted (F17).

## Sources

- [JQL queries containing issuekey: changed error messages and response codes (Atlassian Developer Community)](https://community.developer.atlassian.com/t/jql-queries-containing-issuekey-or-issue-will-have-the-error-messages-and-response-code-changed/48242): 400 when one key in `key in (...)` does not exist. From a rollout thread; confirm against the live site.
- [Issuekey in JQL should have an option not to fail if the key does not exist (JRASERVER-33031)](https://jira.atlassian.com/browse/JRASERVER-33031): same behavior, Server tracker.
- [Get attachment content (cljdoc, community SDK)](https://cljdoc.org/d/net.clojars.deadmeme5441/atlassian-sdk/0.3.0/api/atlassian.jira.attachments) and [Blink Ops connector docs](https://docs.blinkops.com/docs/integrations/jira/actions/get-attachment-content.md): path `/rest/api/3/attachment/content/{id}` and `redirect`. Third-party; `review-tech-currency.md` records the same from the OpenAPI.
- `review-tech-currency.md` F1: the Windows known-folder results, which I relied on and did not repeat.
- Local tests: `fs.cs`, `race.py`, and `race2.py` in the session scratchpad's `t1` folder, `race.cs` in its `t2` folder, and `pw.sh` in `t1`. All used fake values and lab folders.
- Not confirmed from an official source, and marked in the text: the `project` object's members, `expand=schema` and its type names, and the changelog `project` item.
