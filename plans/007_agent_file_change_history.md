# Plan 007: Session Summaries, Git Integration & Agent File Change History

**Status**: Draft
**Goal**: Create a lightweight session summary system that integrates with git, and a dedicated history-keeper agent that tracks changes to agent infrastructure files.

**Supersedes**: The original draft of this plan (agent self-reporting approach). The new design derives history from session summaries and git diffs instead.

---

## Problem

1. **No audit trail for agent infrastructure changes.** When a skill, rule, or agent definition is modified, there's no record of why. Git tracks the diff but not the intent.
2. **Session transcripts are too large to track in git.** Raw JSONL transcripts can be megabytes — full of tool call noise. They're gitignored and ephemeral.
3. **No structured link between commits and sessions.** When reviewing git history, there's no way to trace a commit back to the agent session that produced it.

---

## Design Overview

```
Session ends
    ↓
capture-session.sh (existing hook):
  1. Copies raw transcript to feedback/transcripts/ (gitignored)
  2. Generates a session summary in feedback/summaries/ (git-tracked)  ← NEW
    ↓
User commits when ready
    ↓
Commit message lists session summaries since last commit  ← NEW
    ↓
User runs /update-history (or it runs automatically)  ← NEW
    ↓
history-keeper agent:
  1. Reads recent commits for agent infrastructure file changes
  2. Reads associated session summaries to understand why
  3. Appends entries to .claude/HISTORY.md
```

---

## Part 1: Session Summaries

### What is a session summary?

A small, structured markdown file extracted from the raw JSONL transcript by a shell script (no LLM). Contains only the signal:

- What the user/agent was asked to do
- Which files were created, edited, or deleted
- Build/test outcomes
- Errors encountered

### Summary format

```markdown
# Session Summary

**Date**: 2026-03-10T14:30:00+00:00
**Agent type**: analyzer-implementer
**Session ID**: abc123

## Task
> Implement SAS001 rule from plan 005 spec

## Files Modified
- `src/Spire.Analyzers/Analyzers/SAS001ArrayOfNonDefaultableStructAnalyzer.cs` (created)
- `src/Spire.Analyzers/Descriptors.cs` (edited)
- `tests/Spire.Analyzers.Tests/SAS001Tests.cs` (created)
- `docs/rules/SAS001.md` (created)

## Build/Test Results
- `dotnet build`: success (0 warnings, 0 errors)
- `dotnet test`: 14 passed, 0 failed

## Errors Encountered
_None_
```

### Summary generation script

**`generate-summary.sh`** — a pure `jq` + `bash` script that parses the JSONL transcript.

Extraction logic:

| Field | Source in JSONL |
|-------|----------------|
| Task | First `type: "user"` message content (truncated to 200 chars) |
| Files Modified | All `tool_input.file_path` from Edit/Write tool calls, deduplicated |
| Files Read | All `tool_input.file_path` from Read tool calls (optional, may omit for brevity) |
| Build/Test Results | stdout/stderr from `dotnet build` and `dotnet test` Bash calls — extract last line (summary) |
| Errors Encountered | Any Bash tool calls with non-zero exit codes — capture command + first line of stderr |

The script reads the JSONL transcript and writes a markdown summary. No LLM, no network calls, runs in < 1 second.

### Storage

```
feedback/
├── summaries/                    # Git-tracked
│   ├── 2026-03-10_14-30-00_analyzer-implementer.md
│   ├── 2026-03-10_16-45-00_test-writer.md
│   └── 2026-03-10_18-00-00_main-session.md
├── transcripts/                  # Gitignored (raw JSONL, large)
│   └── ...
├── archived/                     # Gitignored
│   └── ...
├── issue-tracker.md              # Git-tracked
└── skill-usage.log               # Gitignored
```

### Changes to capture-session.sh

The existing hook currently:
1. Copies transcript to `feedback/transcripts/`
2. Writes a report stub to `feedback/`

Updated behavior:
1. Copies transcript to `feedback/transcripts/` (unchanged)
2. Runs `generate-summary.sh` on the transcript → writes to `feedback/summaries/`
3. Writes a report stub to `feedback/` that references both the transcript and summary

The lightweight report stubs in `feedback/*.md` can be **removed** — the summaries replace them. The report stubs were always just placeholders for `/maintain-docs` to evaluate; the summaries serve the same purpose with more useful content.

---

## Part 2: Git Commit Integration

### Commit message format

When committing changes that involved agent sessions, the commit message should reference the summaries:

```
Add SAS001 analyzer rule

Sessions:
- feedback/summaries/2026-03-10_14-30-00_analyzer-implementer.md
- feedback/summaries/2026-03-10_16-45-00_test-writer.md
```

### How to generate

Option A: **Manual convention.** The user or committing agent includes session references. Simple but easy to forget.

Option B: **Script that lists uncommitted summaries.** A helper script finds all summary files not yet referenced in any commit message:

```bash
#!/usr/bin/env bash
# list-pending-sessions.sh
# Lists summary files created since the last commit
git diff --name-only --cached -- feedback/summaries/
git ls-files --others -- feedback/summaries/
```

The user runs this before committing to see which sessions to reference, or a commit skill injects them automatically.

Option C: **Pre-commit hook.** Automatically appends pending summaries to the commit message. This is the most reliable but also the most intrusive — the user might not want every commit to reference sessions.

**Recommended: Option B** — a helper script that a commit skill can call. Not forced on every commit, but available and easy to use.

---

## Part 3: History-Keeper Agent

### Purpose

A dedicated agent that reads git history, identifies changes to agent infrastructure files, cross-references session summaries, and writes structured entries to `.claude/HISTORY.md`.

**Key principle: agents don't maintain their own history.** They stay focused on their tasks. The history-keeper derives everything retroactively from git diffs and session summaries.

### Tracked files (agent infrastructure)

| Path | Contents |
|------|----------|
| `.claude/agents/*.md` | Agent definitions |
| `.claude/skills/**/SKILL.md` | Skill instructions |
| `.claude/skills/**/templates/*` | Scaffolding templates |
| `.claude/rules/*.md` | Path-specific conventions |
| `.claude/hooks/*.sh` | Hook scripts |
| `.claude/settings.json` | Permissions and hooks config |
| `CLAUDE.md` | Project-level instructions |
| `docs/contributing.md` | Rule creation guide |
| `docs/architecture.md` | Internal design overview |

### Agent definition

```yaml
---
name: history-keeper
description: Reads git history and session summaries to maintain .claude/HISTORY.md. Use after committing changes that modified agent infrastructure files.
tools: Read, Write, Edit, Glob, Grep, Bash
model: opus
maxTurns: 15
---
```

### Agent workflow

1. **Find recent commits with agent infrastructure changes.**
   ```
   git log --oneline --diff-filter=M -- .claude/ CLAUDE.md docs/contributing.md docs/architecture.md
   ```
   Compare against the last entry date in `.claude/HISTORY.md` to only process new commits.

2. **For each commit, get the diff.**
   ```
   git diff <commit>~1 <commit> -- <tracked paths>
   ```

3. **Find associated session summaries.**
   - Check the commit message for `feedback/summaries/` references
   - If none referenced, find summaries with timestamps close to the commit date (within ±1 hour)
   - Read those summaries to understand the context

4. **Write a HISTORY.md entry** for each changed file, following the entry format:
   ```markdown
   ## [2026-03-10] `analyzer-implementer` — `.claude/skills/test/SKILL.md`

   **What**: Added constraint "Do NOT modify analyzer code" to the test skill.
   **Why**: Session summary shows test-writer was modifying analyzer source to fix failing tests.
   **Commit**: a1b2c3d
   **Session**: feedback/summaries/2026-03-10_14-30-00_analyzer-implementer.md
   ```

5. **If no summary is available** (e.g., manual edit by user), write a shorter entry:
   ```markdown
   ## [2026-03-10] `manual` — `.claude/agents/analyzer-implementer.md`

   **What**: Removed netstandard2.0 constraint line.
   **Why**: Manual edit — no session summary available. See commit a1b2c3d.
   **Commit**: a1b2c3d
   ```

### HISTORY.md format

```markdown
# Agent Infrastructure Change History

Append-only log of changes to agent infrastructure files.
Newest entries first. Maintained by the history-keeper agent.

---

## [2026-03-10] `manual` — `.claude/agents/analyzer-implementer.md`

**What**: Removed netstandard2.0 constraint line.
**Why**: Manual edit — no session summary available.
**Commit**: a1b2c3d

---
```

### Entry fields

| Field | Required | Description |
|-------|----------|-------------|
| Date | Yes | ISO date from the commit |
| Actor | Yes | Agent name from session summary, or `manual` for human edits |
| File(s) | Yes | Which tracked file(s) were modified |
| What | Yes | One sentence describing the change (derived from git diff) |
| Why | Yes | Why the change was necessary (derived from session summary, or "no summary available") |
| Commit | Yes | Short commit hash |
| Session | No | Path to session summary if available |

### Triggering the history-keeper

**Option A: `/update-history` skill (recommended)**

A user-invocable skill that spawns the history-keeper agent:

```yaml
---
name: update-history
description: Update .claude/HISTORY.md with entries for recent commits that changed agent infrastructure files. Use after committing changes to agent files.
disable-model-invocation: true
user-invocable: true
context: fork
agent: history-keeper
---
```

The user runs `/update-history` after committing. Simple, explicit, no surprises.

**Option B: Post-commit git hook**

A git `post-commit` hook that checks if agent infrastructure files were modified and runs the history-keeper automatically. More reliable but spawns an agent on every commit — potentially slow and expensive.

**Recommended: Option A** initially. Option B can be added later if the user frequently forgets.

---

## Part 4: Changes to Existing Systems

### `/maintain-docs` simplification

Currently `/maintain-docs` reads report stubs in `feedback/` and then reads transcripts. With summaries:

- Phase 1 reads `feedback/summaries/` instead of report stubs + transcripts
- Summaries are more structured and smaller — faster to process
- Raw transcripts are still available in `feedback/transcripts/` for deep investigation (if still on disk)
- Report stubs (`feedback/*.md`) are no longer generated — summaries replace them

The history-keeping responsibility is **removed** from `/maintain-docs` entirely — the history-keeper agent handles it.

### .gitignore changes

```diff
- # Feedback (local-only)
- feedback/transcripts/
- feedback/archived/
- feedback/skill-usage.log
+ # Feedback — local-only (transcripts are large)
+ feedback/transcripts/
+ feedback/archived/
+ feedback/skill-usage.log
+
+ # Summaries are git-tracked (do NOT gitignore feedback/summaries/)
```

No actual change needed — `feedback/summaries/` isn't gitignored. Just ensure it's not accidentally excluded.

### SubagentStop hook matcher

Add `history-keeper` to the SubagentStop matcher so its sessions are also captured:

```json
"matcher": "analyzer-implementer|test-writer|researcher|history-keeper"
```

---

## Part 5: File Inventory

### Files to create

| File | Purpose |
|------|---------|
| `.claude/hooks/generate-summary.sh` | Extracts summary from JSONL transcript |
| `.claude/HISTORY.md` | Append-only change history |
| `.claude/agents/history-keeper.md` | History-keeper agent definition |
| `.claude/skills/update-history/SKILL.md` | Skill to trigger history-keeper |
| `tools/list-pending-sessions.sh` | Helper listing uncommitted session summaries |
| `feedback/summaries/` | Directory for git-tracked summaries |

### Files to modify

| File | Change |
|------|--------|
| `.claude/hooks/capture-session.sh` | Call `generate-summary.sh` instead of writing report stubs |
| `.claude/settings.json` | Add `history-keeper` to SubagentStop matcher |
| `.claude/skills/maintain-docs/SKILL.md` | Read summaries instead of report stubs; remove history responsibility |
| `.claude/agents/doc-maintainer.md` | Remove history instruction; read summaries instead of transcripts |
| `CLAUDE.md` | Remove any self-reporting convention (history-keeper handles it) |
| `.gitignore` | Ensure `feedback/summaries/` is NOT ignored |

### Files to delete

| File | Reason |
|------|--------|
| _None_ | Report stubs stop being generated but existing ones aren't deleted |

---

## Part 6: Implementation Order

1. Create `generate-summary.sh` — the core extraction script
2. Create `feedback/summaries/` directory
3. Update `capture-session.sh` — integrate summary generation
4. Create `.claude/HISTORY.md` — initial empty file
5. Create `.claude/agents/history-keeper.md` — agent definition
6. Create `.claude/skills/update-history/SKILL.md` — trigger skill
7. Create `tools/list-pending-sessions.sh` — commit helper
8. Update `.claude/settings.json` — add history-keeper to SubagentStop matcher
9. Update `.claude/skills/maintain-docs/SKILL.md` — use summaries, remove history responsibility
10. Update `.claude/agents/doc-maintainer.md` — use summaries
11. Verify: run a test session, check that summary is generated, commit, run `/update-history`

---

## Example: Full Lifecycle

```
1. User asks analyzer-implementer to implement SAS002
2. Agent works: creates files, runs tests
3. Session ends → capture-session.sh fires:
   - Raw transcript → feedback/transcripts/2026-03-12_14-30-00_analyzer-implementer.jsonl (gitignored)
   - Summary → feedback/summaries/2026-03-12_14-30-00_analyzer-implementer.md (git-tracked)
4. User reviews changes, commits:
   "Implement SAS002 defensive copy detection

   Sessions:
   - feedback/summaries/2026-03-12_14-30-00_analyzer-implementer.md"
5. User notices the agent also updated a test convention rule
6. User runs /update-history
7. History-keeper agent:
   - Finds commit touched .claude/rules/test-conventions.md
   - Reads the session summary to understand why
   - Appends to .claude/HISTORY.md:
     [2026-03-12] analyzer-implementer — .claude/rules/test-conventions.md
     What: Added note about testing defensive copies with ref readonly locals.
     Why: Session implementing SAS002 found test conventions lacked guidance for ref readonly testing patterns.
     Commit: f4e5d6c
     Session: feedback/summaries/2026-03-12_14-30-00_analyzer-implementer.md
8. User commits the HISTORY.md update
```
