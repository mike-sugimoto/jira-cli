# Glossary

| Term | Meaning |
| --- | --- |
| Ticket, Issue | A Jira work item of any type: epic, story, bug, task, subtask, or custom. "Ticket" in prose, `Issue` in code. |
| Agent | Claude Code, the caller of jira-cli. |
| Engineer | The single person who installs, configures, and runs jira-cli with their own credentials. |
| Allowed projects | The projects listed in `allowedProjects`; the only ones whose content jira-cli returns. |
| User configuration file | The engineer-maintained `config.json` at the fixed path (AD-11). Connection settings at the top level, tool settings under `jira-cli`. The agent may read it and must not change it. |
| Related ticket | A ticket reached from another through parent, subtask, child (a ticket whose parent it is), or issue link. |
| Origin | The ticket a `related <origin> --key <k>` traversal starts from; recorded in the work log. |
| Direct access | Reading a ticket, history, or attachments by key without `related`; logged with no origin. |
| Loop | The request-to-merged-PR workflow in `agent-loop.md`. |
| Envelope | The single JSON document on stdout: `{ formatVersion, ok, data?, error? }` (AD-13). |
| Notice | A non-fatal message in `data.notices`, `{ code, message, hint }` from a fixed template (AD-10, AD-15). |
| Managed attachment folder | The per-ticket folder jira-cli owns for downloads (AD-17). |
| Passthrough field | A custom field listed in `customFieldPassthrough`, output as raw JSON when its type cannot be rendered (AD-16). |
| Request scope | The access declaration a Jira Request carries, enforced by `JiraGateway` (AD-7). Not the same as a token's scopes. |
| Token scopes | Atlassian permissions on a scoped API token, such as `read:jira-work`. |
| Verification token | `VerifiedIssue` or `AttachmentRef`: an internal object the Gateway issues only after a passed check (AD-8). |
