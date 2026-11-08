# Agent Loop

The workflow jira-cli serves. jira-cli owns only the Jira read steps; any change to Jira stays manual.

| Step | Who | jira-cli use |
| --- | --- | --- |
| 1. Engineer asks for unresolved tickets; agent lists them | Agent | `list --project <keys>`; keys come from the engineer or the `projectNotAllowed` hint |
| 2. Engineer picks one; agent reads it fully | Agent | `get`, `related`, `related <origin> --key <k>`, `attachments list`, `attachments download`, `history` |
| 3. Agent asks clarifying questions | Agent, engineer | None |
| 4. Agent implements and opens a pull request | Agent | None |
| 5. After merge, agent drafts root cause and countermeasure, re-reading the ticket if needed; engineer pastes it into Jira | Agent, engineer | `get` |

Usage guide rules for the agent (AD-22):

- Pass `--project`; learn allowed keys from the `projectNotAllowed` hint, never by reading the config.
- Read relations with `related`, and their content with `related <origin> --key <k>`, so the origin is logged.
- Read attachments from the returned absolute path, copying into the repository only if needed.
- Pass `data.notices` on to the engineer. A missing notice does not prove there is no other-project child.
- Never edit the config.
