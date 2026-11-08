# Setup Reference

Facts the README and `doctor` stories build on. Jira facts come from published documentation and community sources, not live tests (2026-11).

## Jira Cloud access

- Auth is HTTP Basic with account email and API token, sent on every request.
- Classic tokens use `https://<site>.atlassian.net/rest/api/3/...` and carry everything the account can do.
- Scoped tokens must use `https://api.atlassian.com/ex/jira/<cloudId>/rest/api/3/...`. The `cloudId` is found through admin.atlassian.com.
- Read-only scopes: `read:jira-work` for issues, search, comments, changelog, attachments, and projects; `read:jira-user` for `myself`.
- A token sees exactly what its account sees, and cannot be limited to projects. The usual workaround is a dedicated account granted only Browse on the target projects.
- Tokens created after 2024-12-15 expire after 1 day to 1 year, so rotation and a clear expired-token error matter.

## Config file

Windows `<RoamingAppData>\jira-cli\config.json`; Linux, WSL, and Docker `<passwd home>/.config/jira-cli/config.json`.

```jsonc
{
  "site": "acme",
  "email": "engineer@example.com",
  "apiToken": "...",
  "cloudId": "...",            // only for scoped tokens
  "jira-cli": {
    "allowedProjects": ["ABC"],
    "customFieldPassthrough": [],
    "defaultPageSize": 20,
    "maxPageSize": 100
  }
}
```

## Protecting the config from the agent

The agent may read the file but must not change it. Protect the file and every ancestor directory:

- Keep it outside every project workspace.
- Docker: mount it read-only (`-v <host path>:<container path>:ro`).
- WSL and Linux: make the file and its directories owned by another user or root, read-only for the user running Claude Code.
- Containers: run the agent as non-root, with `/etc/passwd` not writable by it, and give its UID a passwd entry.
- Claude Code: add `deny` rules for edits and writes to the file path.

## Install and update

```bash
dotnet tool install -g jira-cli --source <folder with .nupkg>
dotnet tool update -g jira-cli --source <folder with .nupkg>
export PATH="$PATH:$HOME/.dotnet/tools"
```

```dockerfile
COPY nupkg/ /tmp/nupkg/
RUN dotnet tool install -g jira-cli --source /tmp/nupkg
```

Always pass `--source`; `--add-source` or no source also consults nuget.org, where anyone can publish a package named `jira-cli`.

## Network

Proxy selection follows .NET defaults, which honor environment and system proxy settings. The department uses a proxy whose effect on the route to Jira is unknown; TLS inspection may require installing the corporate root certificate in WSL and Docker.

## Sources

- [Manage API tokens for your Atlassian account](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/)
- [Manage API tokens for service accounts](https://support.atlassian.com/user-management/docs/manage-api-tokens-for-service-accounts/)
- [Jira Cloud REST API v3 OpenAPI](https://developer.atlassian.com/cloud/jira/platform/swagger-v3.v3.json)
- [dotnet tool install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install)
