---
title: Adversarial review of the jira-cli architecture spine
reviewed: ../ARCHITECTURE-SPINE.md (status draft, updated 2026-11-01)
context: ../../../prds/prd-jira-cli-2026-11-01/prd.md, addendum.md, spine .memlog.md
date: 2026-11-01
---

# Adversarial Review: jira-cli Architecture Spine

## Verdict

**Not ready to build. All holes can be closed with AD changes, and no redesign is needed.**

The Gateway, scope, and GET-only structure is sound. I could not make jira-cli send a non-GET. The holes are at the edges of that structure:

- Data that passes the Gateway check inside an allowed ticket can still carry other-project content (S1).
- jira-cli itself can overwrite its own config (S2).
- The parser can print a local file, including the token, to stdout (S3).
- The post-check can pass without checking anything (S4).
- The core types cannot all be placed as AD-1, AD-6, and AD-8 require (B1, B2).

| ID | Severity | Hole | ADs to change |
| --- | --- | --- | --- |
| S1 | Critical | `get` passes other-project content through the custom field mapper | AD-10, AD-16, AD-20 |
| S2 | High | An attachment download can replace `config.json` | AD-11, AD-17, AD-20 |
| S3 | High | Parser response files print local files, including the token (tested) | AD-13, AD-20 |
| S4 | High | The post-check passes when `ObservedProjects` is empty | AD-6, AD-7 |
| S5 | High | `--page-token` and unvalidated keys can inject into the URL | AD-5, AD-19, conventions |
| S6 | High | A Request's scope and its URL are not bound together | AD-6, AD-7, AD-20 |
| B1 | High | `IJiraGateway` and `IJiraRequest<T>` have no legal home | AD-1, AD-4, AD-6 |
| B2 | High | `VerifiedIssue` and `AttachmentRef` creation cannot be built as written | AD-6, AD-7, AD-8, AD-20 |
| S7 | Medium | Same-host redirects allow an HTTP downgrade and a tenant switch | AD-12 |
| S8 | Medium | Proxy and certificate environment variables reach the transport | AD-3, AD-12 |
| S9 | Medium | The README's config protection fails when the directory is writable | AD-11, docs |
| S10 | Medium | Redaction misses stderr and early errors, and its method is undefined | AD-12, AD-13 |
| B3 | Medium | Config has no single owner, and it conflicts with "only the transport reads credentials" | AD-2, AD-12, conventions |
| B4 | Medium | Attachment streaming conflicts with `Parse(Stream)` and AD-14 | AD-6, AD-14, AD-19 |
| B5 | Medium | Partial-result categories have two possible owners | AD-13, AD-14 |
| B6 | Medium | The `related --key` check order and relation set are undefined | AD-10 |
| B7 | Low | Comments and attachments have two possible sources | AD-8 |
| L1-L3 | Low | Origin coverage, touched-ticket timing, Windows file names | AD-10, AD-17, AD-18 |

**Evidence labels:** *Tested* means I ran it. *Documented* means a source supports it. Findings with no label follow from the spine text. *Verify* means the Jira or runtime behavior needs a real check; the rule is still worth adding because it fails safe either way.

---

## Safety attacks

### S1 (Critical): Other-project content leaves through `get` custom fields

**Obeys:** AD-7, AD-10, and AD-16 to the letter.

- AD-7's post-check tests only the top-level issue's project.
- AD-10 applies `RelatedIssueProjection` to "parent, subtask, and link data", but only the `related` capability is mapped to it. FR-4 does not ask `get` to show relations, so a `get` unit has no reason to call it.
- AD-16 does not define "custom field". It takes names from `expand=names`, and its fallback is "any other shape to its JSON value".

**Attack:**

1. Ticket `ALLOWED-1` links to `SECRET-9`.
2. A `get` builder requests all fields, which is the default for `GET /issue`, and treats every entry in `names` as a displayable field.
3. `names` covers system fields too (*Verify*), so `parent`, `subtasks`, and `issuelinks` reach the shared mapper. They match no specific case, so they are emitted as raw JSON.
4. `SECRET-9`'s embedded summary and status appear in stdout. The post-check passed, and the projection never ran.

A builder who filters on `customfield_*` still has a gap. Issue-valued custom fields such as Parent Link and marketplace issue pickers fall through the same "any other shape" branch. Parent Link is *documented* as an unstable object with the key at `data.key`, and Atlassian support describes it as outside the public API. I did not confirm that it embeds a summary. The point is that the fallback copies whatever is there, with no allow-list check at any depth.

Two related paths:

- `history`: changelog items for link, parent, or issue-valued fields carry `fromString` and `toString` text about other tickets, and AD-8 removes the post-check for this call. Keys are acceptable under FR-13, but any non-key text is not (*Verify* what the text contains).
- AD-10's phrase "before it leaves `Domain`" does not match the data flow. The data enters at `Parse` in Infrastructure and passes through the adapter before it reaches Domain.

**Rule:** Displayable custom fields are only `customfield_*` keys, the mapper's fallback emits only JSON scalars and arrays of scalars and omits any object it cannot map, `Parse` exposes relation data only as a `RawRelation` type that the Contracts layer cannot map, changelog items for relation fields are reduced to keys, and a golden test with other-project data planted in `parent`, `issuelinks`, and a nested custom field proves none of it reaches stdout from `get`, `related`, or `history`.

Dropping unknown objects trades some FR-6 completeness for safety. That trade is a product decision; the alternative is a recursive scan that removes issue-shaped objects, which is harder to prove complete.

### S2 (High): An attachment download replaces `config.json`

**Obeys:** AD-17 ("The tool does not restrict the target directory"), AD-11, AD-8, and AD-3's wording about reading allowed projects.

**Attack** (the option name is illustrative):

```text
jira-cli attachments download ALLOWED-1 --name config.json --dir ~/.config/jira-cli
```

1. Anyone who can attach to a ticket in an allowed project uploads `config.json` with wider `allowedProjects` or a different `site`. A prompt injection in the ticket text can tell the agent to run the command.
2. Jira's `created` is newer than the local file, so AD-17 reports `updated` with `ok: true`.
3. AD-17's temp file and atomic rename need only directory write permission. **This defeats the addendum's root-owned, read-only file protection when the directory is writable.** A `:ro` mount does stop it.
4. Claude Code `deny` rules on the path do not stop it either, because the write comes from inside jira-cli rather than from a file tool the rule covers (*Verify* against the rule forms the README will recommend).
5. The next invocation uses the attacker's config. AD-11's "the tool only reads this file" is false in effect, and FR-12 and FR-14 are defeated.

The attacker's config needs a token to widen access to the real site, so the cleanest results are pointing the agent at an attacker-chosen `site`, or at a colleague's token and access scope. Corrupting the file also disables the tool. The same write path can overwrite the work log.

**Rule:** `IAttachmentStore` resolves the target through symlinks and refuses any directory at or inside the jira-cli config or log directories, and it replaces an existing file only when that file's last-write time equals the `created` of a same-named attachment on that ticket, otherwise reporting `failed`.

The second clause uses the timestamp AD-17 already sets, so only files the tool itself wrote are replaced. It tightens the user's replace decision, so it needs confirmation.

**Residual risk:** new files can still land in places like `.git/hooks` or `~/.claude`. This is outside AD-3's stated guarantees, but the usage guide should warn about it.

### S3 (High): Parser response files print local files, including the token

**Obeys:** AD-13 ("Parser errors go through the same writer as `usage`"), AD-11, and AD-12.

**Tested** on System.CommandLine 2.0.14 with .NET SDK 10.0.403 and a fake config. `get @fakeconfig.json` expanded the file into tokens and produced parser errors that quote each one (messages translated from the Japanese locale):

```text
Unrecognized command or argument 'apiToken'.
Unrecognized command or argument 'FAKE_TOKEN_123'.
```

Response files are on by default. Setting `ParserConfiguration.ResponseFileTokenReplacer = null` turned expansion off, and the argument stayed literal.

Parsing has to happen before config loading, because AD-11 exempts `--help` and `--version` from loading. So a redactor that works from the config has no secret yet when this error is written. Even with one, the attack reads any file the process can read, such as `.env` or SSH keys, and those values are not known secrets.

FR-17 accepts that the agent may read the config, but FR-18 says credentials never appear in output. This also bypasses any Claude Code deny rule on the path, because the read happens inside jira-cli.

**Rule:** The parser is configured with `ResponseFileTokenReplacer = null`, and AD-20 adds a test that `@<file>` arguments are never expanded and that the file's contents never reach stdout, stderr, or the log.

### S4 (High): The post-check passes when nothing is observed

**Obeys:** AD-6, AD-7, and FR-3.

AD-7 says the "project of every returned item is allowed". `ObservedProjects(T)` is written by the Request's author, and nothing requires one project per item.

**Attack, the list fields case:**

1. FR-3 lists the list entry fields: key, issue type, summary, status, priority, assignee, updated. Project is not among them.
2. A builder requests exactly those fields. `/search/jql` returns only what is requested, and its default is ids only (*Documented*, secondhand; confirm against Atlassian's reference).
3. With no `project` in `T`, `ObservedProjects` returns an empty set, or skips null items.
4. "Every item in the empty set is allowed" passes, so the list post-check does nothing.

Units can also differ on the source of the project: `fields.project.key` or the prefix of the response's `key`. For `GetIssueRequest`, a narrowed `fields` value has the same effect. Alone, the JQL restriction still holds. With S5, the result is a full leak.

**Rule:** Every issue-shaped result must yield exactly one non-null `fields.project.key` per returned item, each Request using `IssueScope` or `ProjectScope` on issues must request `project` in its `fields`, and the Gateway raises `ProjectNotAllowedException` when the count differs from the item count, a value is missing, or the value disagrees with the prefix of the item's `key`.

`GetProjectRequest` needs a matching rule on its own response shape.

### S5 (High): `--page-token` and unvalidated keys can inject into the URL

**Obeys:** AD-9 (it escapes JQL values only), AD-5 (`RelativeRequest` "carries a path and query", with no defined structure), and AD-19 (the cursor is opaque).

**Attack on the query:**

```text
jira-cli list --project ALLOWED --page-token "x&jql=project=SECRET"
```

A Request that builds its query by concatenation sends a second `jql` parameter. Whichever one Jira uses (*Verify*), AD-9's "only JQL producer" no longer holds. With S4, `SECRET` content is returned. The same channel can add `fields` or `expand`, or `updateHistory=true`, a GET that changes the user's recently viewed data (from my memory of Jira's docs, *Verify*).

A clean token from a different query is also a concern, since Jira may honor the query it was issued for. The post-check covers that once S4 is closed.

**Attack on the path:** the conventions require `IssueKey` to be "normalized to upper case", with no format. `ALLOWED-1/../SECRET-1` has the allowed project part, survives upper-casing, and normalizes to `/rest/api/3/issue/SECRET-1`. S4's rule catches that response, but the pre-check should not depend on it. Upper-casing limits this to upper-case path targets, which is partial and accidental protection.

**Rule:** `RelativeRequest` holds a list of path segments and a list of query name and value pairs and no raw string, the transport alone percent-encodes them and refuses `.` or `..` segments, `IssueKey` and `ProjectKey` constructors accept only `^[A-Z][A-Z0-9_]+-[1-9][0-9]*$` and `^[A-Z][A-Z0-9_]+$` after ordinal upper-casing, and `--page-token` is passed only as the value of `nextPageToken`.

### S6 (High): A Request's scope and its URL are not bound together

**Obeys:** AD-6 ("declares its scope") and AD-7.

The Request declares its own scope, and `Build()` is independent of it. The Gateway checks the declaration, not the URL.

- `new XRequest(verified)` declares `VerifiedIssueScope(verified)` while `Build()` targets another issue's key. This scope has no post-check, so nothing catches it.
- A new Request can declare `Unscoped`. AD-7's "fixed list" is prose, and AD-20 lists no test for it. NFR-6 expects any engineer in the department to add features, so this is the likely way a guarantee erodes.

**Rule:** The Gateway refuses an `Unscoped` Request unless its type is exactly `GetMyselfRequest` and refuses any `IssueScope` or `VerifiedIssueScope` Request whose path lacks the scope's key as an exact segment, and AD-20 adds a reflection test that every `IJiraRequest<T>` implementation is in a reviewed Request-to-scope table.

### S7 (Medium): Same-host redirects allow an HTTP downgrade and a tenant switch

**Obeys:** AD-12, which says "follows same-host redirects itself" and "never sends credentials to another host".

- A redirect to `http://<site>.atlassian.net/...` is the same host, so Basic credentials can go out in clear text.
- With `cloudId`, the host is the multi-tenant `api.atlassian.com`. A redirect to `/ex/jira/<otherCloudId>/...` is the same host, so credentials and reads move to another site. This also breaks "one configuration connects to one site".
- The rule says credentials do not cross hosts, but not whether an unauthenticated cross-host hop is followed. That choice is deferred for attachments, and it should not silently apply to every Request.

**Rule:** The transport attaches credentials only when the absolute target starts with the configured base URL (scheme, host, port, and base path), allows at most 3 hops, and refuses every other redirect as `network` unless an attachment-only exception is added by changing this AD.

### S8 (Medium): Proxy and certificate environment variables reach the transport

**Obeys:** AD-12 ("TLS validation is never disabled"), and AD-11, whose ban on environment variables covers only config paths. AD-3 claims no env var can break the guarantees.

```text
HTTPS_PROXY=http://127.0.0.1:8080 SSL_CERT_FILE=/tmp/evil.pem jira-cli get ALLOWED-1
```

- `HttpClient` reads `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` by default (*Documented*).
- On Linux, .NET gets its root store through native OpenSSL helpers, which as far as I recall honor `SSL_CERT_FILE` and `SSL_CERT_DIR` (*Verify*; not tested, since only Windows is available here).
- If both hold, a local interceptor receives the Basic credentials, and it can forge responses that pass the post-check, with TLS validation still on.

The practical gain is limited when the agent can read the config (FR-17), but it matters wherever deny rules block that read. `DOTNET_STARTUP_HOOKS` is the same class. AD-3 should not claim more than the tool enforces.

**Rule:** The transport takes its proxy only from an optional config setting and sets `UseProxy = false` otherwise, on Linux validates against a trust store that ignores `SSL_CERT_*` (or AD-3 explicitly excludes process-environment tampering of the runtime), and the build sets `StartupHookSupport=false`.

Corporate proxies are the cost here, and the spine does not say whether the department uses one. Confirm before choosing.

### S9 (Medium): The README's config protection fails when the directory is writable

**Obeys:** AD-11 and FR-12 (README guidance). The memlog records the constraint: the agent reading the config is acceptable, and modifying it is not.

The addendum's advice is a root-owned, read-only file. In `~/.config/jira-cli/`, the agent owns the directory, so it can run `mv config.json x` and create a new `config.json` without touching the protected file's permissions. A single-file `:ro` mount resists renaming the file itself, but renaming a parent directory and recreating the path likely works (*Verify*). Containers running as root defeat ownership entirely and can edit the `passwd` home that AD-11 trusts. For Windows `%APPDATA%`, the addendum offers only deny rules.

This touches a closed choice: the addendum rejected refusing to start when the file is writable. The check below is advisory, so it keeps that choice.

**Rule:** The README protection steps cover the file and every ancestor directory, not just the file, and `doctor` adds a check that fails with a fix hint when the current user can replace the config file through the file or any ancestor directory.

### S10 (Medium): Redaction misses stderr and early errors, and its method is undefined

**Obeys:** AD-12 (redaction "at the envelope writer and the work log writer").

- The stderr warning writer is not covered, even though it can carry exception text.
- When config parsing or validation fails, the redactor has no token yet, so an error that echoes a misplaced value leaks it.
- "Only the transport reads credentials" conflicts with a redactor that must know the token.
- One unit redacts literal values and another uses patterns. Patterns corrupt ticket text and break golden files. A literal-only redactor misses the Base64 `email:token` form. Redacting the email address would alter legitimate ticket content.

**Rule:** `ISecretRedactor` replaces exact matches of the token and its Basic header value, runs at every stdout, stderr, and log sink, and config-load errors name the offending key but never its value.

---

## Incompatible builds

### B1 (High): `IJiraGateway` and `IJiraRequest<T>` have no legal home

**Unit A (Domain placement):** the conventions say "ports are `I…` in `Domain`", so `IJiraGateway` and `IJiraRequest<T>` go in Domain. But `Build()` returns `RelativeRequest` per the request flow, which is in `Atlassian.Common`, and AD-1 forbids Domain from referencing Common. So A creates a second, Domain-level request type and has the Gateway convert it.

**Unit B (Infrastructure placement):** B reads AD-4's "port implementations depend on `IJiraGateway`" as an internal detail and keeps both as `internal` in Infrastructure, using `RelativeRequest` directly.

Requests from A do not compile against B's Gateway, and no AD settles the choice.

**Why it matters for safety:** with A, `Application` and `Cli` both reference Domain. A use case can inject `IJiraGateway` directly and define its own `IJiraRequest<T>`, skipping the port layer and declaring any scope (S6). AD-20's tests cover only the transport dependency.

**Rule:** `IJiraGateway` and `IJiraRequest<T>` are `internal` to `JiraCli.Infrastructure` and are not ports, `RequestScope` and `ProjectAccessPolicy` stay in `Domain`, the port convention is limited to interfaces consumed by `Application`, and a NetArchTest rule forbids `Application` and `Cli` from depending on any Gateway or Request type.

### B2 (High): `VerifiedIssue` and `AttachmentRef` creation cannot be built as written

AD-8 says "Only the Gateway creates `VerifiedIssue`". Three things stop that from working as written:

1. **Assembly boundary.** The type is in Domain, and the Gateway is in Infrastructure. A public constructor lets `Application`, `Cli`, or any adapter forge one. An internal constructor needs `InternalsVisibleTo`, which the spine does not mention. Unit A uses a public constructor, and Unit B uses a Domain factory. AD-7's "argument came from a post-checked response" cannot be checked at runtime in either case, so a forged value bypasses the only issue scope with no post-check. AD-20 does not test this.
2. **No data path.** `IJiraRequest<T>` exposes `Scope`, `Build`, `Parse`, and `ObservedProjects`. The last returns projects, so the Gateway cannot learn the canonical key from it. `SendAsync` returns `T` in the request flow, with no slot for a `VerifiedIssue`.
3. **Timing and shape of `T`.** `AttachmentRef` must come "from that same verified response", but `Parse` runs before the post-check. Unit A has `T` as a Domain `Issue` that `Parse` fills, refs included, before verification. Unit B has `T` as an Infrastructure DTO, and the adapter builds refs afterward, meaning any adapter can build one from anything. These units share no `T`, and neither puts creation in the Gateway.

**Rule:** The Gateway alone calls a factory in `Domain` that is internal with `InternalsVisibleTo("JiraCli.Infrastructure")`, a new `IIssueRequest<T>` method exposes the canonical key, project, and raw attachments, `SendIssueAsync` returns `(VerifiedIssue, T)` after the post-check, `AttachmentRef` instances are reachable only from `VerifiedIssue`, `T` is an Infrastructure DTO that adapters map to Domain models, and NetArchTest allows only `JiraGateway` to depend on the factory.

### B3 (Medium): Config has no single owner

The conventions put the "schema type in `Infrastructure`, loader in `Common`". AD-12 says "only the transport reads credentials", and the transport is in Common, which cannot see the schema type.

- **Unit A:** the composition root reads the schema and passes values to the transport, so the Cli layer holds the token, and the schema object carrying it is available to Infrastructure.
- **Unit B:** Common gains its own connection settings type, which it loads from a second, partial read of the file.
- **`--limit` validation:** one unit validates `maxPageSize` in `ListCommand` from the schema. Another uses a Domain port. `Application` cannot see the schema at all, because it references only Domain.
- **`site` shape:** undefined. `acme`, `acme.atlassian.net`, and a full URL each produce a different base URL or a bad one. A value like `evil.example/#` produces a URL on another host entirely.
- **Source generation:** a generic loader in Common cannot generate code for a type in Infrastructure unless it takes a `JsonTypeInfo<T>`.
- **`doctor`:** AD-11 requires it to report config failures, but a container built from a valid config cannot exist yet at that point. Units may fail in the middleware or build a partial container.

**Rule:** The composition root loads config once through `Load(path, JsonTypeInfo<T>)`, validates `site` as a single DNS label and `cloudId` as a GUID, passes the schema object only to a mapper producing `ConnectionSettings` (Common) and `AccessSettings` with allowed projects and page sizes (Domain), and exposes load failure as a value that `doctor` reports and every other command raises as `config`.

### B4 (Medium): Attachment streaming conflicts with `Parse(Stream)` and AD-14

AD-6 requires `Parse(Stream)` to be pure, with no I/O, and to return `T` for every endpoint. AD-19 sets no total time limit, so that downloads of any size work.

- **Unit A:** `Parse` buffers the whole attachment into `byte[]`, so memory use grows with file size.
- **Unit B:** `Parse` returns the open stream, which must outlive `SendAsync`. A stall then raises `TransportException` in `IAttachmentStore`, outside the Gateway, breaking AD-14's translation. The response's owner and disposal are undefined.

Reading JSON from a stream inside `Parse` is itself I/O. The same stall can occur there, though inside the Gateway.

**Rule:** Attachment download is a distinct Gateway method, `DownloadAsync(AttachmentRef, Func<Stream, CancellationToken, Task> sink)`, in which the Gateway owns the response lifetime and translates any `TransportException` raised during the sink, and `Parse` is defined as pure apart from reading its given stream.

### B5 (Medium): Partial-result categories have two possible owners

AD-13 requires `error` to carry "the first failing item's category" alongside `data`, for `doctor` and `attachments download`. AD-14 says `ExceptionMapper` is "the only place" that maps exceptions, and commands return data rather than throwing.

- **Unit A:** per-item results hold a category string set in `Application` or `Domain`, a second mapping that violates AD-14.
- **Unit B:** per-item results hold the exception, which the Cli layer maps. This is compliant, but it needs `ExceptionMapper` to also supply per-item `hint` text.

The two units' Contracts differ. AD-13 also does not state the exit code for partial results, though the natural reading is the category's code.

**Rule:** Per-item results in `Domain` carry the Domain exception, `ExceptionMapper` maps each item, the first failure sets both the envelope `error` and the exit code, and AD-13 states this explicitly.

### B6 (Medium): The `related --key` check order and relation set are undefined

AD-10 says `k` returns content "only when `k` is actually related to `origin`; otherwise it fails with `notFound`". Consider `related ALLOWED-1 --key SECRET-9`:

- **Unit A** checks the allow-list first and returns `projectNotAllowed` for every disallowed `k`, whether related or not.
- **Unit B** checks the relation first and returns `notFound` when unrelated, and `projectNotAllowed` when related.

The exit codes differ, so golden files conflict. Unit B also fetches the origin before refusing, while Unit A makes no HTTP call.

The other open points:

- **Where the decision lives:** AD-1 says business decisions belong in Domain, and AD-10 assigns only the logging to the use case. A use case with an inline `Contains` check is an easy way to break AD-1 without noticing.
- **The relation set:** FR-7 says "parent or epic". Whether that means `parent` only, or also Epic Link and Parent Link custom fields (S1), decides what counts as related. Two units return different `notFound` results for the same ticket.
- **When the origin is recorded:** if the use case records it after the origin's fetch and that fetch fails, FR-25's "always" fails for that log line.

**Rule:** `related <origin> --key <k>` records `origin` first, applies `ProjectAccessPolicy` to `k` before any HTTP call and returns `projectNotAllowed` when it fails, then fetches `origin` and calls a Domain `RelationSet.Contains(k)` built only from `parent`, `subtasks`, and `issuelinks`, returning `notFound` on a miss, then fetches `k` through `IssueScope`.

### B7 (Low): Comments and attachments have two possible sources

With all fields requested, `GET /issue` also embeds a partial `comment` page and the `attachment` list. One `get` unit uses the embedded comments and misses later pages, breaking FR-4's "all comments". Another pages through a Request, or merges both and duplicates. One attachments unit reads a second dedicated request, but AD-8 ties `AttachmentRef` to "that same verified response".

**Rule:** `GetIssueRequest` is the only source of `AttachmentRef` and its `Parse` ignores embedded `comment`, and comments come only from `GetCommentsRequest` paged to completion.

---

## Low-severity gaps

- **L1 (FR-25 coverage):** `related --key` covers only content retrieval. `history` and `attachments` on a ticket reached through a relation have no origin path, and neither does `get`, which AD-10 accepts. FR-25 says "always". **Rule:** Either `history` and `attachments` accept `--origin`, validated the same way as `related --key`, or the PRD narrows FR-25 to content retrieved through `related`.
- **L2 (touched tickets):** AD-18 does not say when the Gateway records a ticket. If it records after the post-check, a refused moved ticket disappears from the log. If it records only the requested key, the canonical key is lost. **Rule:** The Gateway records the requested key before the call and the canonical key after the response, even when the post-check fails, and it records no other response data.
- **L3 (Windows file names):** AD-17's "strips all path components" does not cover `report.txt:evil` (an alternate data stream), reserved names such as `CON`, or trailing dots. On Linux, `\` is not a separator for `Path.GetFileName`. AD-17's duplicate handling can also miss names differing only by case on Windows, so one file overwrites another. **Rule:** `IAttachmentStore` treats both `/` and `\` as separators on all platforms, refuses `.`, `..`, names containing `:`, and reserved Windows names as `failed`, and compares duplicates after this sanitizing using the target file system's case rule.

---

## Attacks that failed

- **Sending a non-GET:** AD-5's type has no method or header slot, so `X-HTTP-Method-Override` cannot be expressed, and only the transport may use `HttpClient`. The only gap is a side-effecting GET through injection, which S5 closes.
- **Moved tickets:** both the old-key and new-key directions are refused, once S4 is in place.
- **JQL through filter values:** AD-9's single builder and top-level AND hold for typed filters. The remaining channel is outside JQL, in S5.
- **Raw attachment ids:** `--id` is matched against the verified ticket's attachments, so it does not reach other projects, given B2's rule.
- **Config path through environment or options:** AD-11 holds on Linux. The Windows Known Folder check is already deferred to an AD-20 test. Attacks on the file itself are S2 and S9.
- **Disclosure through `related`:** FR-13 explicitly allows keys for other projects, so the difference between `projectNotAllowed` and `notFound` reveals nothing new.

---

## Required AD-20 additions

These go with the rules above: forged `VerifiedIssue` prevention (B2), Gateway and Request dependency limits (B1), a Request-to-scope table (S6), planted other-project data in embedded fields (S1), `@file` arguments (S3), config-directory download refusal (S2), empty or missing `ObservedProjects` (S4), hostile `--page-token` and key values (S5), and redirect downgrade and tenant-switch cases (S7).

## Sources

- [HttpClient.DefaultProxy (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.defaultproxy): environment proxy behavior on Linux.
- [OpenSslCachedSystemStoreProvider.cs (dotnet/runtime)](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/X509Certificates/OpenSslCachedSystemStoreProvider.cs): root store paths come from native helpers. It does not itself show the environment variables.
- [Parent Link JSON object thread (Atlassian Developer Community)](https://community.developer.atlassian.com/t/jira-portofolio-parent-issue-field-json-object-instead-of-null-or-key/23652): object shape, `data.key`, unstable and outside the public API.
- [jira_v3_openapi generated models (docs.rs)](https://docs.rs/crate/jira_v3_openapi/latest/source/docs/SearchAndReconcileRequestBean.md): secondhand source for `/search/jql` `fields` defaulting to `id`. I could not read this part of Atlassian's own reference, because the page was truncated.
- Local test: a small console program using System.CommandLine 2.0.14 on .NET SDK 10.0.403, with a fake config file and no real credentials.
