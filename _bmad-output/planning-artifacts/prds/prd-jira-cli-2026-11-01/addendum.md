# jira-cli PRD Addendum

Content that belongs to downstream documents (architecture, solution design) or that earned a place but does not fit the PRD itself.

## For Architecture

### Technical constraints stated by the user

- Language: C# (.NET), must run on Windows and Linux (x64 and arm64).
- Use the standard .NET Dependency Injection container; decompose into interface-based, injectable services for testability.
- TDD-first development.
- Architecture-level decomposition must be reviewed before implementation starts.
- Shared concerns (HTTP client, auth, config, logging, output formatting) should be modular so a future Confluence client can reuse them.

### Notes from PRD decisions

- The CLI is stateless. To record the originating ticket key for a related ticket (FR-25), the request itself must carry the origin, for example as a "related tickets of X" retrieval or an explicit origin argument.
- The user configuration file holds the Jira site, credentials, allowed projects, default list size (initial 20), and the list upper limit (initial 100).
- Request timeout: 30 seconds (NFR-2).

### Jira Cloud API and tokens (2026-11-01; community and support sources, verify against official docs)

- REST API v3. Classic (unscoped) token base URL: `https://<site>.atlassian.net/rest/api/3/...`.
- Scoped tokens must go through the gateway: `https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/...`. The cloudId is site-specific and can be found via admin.atlassian.com.
- Auth: HTTP Basic with account email plus API token.
- A read-only scoped token uses `read:jira-work` and `read:jira-user`. A classic token carries everything the account can do.
- A token cannot be limited to specific projects; it sees exactly what the account sees in the browser. The usual workaround is a dedicated account whose permission scheme grants only Browse on the target projects.
- Any user can create personal API tokens unless an org admin restricts it. Tokens created after 2024-12-15 expire after 1 day to 1 year; expiry cannot be disabled, so rotation and a clear "token expired" error matter.
- Implication: jira-cli enforces read-only and the allowed projects in the tool itself (defense in depth) and does not rely on the token alone.

Sources:

- [Manage API tokens for your Atlassian account](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/)
- [Manage API tokens for service accounts](https://support.atlassian.com/user-management/docs/manage-api-tokens-for-service-accounts/)
- [Are API tokens read-only or write-only? (Atlassian Community)](https://community.atlassian.com/forums/Atlassian-Account-questions/Are-API-TOKENS-read-only-or-write-only/qaq-p/2923564)
- [ID-8726 (Atlassian feature request)](https://jira.atlassian.com/browse/ID-8726)
- [atlassian-mcp (Glama)](https://glama.ai/mcp/servers/atatter/atlassian-mcp)
- [Jira (API Token) – Connection Guide (Apideck)](https://developers.apideck.com/apis/issue-tracking/jira-api-token/connection)

### Protecting the user configuration file from the agent (FR-12)

Decision: the README documents the file's fixed location and how to protect it. Ideas for the README:

- Keep the file outside any project workspace (for example a user config directory), never in the repo.
- Docker: mount the file read-only (`-v host/path:container/path:ro`), so nothing inside the container can change it.
- WSL / Linux: make the file owned by a different user or root, read-only for the user running Claude Code.
- Claude Code: add `deny` permission rules for the file path in Claude Code settings as an extra layer.

Rejected alternative: refusing to start, or warning, when the file is writable by the current user. Dropped in favor of README guidance to keep setup simple.

### Distribution options (open question)

Runtime context: Claude Code runs inside WSL or Docker containers, and also on Windows. Targets: Linux and Windows, x64 and arm64.

| Option | How it reaches the dev environment | Notes |
| --- | --- | --- |
| .NET global tool (`dotnet tool install`) from an internal NuGet feed or a shared folder | `dotnet tool install -g jira-cli --add-source <feed>` | Requires the .NET SDK or runtime in every container; easy updates. |
| Self-contained single-file binary (`dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true`) | Download from internal releases (for example GitHub or GitLab Releases), or `COPY` into the container image | No .NET runtime needed; one file per OS and architecture; simplest for Docker. |
| Bake into the team's base dev-container image | Image layer or devcontainer feature | Zero setup for users; update cadence tied to image rebuilds. |

Initial lean (to be decided in architecture): self-contained single-file binaries published per runtime identifier (RID), which can also be layered into the dev-container image.
