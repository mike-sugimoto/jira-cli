---
review: tech-currency
target: ../ARCHITECTURE-SPINE.md
date: 2026-11-01
verdict: changes-required
---

# Tech Currency Review: jira-cli Architecture Spine

## Verdict

**Changes required before the spine is used as a build substrate.**

Every version pin is current. The gap is that the decision log only confirmed those versions *exist*. It never confirmed they *fit*. Testing fit produced two contradictions of committed safety claims and several live defaults the spine has to name.

| Severity | Count | Findings |
| --- | --- | --- |
| High | 2 | F1 Windows config path follows `USERPROFILE`; F2 NetArchTest cannot see into async lambdas |
| Medium | 6 | F3 xunit `dotnet test`; F4 System.CommandLine defaults; F5 search `fields` and 400 mapping; F6 attachment redirect; F7 stdout encoding and message language; F8 scoped token needs `cloudId` |
| Low | 5 | F9 to F13 |

Three items in the spine's Deferred table can be closed now (see "Deferred table updates").

## How this was checked, and its limits

- **Sources:** the official Jira OpenAPI spec, Microsoft Learn, the `dotnet/dotnet` source at the exact System.CommandLine 2.0.14 commit, the NuGet flat container, and the .NET release index.
- **Experiments:** throwaway projects in the session scratchpad, with isolated `NUGET_PACKAGES`. The repo and the spine were not changed.
- **Test hosts:**
  - Windows 11 build 10.0.26300 (ja-JP, win-arm64) with SDK 10.0.403 and runtime 10.0.14.
  - WSL Ubuntu 24.04 (linux-arm64, glibc, `libc6-dev` installed).
- **Not available:**
  - No Jira site or credentials, so **no Jira behaviour was tested live**. Jira items marked confirmed are confirmed from documentation only.
  - The Docker daemon was off, so musl/Alpine was not tested.
  - No x64 host was tested.
- **Doc tooling:** the Atlassian HTML reference pages came back truncated, so Jira facts come from the machine-readable OpenAPI spec instead: <https://developer.atlassian.com/cloud/jira/platform/swagger-v3.v3.json>. "OpenAPI" below means that file.
- **Side effect:** the WSL probes started the stopped Ubuntu distro. All temporary files inside WSL were removed.

## Status of every requested item

| # | Item | Status | Evidence |
| --- | --- | --- | --- |
| 1 | `GET /rest/api/3/search/jql` exists; legacy search going away | Confirmed | OpenAPI: not deprecated. Legacy `/search` is flagged deprecated, "Currently being removed" |
| 2 | Parameters fit AD-9 and AD-19 (`jql`, `nextPageToken`, `maxResults`, `fields`, `expand`) | Confirmed, with F5 | OpenAPI. A project restriction satisfies the bounded-query rule. The cursor is opaque and "will expire in 7 days" |
| 3 | `maxResults` limit | Confirmed | OpenAPI: default 50, "may return fewer items per page", "returns max 5000 issues". The spine's cap of 100 is well inside this |
| 4 | `fields` default | Confirmed; **spine does not account for it** (F5) | OpenAPI: "The default is `id`"; "By default, this resource returns IDs only" |
| 5 | `nextPageToken=null` bug (JRACLOUD-94632) | Confirmed; memlog is stale | Closed/Fixed 2025-06-26: <https://jira.atlassian.com/browse/JRACLOUD-94632>. Omitting the parameter is still correct and is what the OpenAPI describes |
| 6 | GET issue with `expand=names` | Confirmed | OpenAPI: "`names` Returns the display name of each field"; `IssueBean.names` exists |
| 7 | Comment pagination | Confirmed, with F11 | OpenAPI: `startAt`, `maxResults` (default 100), `total`. No `isLast` |
| 8 | Changelog pagination | Confirmed | OpenAPI: `startAt`, `maxResults` (default 100), `isLast`, `total`, oldest first |
| 9 | AD-8 premise: sub-resource responses carry no project | Confirmed | OpenAPI schemas `Comment`, `PageBeanChangelog`, `AttachmentMetadata` have no project field |
| 10 | Attachment content endpoint and `redirect` | Confirmed from docs; live behaviour unverified (F6) | OpenAPI: `redirect` defaults to true; 303 with `Location`; 200 or 206 when `redirect=false` |
| 11 | Moved issue under an old key | Confirmed from docs; not tested live | OpenAPI on GET issue: "a case-insensitive search and check for moved issues is performed. If a matching issue is found its details are returned, a 302 or other redirect is **not** returned. The issue key returned in the response is the key of the issue found." |
| 12 | Scoped token via `api.atlassian.com/ex/jira/{cloudId}` with Basic auth | Confirmed from docs; not tested live | "You need to call the Atlassian API to use API tokens with scopes" and HTTP basic with email and token: <https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/>. URL form: <https://support.atlassian.com/atlassian-cloud/kb/401-unauthorized-error-when-service-account-accesses-jira-or-confluence-api/>. Third-party report of 401 on site URL and 200 on gateway: <https://github.com/sooperset/mcp-atlassian/issues/968> |
| 13 | Scopes needed | Confirmed | OpenAPI `x-atlassian-oauth2-scopes`: `read:jira-work` for search, issue, comment, changelog, attachment content, project; `read:jira-user` for myself. Matches PRD addendum. Granular alternatives are marked "Beta" |
| 14 | Scoped token can download attachments through the gateway | **Unverifiable** | No official statement found. A third party claims `read:attachment:jira` alone is not enough (<https://pypi.org/project/mcp-atlassian-attachments/>). Use the classic scopes; needs a live check |
| 15 | Removal date for classic (unscoped) tokens | **Unverifiable** | No official date found. PRD FR-16 supports both token types, so nothing breaks today |
| 16 | NetArchTest.Rules 1.3.2 on net10.0 | Loads and runs: confirmed. Rule coverage: **contradicted** (F2) | Experiment. Latest on NuGet, published 2021-05-23, netstandard2.0, Mono.Cecil 0.11.3. It read C# 14 extension blocks, `field`, function pointers, ref fields, and file-local types without error |
| 17 | System.CommandLine 2.0.14 can customize parse-error output | Confirmed | Experiment; `ParseResult.Errors`, `ParseResult.Action`, `ParseErrorAction`: <https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke> |
| 18 | System.CommandLine writes to stdout on its own | Confirmed: **it does** (F4) | Source at commit `0427b9a`, `Invocation/ParseErrorAction.cs`, plus experiment |
| 19 | xunit.v3 4.0.2 | Current: confirmed. Fit: **needs config** (F3) | Latest stable; released 2026-10-09: <https://xunit.net/releases/v3/4.0.2>. Experiment |
| 20 | `GetFolderPath(ApplicationData)` ignores `APPDATA` | **Contradicted in part** (F1) | Experiment. Ignored only while the API succeeds. `USERPROFILE` redirects it, and on API failure it reads `APPDATA` |
| 21 | `getpwuid` from .NET | Confirmed on glibc arm64; musl and x64 unverified (F9) | Experiment. The BCL's own wrapper is `internal`: <https://raw.githubusercontent.com/dotnet/runtime/release/10.0/src/libraries/Common/src/Interop/Unix/System.Native/Interop.GetPwUid.cs> |
| 22 | `PackAsTool` framework-dependent tool on arm64 | Confirmed | Install, run, and update tested on win-arm64 and linux-arm64 from one package built on win-arm64. Layout `tools/net10.0/any/`, `Runner="dotnet"`. Docs: "works on any platform with the .NET runtime installed": <https://learn.microsoft.com/en-us/dotnet/core/tools/rid-specific-tools> |
| 23 | `dotnet tool update --source` | Confirmed | Worked on both platforms. Docs: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-update> ("completely replaces existing NuGet configuration") |
| 24 | .NET 10 LTS and support window | Confirmed | Release index: 10.0 lts active, 10.0.14 (2026-10-25), EOL 2028-11-14; 11.0 still rc.3: <https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json> |
| 25 | System.CommandLine 2.0.14, DI 10.0.14 are the latest stable | Confirmed | NuGet flat container. 3.0.0 is rc.3 only (see F13) |
| 26 | `Retry-After` on 429 (AD-14 "if present") | Confirmed | <https://developer.atlassian.com/cloud/jira/platform/rate-limiting/> (updated 2026-10-30). The page's own pseudocode handles a missing header, so "if present" is right. API-token traffic stays under burst limits, not points |
| 27 | `resolution = Unresolved` (AD-9) | Confirmed, with F12 | <https://support.atlassian.com/jira-core-cloud/docs/advanced-search-reference-jql-fields> (read through a search summary, not opened directly) |

## High findings

### F1. The Windows config path follows `USERPROFILE`, and the planned test would not catch it

**Status: contradicted. Affects AD-11, AD-18, AD-20, the Deferred table, FR-14.**

AD-11 says the path is fixed and "environment is never trusted". The default registry value behind the Known Folder lookup is `%USERPROFILE%\AppData\Roaming` (`REG_EXPAND_SZ`, read on this machine), and it expands from the process environment.

Results on .NET 10.0.14:

| Hostile environment | Raw `SHGetKnownFolderPath(NULL token)` | `Environment.GetFolderPath(ApplicationData)` |
| --- | --- | --- |
| `APPDATA` and `LOCALAPPDATA` only | Real path | **Real path** |
| `USERPROFILE` set to a nonexistent folder | Fails, `0x80070003` | Real path, by reading the `APPDATA` variable |
| `USERPROFILE` nonexistent, plus `APPDATA` | Fails, `0x80070003` | **`C:\evil\appdata`** |
| `USERPROFILE` set to an existing folder | **Attacker folder** | **Attacker folder** |

So `USERPROFILE=<dir> jira-cli` loads a config with wider `allowedProjects`, which is exactly what AD-11 and FR-14 exclude. The work log under `LOCALAPPDATA` moves the same way.

Three parts of the spine are affected:

- **AD-20 gives false assurance.** "Config path tests with hostile `HOME` and `APPDATA`" pass on row 1 while the hole stays open.
- **The Deferred fallback does not work.** "If it fails, use the Win32 API directly" still gets the attacker path when called with a NULL token (row 4).
- **The BCL has an environment fallback.** Row 3 matches the runtime source: when the API fails, it reads the environment variable. <https://raw.githubusercontent.com/dotnet/runtime/release/10.0/src/libraries/System.Private.CoreLib/src/System/Environment.Win32.cs>

**Tested alternative.** With hostile `USERPROFILE` (existing folder) and `APPDATA`, both of these returned the real user's path:

- `SHGetKnownFolderPath` with an explicit process token (`OpenProcessToken`), with and without `KF_FLAG_DONT_VERIFY`.
- `GetUserProfileDirectory` with that same token.

Microsoft documents the token parameter as being meant for requesting a particular user's folder, with `TOKEN_QUERY` and `TOKEN_IMPERSONATE` rights: <https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath>. My test opened the token with `TOKEN_QUERY` and `TOKEN_DUPLICATE`.

**Recommendation:**

1. Change AD-11 to resolve the folder with an explicit process token, and ban `Environment.GetFolderPath` for these paths.
2. Fail with `config` if the call fails. Do not fall back.
3. Widen AD-20 to include `USERPROFILE`, `HOMEDRIVE`, `HOMEPATH`, and `LOCALAPPDATA`, with `USERPROFILE` pointing at an existing folder that holds a decoy config.
4. Remove this item from Deferred.

**Limits:** tested on one Windows build (10.0.26300, arm64). A registry edit to `HKCU\...\User Shell Folders` is a separate route that this fix does not cover. It is not an environment variable, so it is outside FR-14 as written, but the AD-3 documentation should state it.

### F2. NetArchTest 1.3.2 misses forbidden dependencies inside async lambdas and async local functions

**Status: contradicted for AD-4 and AD-20's purpose. It loads and runs fine.**

Tested with `HaveDependencyOn` on a Release build, using the same test project and sample library against both packages:

| Usage of the forbidden type | NetArchTest.Rules 1.3.2 | NetArchTest.eNhancedEdition 1.4.5 |
| --- | --- | --- |
| Field, cast in a method, generic argument, nested class | Caught | Caught where tested (nested class) |
| Async method | Caught | Caught |
| Sync lambda, with and without capture | Caught | Caught |
| **Async lambda, with and without capture** | **Missed** | Caught |
| **Async local function** | **Missed** | Caught |
| **`new HttpClient()` in an async lambda** | **Missed** | Caught |

The misses share one shape: a compiler-generated state machine nested two levels down. In this codebase, a one-line `async` lambda would pass the rule that "only the Gateway touches the transport". AD-20 presents this suite as the safeguard for AD-4, and it would stay green.

The package has also had no release since 2021-05-23. The decision log recorded only that 1.3.2 is the latest version.

**Recommendation:** choose one of these, then record it in the Stack table.

- Switch to `NetArchTest.eNhancedEdition` 1.4.5 (last release 2025-06-04, Mono.Cecil 0.11.6). The API differs: I had to use `HaveDependencyOnAny`.
- Write a small Mono.Cecil walker that recurses through all nested types.

Either way, add a self-test: a fixture containing an async-lambda violation that the rule must flag. It stops a future library change from silently weakening the rule. ArchUnitNET and Roslyn banned-API analyzers are other options, but I did not test them.

**Side note:** the fork reports a failing state-machine type as `D2_AsyncLambdaCapture/<>c__DisplayClass0_0/<<M>b__0>d`. An exclusion by the name `JiraGateway` may therefore flag the Gateway's own async lambdas. I did not test that case, so check it when writing the rule.

## Medium findings

### F3. `dotnet test` fails on xunit.v3 4.0.2 without a `global.json` opt-in

**Status: confirmed by experiment. Affects the Stack table, Structural Seed, AD-20.**

`xunit.v3` 4.x installs `xunit.v3.mtp-v2`. On SDK 10, plain `dotnet test` stops with: "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later." After adding the setting below, `dotnet test --project Tests` passed.

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Sources: <https://xunit.net/releases/v3/4.0.0> and <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>.

**Recommendation:** add `global.json` to the Structural Seed with this setting. Project and solution arguments also change: `--project`, `--solution`. Any developer on an SDK older than 10 cannot use this mode.

### F4. System.CommandLine's defaults break AD-13, and one default can expose the token

**Status: customization confirmed; three defaults must be switched off. Affects AD-13, FR-18.**

AD-13 says parser errors go through the same writer, but it names no mechanism. Calling `Invoke` with the defaults gives this:

| Behaviour | Result | Evidence |
| --- | --- | --- |
| Parse error | Typo suggestions **and full help go to stdout**, errors to stderr, exit 1 | `ParseErrorAction` source; experiment |
| `jira-cli [suggest:2] li` | Plain text on stdout, exit 0 (`SuggestDirective` is added by `RootCommand`) | Experiment |
| `jira-cli @file` or `--project @file` | **Reads the file and turns its contents into arguments** (`ResponseFileTokenReplacer` is on by default) | Experiment |
| Unhandled exception | Written to stderr, exit 1, no envelope (`EnableDefaultExceptionHandler` defaults to true) | `InvocationPipeline` source |

Pointing `@file` at a config file put `SECRET-TOKEN-VALUE` in the parse-error messages. If parse errors go to `EnvelopeWriter` before config loads, the redactor may not know the secret yet, so FR-18 depends on load order. That last step is my inference from AD-11 and AD-13; I did not test it against a redactor. The practical impact is reduced because FR-17 accepts that the agent may read the file, but it still fails FR-18 as written.

**Tested configuration.** This produced one JSON document with exit 2 on every error case I tried, and `--help` and `--version` stayed plain text:

```csharp
var cfg = new ParserConfiguration { ResponseFileTokenReplacer = null };
root.Directives.Clear();
var pr = root.Parse(args, cfg);
if (pr.Action is ParseErrorAction) { /* EnvelopeWriter: usage, exit 2 */ }
```

**Recommendation:** add these three settings to AD-13 and one golden test for each. Also map exceptions inside the invocation middleware, and decide what Ctrl+C should emit: `ProcessTerminationTimeout` defaults to 2 seconds, and the process can end without an envelope.

### F5. List search returns IDs only by default, and 400 has no category

**Status: documented default, not reflected in the spine. Affects AD-7, AD-9, AD-14, AD-19.**

- **Post-check input.** With the default `fields`, the response has no `project` for `ObservedProjects` to inspect. An empty result set of observed projects must not count as a pass. `SearchIssuesRequest` must always send `fields=project,...`, and the Gateway should fail closed if an item has no project. The same applies to `GetIssueRequest` if it ever narrows `fields`.
- **Short pages.** "May return fewer items per page", so only `nextPageToken` or `isLast` may decide whether more exist. There is no `total`, so the output contract cannot promise one.
- **Status 400.** OpenAPI lists 400 for invalid search requests. Under AD-14 it falls into "Any other status: `unexpected`", exit 1. An expired or malformed `--page-token` (per JRACLOUD-94632, "invalid or expired"; tokens "expire in 7 days") should be a `usage` error the agent can act on. A 400 on a project the token cannot see probably belongs in the same discussion, but its exact behaviour needs a live check.
- **Authorization header.** These operations "can be accessed anonymously", and Jira "does not supply an authentication challenge" (<https://developer.atlassian.com/cloud/jira/platform/basic-auth-for-rest-apis/>). The transport must send `Authorization` on every request; it must not rely on `HttpClient` credentials reacting to a challenge. Otherwise a request can run anonymously and return empty results rather than an error.
- **Other.** `warnings` in the response is experimental and may report truncated results. Ignoring it is reasonable for now.

### F6. Attachment download should commit to `redirect=false`

**Status: documented; live behaviour unverified. Affects AD-5, AD-12, AD-14, Deferred.**

The default is a 303. Community reports consistently show the `Location` pointing to `api.media.atlassian.com` with a signed token:

- <https://github.com/epam/dmtools-dart/issues/461> (opened 2026-10-27; describes forwarded `Authorization` as a likely cause of failure, not a proven one)
- <https://community.sap.com/t5/technology-q-a/jira-attachment-api-redirecting-to-other-location-error-code-303/qaq-p/14009241>

That path conflicts with the spine. AD-5's `RelativeRequest` cannot express another host, and AD-12 limits redirect following to the same host. Also, 303 is not in the AD-14 table.

The documented alternative fits without any change: `redirect=false` returns 200, or 206 with `Range`. One report in the search results says a Forge developer saw different behaviour with this flag, so it is not proven for Basic auth through both endpoints.

Also, `AttachmentMetadata.content` is an absolute URL. In the likely case that it points at the site host, it must not be followed under gateway mode. Build the request path from the attachment id. (Inference from the schema; I did not see a live value.)

**Recommendation:** commit to `redirect=false` now and treat any other 3xx on this request as a defined error. Make one live smoke test against both endpoint types the first task of the attachment story, including item 14 above.

### F7. Stdout encoding and message language depend on the machine

**Status: confirmed by experiment. Not mentioned in the spine. Affects AD-13, AD-15.**

The same .NET 10 app writing `{"s":"日本語é😀"}` through `Console.Out`:

| Launch context | Output |
| --- | --- |
| Console code page 65001 (PowerShell, or Python with that console inherited) | UTF-8, correct |
| **Git Bash from this agent's Bash tool, no console, or a new console** | **cp932: `é` became `e`, the emoji became `??`** |

An output code page outside UTF-8 means Claude Code may receive corrupted text on a Japanese Windows machine, and the loss is permanent.

There is a mitigation: System.Text.Json "escapes all non-ASCII characters" by default (<https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-encoding>), which keeps the JSON ASCII-only. A developer who changes to a relaxed encoder to make Japanese readable or save tokens would trigger the corruption with no test failure on a UTF-8 developer machine. Plain-text help and stderr warnings are affected regardless.

Separately, System.CommandLine's messages follow the OS language. On this machine they came out in Japanese, so a `usage` message varies by machine and golden tests would too. Setting `SatelliteResourceLanguages=en` produced English messages and removed the extra resource folders from the build output.

**Recommendation:** add to AD-13 that `EnvelopeWriter` writes UTF-8 bytes directly to `Console.OpenStandardOutput()` (tested in the same context where `Console.Out` produced cp932: the direct stream produced correct UTF-8). Pin the encoder choice, set `SatelliteResourceLanguages=en`, and set the output encoding explicitly for help and warnings.

### F8. The recommended token does not work with the default endpoint

**Status: confirmed from docs; not tested live. Affects AD-3, AD-12, AD-14.**

AD-12 is technically right: site URL by default, gateway when `cloudId` is set. But AD-3 and FR-17 rely on the read-only token, and scoped tokens must use the gateway. A user who follows the README and forgets `cloudId` gets a 401 with a hint of "invalid or expired token, or missing scope", which points at the wrong cause. A user who "fixes" that by switching to a classic token loses the read-only protection without noticing.

**Recommendation:** add "scoped tokens also need `cloudId`" to the 401 hint in AD-14. Have `doctor` state which endpoint is in use. Put the `cloudId` lookup in the README. Token lifetime is 1 to 365 days, so an expired token will be a routine failure. `https://<site>/_edge/tenant_info` is mentioned by third parties only and I found no official reference for it, so the PRD addendum's admin.atlassian.com route is the safer one to document.

## Low findings

- **F9. `getpwuid` on Linux.** A P/Invoke to `getpwuid_r` worked on linux-arm64, and it ignored hostile `HOME` and XDG variables. A UID with no passwd entry returned no result, which matches AD-11's `config` failure. Name resolution with `"libc"` worked even with the `libc.so` linker script present, and so did `"libc.so.6"`.
  - **Name the right call:** AD-11 says `getpwuid`; prefer `getpwuid_r`. The BCL wrapper is internal, and `Environment.GetFolderPath` reads `HOME`, as the probe showed.
  - **Unverified:** musl/Alpine and x64. `libc.so.6` does not exist on musl, so run one Alpine test if Alpine containers are in scope for NFR-3.
  - **Usability:** `docker run --user <uid>` with no passwd entry is common, and it will fail by design. The README should say so.
- **F10. Package id `jira-cli` is unclaimed on nuget.org** (404 today). `dotnet tool update` without `--source` does consult nuget.org: the failure message from my test named `https://api.nuget.org/v3/index.json`. If someone publishes that id, an update run without the flag could install their package. AD-21 covers documented installs only. Extend it to updates, and consider a prefixed package id or reserving the name.
- **F11. Comment paging has no `isLast`.** Stop when `startAt + count >= total`, and also stop on an empty page so a loop cannot run forever. The OpenAPI's description of `total` ("number of items returned") reads oddly, so confirm its meaning on a live ticket. Changelog has `isLast`; use that.
- **F12. `resolution = Unresolved` default.** The JQL is valid, but the JQL reference says the field does not exist in service team-managed spaces, where `statusCategory` is the alternative (read through a search summary). The effect there is unknown and could be a 400 (F5). Confirm before relying on it across all projects, or consider `statusCategory != Done`.
- **F13. Version watch.** .NET 11 is expected on 2026-11-10 per the memlog, and System.CommandLine 3.0.0 is at rc.3, so it may ship around then. Stay pinned to 2.0.14, and check this review's F4 settings again before any move. .NET 9 reaches end of life on the same date, which has no effect here.

## Deferred table updates

| Deferred item | Suggested change |
| --- | --- |
| Moved-ticket behaviour | Move out of open questions: documented. An optional live check remains |
| Attachment `redirect=false` | Commit to `redirect=false` (F6). The live check stays, scoped-token access included |
| `Known Folder` immunity | **Remove.** Answered: not immune (F1). Becomes a change to AD-11 |
| `dotnet tool update --source` | **Close.** Works on both tested platforms |

## Memlog corrections

- `(version)` Jira entry: JRACLOUD-94632 is described as a known bug, but it has been closed as fixed since 2025-06-26. The decision stands.
- `(version)` NuGet entry: confirms only that the versions exist. Add a note that fit was checked here, with the F2 and F3 results.
- `(question)` entries can be resolved using the table above.
