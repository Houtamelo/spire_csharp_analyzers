# Plan 008: Research — Claude Code Agent Teams

## Context

Claude Code recently shipped `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` as an experimental feature. This plan documents how the feature works and evaluates whether/how Spire.Analyzers should adopt it.

## Verification Status

This plan was initially written from two research agents' outputs. It was then cross-checked against the official docs at `code.claude.com/docs/en/agent-teams` and `code.claude.com/docs/en/hooks`. Claims not found in official docs are marked with **> Note** callouts throughout. Key corrections applied:

- **Delegate mode (`Shift+Tab`)**: Not found in official docs. Removed as a verified feature.
- **Environment variables**: Not documented. Marked as unverified.
- **Task JSON schema details** (`owner`, `blockedBy`, `blocks`): Not documented. Simplified to what's verifiable from hook inputs.
- **Inbox file paths**: Not documented. Removed specific path claim.
- **Message type taxonomy**: Not documented. Simplified to documented operations (`message`, `broadcast`).
- **TeammateTool operations list**: Not documented. Removed.

## How Agent Teams Work

### Architecture

Agent teams coordinate **multiple independent Claude Code instances** (each with its own context window) working together on a shared codebase. Four components:

| Component | Description |
|-----------|-------------|
| **Team Lead** | The main Claude Code session. Creates the team, spawns teammates, assigns work, reviews results. |
| **Teammates** | Separate Claude Code processes. Work independently, communicate via mailbox and shared task list. |
| **Shared Task List** | Stored in `~/.claude/tasks/{team-name}/`. Supports dependency tracking and file-locking for safe claiming. |
| **Mailbox** | Messaging system for communication between agents. Supports direct messages and broadcasts. |

### No Config Files

Teams are **not** declared in `.claude/agents/` or any config file. The lead creates them via natural language:

```
Create a team with 3 teammates:
- One focused on analyzer implementation
- One writing tests
- One reviewing for correctness
```

Claude spawns separate processes. Each loads the same project context (CLAUDE.md, skills, MCP servers) but does **not** inherit the lead's conversation history.

### Task Coordination

- Tasks are stored in `~/.claude/tasks/{team-name}/`. Tasks have at least `task_id`, `task_subject`, and `task_description` fields (visible in hook inputs). Full internal schema is not documented.
- File locking prevents race conditions when multiple teammates try to claim the same task.
- Tasks support dependencies: a pending task with unresolved dependencies cannot be claimed until those dependencies are completed. Dependencies auto-unlock when blocking tasks complete.
- Teammates self-claim the next unblocked task when they finish their current one.
- The lead can assign tasks explicitly, or teammates can self-claim.

### Inter-Agent Communication

- Teammates can message **each other directly** — not just report back to the lead.
- Two messaging operations: `message` (to one specific teammate) and `broadcast` (to all teammates — use sparingly, costs scale with team size).
- Messages are delivered automatically (no polling needed).
- Idle notifications: when a teammate finishes and stops, they automatically notify the lead.

> **Note**: The exact message type taxonomy (e.g., `shutdown_request`, `idle_notification`) is not documented. The docs describe messaging conceptually.

### Lead Controls

- **Preventing self-implementation**: The lead sometimes starts implementing instead of coordinating. The docs recommend telling the lead: "Wait for your teammates to complete their tasks before proceeding."
- **Plan approval**: Teammates can be required to plan before implementing. They work in read-only plan mode until the lead approves. If rejected, the teammate revises and resubmits. The lead makes approval decisions autonomously — influence its judgment via criteria in the prompt.
- **Model selection**: Different models per teammate (e.g., Opus lead for coordination, Sonnet teammates for execution — cost optimization).

> **Note**: The agents' research claimed a "delegate mode" activated by `Shift+Tab`. This is **not found** in the official docs. The docs only suggest telling the lead to wait.

### Display Modes

| Mode | How it works | Requirement |
|------|-------------|-------------|
| `in-process` | All teammates in main terminal. `Shift+Down` to cycle, `Ctrl+T` for task list. | None |
| `tmux` | Each teammate in its own tmux pane. | tmux or iTerm2 |
| `auto` (default) | Uses split panes if in tmux, in-process otherwise. | — |

### Hooks

Two team-specific hooks:

- **`TeammateIdle`**: Fires when a teammate is about to go idle. Exit code 2 sends stderr as feedback and keeps teammate working. Can output `{"continue": false, "stopReason": "..."}` to stop. **Only supports `type: "command"` hooks.** No matcher support. Input includes `teammate_name` and `team_name`.
- **`TaskCompleted`**: Fires when a task is being marked as completed (either explicitly via TaskUpdate, or when a teammate finishes with in-progress tasks). Exit code 2 prevents completion. **Supports all four hook types** (command, http, prompt, agent). No matcher support. Input includes `task_id`, `task_subject`, `task_description` (optional), `teammate_name` (optional), `team_name` (optional).

> **Note**: The agents' research claimed specific environment variables injected into teammates (`CLAUDE_CODE_TEAM_NAME`, `CLAUDE_CODE_AGENT_ID`, etc.). These are **not documented** in the official docs. They may exist internally but cannot be verified from public documentation.

## Agent Teams vs Subagents

| Aspect | Subagents (.claude/agents/) | Agent Teams |
|--------|---------------------------|-------------|
| Communication | Report results back to caller only | Direct teammate-to-teammate messaging |
| Coordination | Main agent manages everything sequentially | Self-coordinating via shared task list |
| Definition | Declared in `.md` files with frontmatter | Created dynamically via natural language |
| Context | Same project context, no conversation history | Same |
| Inter-agent awareness | None — subagents don't know about each other | Full — teammates can discuss, challenge, share |
| Best for | Focused single tasks | Complex multi-faceted collaborative work |
| Token cost | Lower (one extra context window) | Higher (N separate context windows) |
| Resumability | Can resume individual agents | No session resumption for teammates |

## Limitations

1. **No session resumption** — `/resume` doesn't restore teammates. After resume, lead may try to message non-existent teammates.
2. **One team per session** — Must clean up before starting another.
3. **No nested teams** — Teammates cannot spawn their own teams.
4. **Same-file edits cause overwrites** — Teammates must work on different files.
5. **Token costs scale linearly** — Each teammate has its own full context window (significantly more expensive).
6. **Split panes require tmux/iTerm2** — VS Code terminal, Windows Terminal, Ghostty not supported.
7. **Task status lag** — Teammates sometimes fail to mark tasks as completed, blocking dependents.
8. **Slow shutdown** — Teammates finish current request/tool call before stopping.
9. **Lead may self-implement** — The lead sometimes starts implementing instead of coordinating. Tell it to wait for teammates.
10. **Permissions fixed at spawn** — All teammates start with lead's permission mode.

## Applicability to Spire.Analyzers

### Where Teams Would Help

**1. Batch rule implementation (high value)**
When implementing multiple rules simultaneously (e.g., SAS002 + SAS003 + SAS004), each teammate owns one rule end-to-end: analyzer file, test file, doc file. These are all separate files, so no conflict risk. The only shared file is `Descriptors.cs` — the lead can handle descriptor additions before spawning teammates, or serialize those edits.

**2. Rule implementation with parallel tracks (medium value)**
For a single complex rule, one teammate implements the analyzer while another writes tests. Works because they touch different files (`Analyzers/SAS001*.cs` vs `SAS001Tests.cs`). However, the test writer needs to understand the analyzer's behavior, so inter-agent messaging becomes important.

**3. Research + implementation (medium value)**
A researcher teammate investigates Roslyn APIs or C# edge cases while an implementer starts scaffolding based on existing plan docs.

**4. Comprehensive review (low-medium value)**
After implementing a rule, multiple reviewers check different aspects: correctness, edge cases, performance. But our `/verify-rule` skill already covers this for a single agent.

### Where Teams Are Overkill or Problematic

- **Single sequential rule implementation** — Steps are naturally sequential (plan → descriptor → analyzer → tests → docs). The dependency chain limits parallelism.
- **Documentation maintenance** — Single-agent task, no collaboration needed.
- **Any task heavily touching shared files** — `Descriptors.cs`, `Verifiers.cs`, `.csproj` files. Conflict risk.
- **Small tasks** — Token cost of N context windows isn't justified for quick fixes.

### Recommended Adoption Strategy

**Phase 1 (now)**: Don't adopt. SAS001 isn't implemented yet. Subagents are simpler, cheaper, and sufficient for single-rule work.

**Phase 2 (after 3-5 rules exist)**: Experiment with teams for batch implementation. Pattern:
1. Lead adds all descriptors to `Descriptors.cs` (serialized, no conflict)
2. Lead spawns one teammate per rule
3. Each teammate implements analyzer + tests + docs for their assigned rule
4. Lead reviews results

**Phase 3 (mature)**: Use teams for major refactors or cross-cutting changes that span multiple rules.

### Key Pattern for This Project

If adopted, the ideal team structure would be:

```
Lead (Opus): Coordinates, adds descriptors, reviews
├── Teammate 1 (Sonnet): SAS00X — analyzer + tests + docs
├── Teammate 2 (Sonnet): SAS00Y — analyzer + tests + docs
└── Teammate 3 (Sonnet): SAS00Z — analyzer + tests + docs
```

Critical rule: **one teammate per rule, never per layer** (i.e., don't have "the test writer" and "the analyzer writer" — have "the SAS002 owner"). This avoids same-file conflicts and reduces inter-agent coordination overhead.

### Integration with Existing Tooling

- **Skills**: Teammates load all skills automatically. `/new-rule`, `/test`, `/verify-rule` work inside teammates.
- **Hooks**: Team-specific hooks (`TeammateIdle`, `TaskCompleted`) are available. Whether `SubagentStop`/`SessionEnd` also fire for teammates is not explicitly documented — would need testing.
- **CLAUDE.md**: Loaded by all teammates — conventions are enforced everywhere.
- **Team-specific hooks**: `TaskCompleted` could run `dotnet build && dotnet test` as a quality gate before allowing task completion.

## Sources

- [Official Docs: Orchestrate teams of Claude Code sessions](https://code.claude.com/docs/en/agent-teams)
- [Official Docs: Hooks reference](https://code.claude.com/docs/en/hooks)
- [Addy Osmani: Claude Code Swarms](https://addyosmani.com/blog/claude-code-agent-teams/)
- [Claude Code Agent Teams: The Complete Guide 2026](https://claudefa.st/blog/guide/agents/agent-teams)
- [Claude Code CHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)
