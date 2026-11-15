# Live check against the real Jira

`tools/JiraCli.LiveCheck` is an engineer-run console app (Story 1.3b). It is not a test and is never
packaged: `dotnet test` never runs it and never reaches a real host. It proves that the Story 1.3
transport authenticates against the real Jira with both token kinds before Story 1.6 builds the
Gateway on it.

## What it does

It takes no arguments and reads no environment variables. It:

1. Loads the real config from the one fixed location (`%APPDATA%\jira-cli\config.json` on Windows,
   `~/.config/jira-cli/config.json` on Linux), the same way jira-cli does. See
   [config.example.json](config.example.json).
2. Prints the endpoint that `AtlassianEndpoint` picked.
3. Sends, in order and with no retry, `GET rest/api/3/myself` and then
   `GET rest/api/3/project/<KEY>` for every key in `allowedProjects`. A failure does not stop the
   remaining requests.
4. Prints each request path with its status and the body as Jira sent it. A failed request shows the
   failure kind (`Status`, `Timeout`, or `Connection`), the status code, `Retry-After`, and the
   response body, or, for a timeout or connection failure, one `Inner exception:` line per exception
   in the inner-exception chain (type and `HttpRequestError` only, never messages).

It never prints the configured token or any header. The `myself` body shows your account, including
your email address, so do not paste raw bodies into committed docs.

Exit codes: `0` every request returned 2xx, `1` at least one did not, `2` the config failed to load
(the message and hint are printed and nothing is sent).

## How to run it

In VS Code, pick the **LiveCheck** launch configuration and press F5. It builds the tool first. From
a terminal:

```shell
dotnet run --project tools/JiraCli.LiveCheck
```

Run it twice, editing the config between runs:

| Run | `apiToken` | `cloudId` | Expected endpoint |
| --- | --- | --- | --- |
| Classic | a classic API token | absent | `https://<site>.atlassian.net/` |
| Scoped | a scoped, read-only API token | your site's cloud id | `https://api.atlassian.com/ex/jira/<cloudId>/` |

Your site's cloud id is the `cloudId` in `https://<site>.atlassian.net/_edge/tenant_info`.

## What to look for

- The endpoint line matches the table above.
- `myself` returns 200 with your account, and each allowed project returns 200 with its `key`.
- No line in the console shows the token, or a `Basic` authorization value.
- A 401 or 403 usually means an invalid or expired token, a missing scope, or a scoped token used
  without `cloudId`. A 404 on a project means the key is wrong or the account cannot see it.
- Anything unexpected: a different status, a redirect (shown as a 3xx `Status` failure, since the
  transport follows no redirects), a different endpoint shape, or scopes the scoped token needs.

## Recording differences

Every difference from AD-12 (endpoint shape, authentication, redirects, required scopes) must be
recorded before Story 1.6 starts, in both places:

- AD-12 in `_bmad-output/planning-artifacts/architecture/architecture-jira-cli-2026-11-01/ARCHITECTURE-SPINE.md`
- the Implementation Notes of `_bmad-output/implementation-artifacts/spec-1-3b-live-check-tool-against-real-jira.md`

If both runs return 2xx for every request, record that too.
