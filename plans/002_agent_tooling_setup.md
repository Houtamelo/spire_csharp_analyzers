# Plan 002: Agent Tooling & Setup

**Status**: Draft
**Goal**: Define all the tools, documentation, and instructions needed so that any future Claude Code agent can work on this project effectively without prior context.

---

## What We Need to Create

### 1. CLAUDE.md — Project Instructions

**Location**: `CLAUDE.md` (project root)

The single most important file. Every agent session loads this automatically. Must contain:

- **Project overview**: What this is, what it replaces, the rule ID prefix
- **Build & test commands**: Exact commands to build, test, and pack
- **Project structure**: Where analyzers, code fixes, tests, and docs live
- **Coding conventions**: Naming, patterns, and anti-patterns specific to Roslyn analyzers
- **Rule creation checklist**: Step-by-step process for adding a new rule (files to create, patterns to follow)
- **References to deeper docs**: `@` imports to plans, architecture docs, and rule templates

Should stay under 200 lines. Deep details go in `.claude/rules/` files.

### 2. `.claude/rules/` — Path-Specific Guidelines

Rules that activate only when the agent is working on files matching specific paths.

#### `analyzer-conventions.md`
```yaml
---
paths:
  - "src/Analyzer/**/*.cs"
---
```
Contents:
- Every analyzer must inherit `DiagnosticAnalyzer`
- Every analyzer must define `DiagnosticDescriptor` as `public static readonly`
- Use `CompilationStartAction` when you need to resolve types; cache them
- Register the narrowest action type possible (`SyntaxNodeAction` > `SyntaxTreeAction` > `CompilationAction`)
- Target `netstandard2.0` — no C# features beyond what netstandard2.0 supports at runtime
- All dependencies must use `PrivateAssets="all"`

#### `codefix-conventions.md`
```yaml
---
paths:
  - "src/CodeFixes/**/*.cs"
---
```
Contents:
- Every code fix must inherit `CodeFixProvider` and be decorated with `[ExportCodeFixProvider]`
- Must override `FixableDiagnosticIds` returning the matched analyzer's ID(s)
- Must override `GetFixAllProvider()` returning `WellKnownFixAllProviders.BatchFixer`
- Register fixes via `context.RegisterCodeFix(CodeAction.Create(...))`

#### `test-conventions.md`
```yaml
---
paths:
  - "tests/**/*.cs"
---
```
Contents:
- Use `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` with xUnit
- Use project-level `Verifiers.cs` helper (wraps `CSharpAnalyzerVerifier` and `CSharpCodeFixVerifier`)
- Test method naming: `{Scenario}_Should{Not}Report{DiagnosticId}`
- Every analyzer must have:
  - At least 3 positive tests (code that should NOT trigger)
  - At least 3 negative tests (code that SHOULD trigger)
  - Code fix tests if a fix exists (before/after pairs)
- Embed test source code as string literals, use `[|...|]` markup for expected diagnostic spans
- Test against both `struct` and `record struct` where applicable

#### `documentation-conventions.md`
```yaml
---
paths:
  - "docs/rules/**/*.md"
---
```
Contents:
- One `.md` file per rule, named after the rule ID (e.g., `SA0001.md`)
- Standard template: Title, Severity, Category, Description, Examples (violating + compliant), How to Fix, Configuration, When to Suppress

### 3. Custom Skills (`.claude/skills/`)

#### `/new-rule` — Scaffold a New Analyzer Rule
```yaml
---
name: new-rule
description: Scaffold all files needed for a new analyzer rule. Creates the analyzer, code fix, tests, and documentation.
user-invocable: true
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: [RuleId] [RuleTitle]
---
```
Workflow:
1. Parse `$ARGUMENTS` for rule ID (e.g., `SA0005`) and title
2. Read the template files from `.claude/skills/new-rule/templates/`
3. Create: `src/Analyzer/Rules/{Category}/{RuleId}Analyzer.cs`
4. Create: `src/CodeFixes/Rules/{Category}/{RuleId}CodeFixProvider.cs`
5. Create: `tests/{RuleId}Tests.cs`
6. Create: `docs/rules/{RuleId}.md`
7. Register the new diagnostic ID in the central descriptor registry
8. Run `dotnet build` to verify compilation

#### `/test` — Build & Run Tests
```yaml
---
name: test
description: Build the solution and run all tests, or tests for a specific rule.
user-invocable: true
allowed-tools: Bash, Read, Grep
argument-hint: [optional:RuleId]
---
```
Workflow:
1. If `$ARGUMENTS` is empty: `dotnet test` (all tests)
2. If a rule ID is given: `dotnet test --filter "FullyQualifiedName~{RuleId}"`
3. Report results concisely

#### `/verify-rule` — Validate a Rule is Complete
```yaml
---
name: verify-rule
description: Check that a rule has all required files (analyzer, tests, docs) and passes tests.
user-invocable: true
allowed-tools: Bash, Read, Glob, Grep
argument-hint: [RuleId]
---
```
Workflow:
1. Check analyzer file exists and has `DiagnosticDescriptor`
2. Check test file exists with positive and negative tests
3. Check documentation file exists with required sections
4. Check code fix exists (warn if missing, don't fail)
5. Run tests for that rule
6. Report pass/fail summary

### 4. Custom Agents (`.claude/agents/`)

#### `analyzer-implementer`
```yaml
---
name: analyzer-implementer
description: Implements a Roslyn analyzer rule from a specification. Use when implementing a new diagnostic rule.
tools: Read, Write, Edit, Glob, Grep, Bash
model: opus
maxTurns: 30
skills:
  - new-rule
  - test
  - verify-rule
---
```
Prompt:
- You are a Roslyn analyzer expert
- Read the rule specification from the plans/ folder
- Use `/new-rule` to scaffold, then implement the detection logic
- Write comprehensive tests
- Run `/verify-rule` to confirm completeness
- Follow all conventions in `.claude/rules/`

#### `test-writer`
```yaml
---
name: test-writer
description: Writes thorough test suites for existing analyzer rules. Use to expand test coverage.
tools: Read, Write, Edit, Glob, Grep, Bash
model: opus
maxTurns: 20
---
```
Prompt:
- You are a testing expert for Roslyn analyzers
- Read the analyzer source to understand what it detects
- Add edge case tests: generics, nested structs, partial classes, record structs, ref structs
- Ensure both positive (no diagnostic) and negative (diagnostic reported) coverage
- Test code fixes if they exist

#### `researcher`
```yaml
---
name: researcher
description: Researches C# struct pitfalls, Roslyn APIs, or existing analyzer implementations. Use for investigation tasks.
tools: Read, Glob, Grep, Bash, WebSearch, WebFetch
model: opus
maxTurns: 15
---
```
Prompt:
- You are a researcher investigating C# struct behavior and Roslyn analyzer patterns
- Search the web, read documentation, examine existing analyzer source code
- Write findings to the `plans/` folder
- Focus on accuracy — cite sources

### 5. Hooks (`.claude/settings.json`)

#### Post-edit: Format with `dotnet format`
```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "FILE=$(cat | jq -r '.tool_input.file_path // empty'); if [[ \"$FILE\" == *.cs ]]; then dotnet format --include \"$FILE\" 2>/dev/null; fi"
          }
        ]
      }
    ]
  }
}
```

#### Pre-bash: Block dangerous operations
```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          {
            "type": "command",
            "command": "CMD=$(cat | jq -r '.tool_input.command // empty'); if echo \"$CMD\" | grep -qE 'rm\\s+-rf|git\\s+push\\s+--force|git\\s+reset\\s+--hard'; then echo 'Blocked destructive command' >&2; exit 2; fi"
          }
        ]
      }
    ]
  }
}
```

### 6. Settings (`.claude/settings.json`)

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet build *)",
      "Bash(dotnet test *)",
      "Bash(dotnet format *)",
      "Bash(dotnet pack *)",
      "Bash(dotnet restore *)",
      "Bash(dotnet new *)",
      "Bash(mkdir *)",
      "Bash(ls *)",
      "Bash(git status*)",
      "Bash(git diff*)",
      "Bash(git log*)",
      "Bash(git add *)",
      "Bash(git commit *)"
    ]
  }
}
```

### 7. Template Files

#### `.claude/skills/new-rule/templates/`

Pre-written boilerplate files with placeholders (`{{RULE_ID}}`, `{{RULE_TITLE}}`, `{{CATEGORY}}`) that the `/new-rule` skill fills in:

- `AnalyzerTemplate.cs` — DiagnosticAnalyzer boilerplate with descriptor, Initialize(), and analysis method
- `CodeFixTemplate.cs` — CodeFixProvider boilerplate
- `TestTemplate.cs` — Test class with positive/negative/codefix test stubs
- `DocTemplate.md` — Rule documentation template

### 8. Documentation Structure

```
docs/
├── rules/
│   ├── SA0001.md          # Per-rule documentation
│   ├── SA0002.md
│   └── ...
├── architecture.md         # How the analyzer works internally
└── contributing.md         # How to add new rules (for agents and humans)
```

`contributing.md` is especially important — it's the step-by-step guide that agents follow when implementing rules. It should mirror the `/new-rule` skill workflow but in prose form.

### 9. Feedback Loop System

The goal: automatically capture what agents did, so a dedicated doc-maintainer can identify and fix documentation/skill issues. Agents don't need to do anything — transcripts are captured by hooks. They *can* voluntarily annotate via `/feedback`, but the automatic pipeline is the primary mechanism.

**Design informed by**: Plan 003 research (claude-diary, Spotify Honk, /insights, claude-code-auto-memory, known hook bugs).

#### Key Insight: Transcripts Are Already on Disk

Claude Code writes every session to a JSONL file:
- **Main sessions**: `~/.claude/projects/<project>/<session-id>.jsonl`
- **Subagent sessions**: `~/.claude/projects/<project>/<session-id>/subagents/agent-<id>.jsonl`

Hooks receive the path to these files:
- `SubagentStop` → `agent_transcript_path` (subagent's JSONL)
- `SessionEnd` → `transcript_path` (main session's JSONL)

Each JSONL line is a JSON object with `type` ("user"/"assistant"), `message` (with `role` and `content`), and `timestamp`. This contains everything: what the agent was asked, what it thought, what it wrote, and all tool interactions.

#### How It Works

```
Agent finishes work
        ↓
Hook fires (SubagentStop or SessionEnd)
        ↓
Command hook runs capture-session.sh:
  1. Copies the transcript JSONL to feedback/transcripts/
  2. Writes a lightweight report stub referencing the transcript
        ↓
Transcripts accumulate (no evaluation at capture time)
        ↓
User runs /maintain-docs periodically
        ↓
doc-maintainer reads transcripts, identifies issues,
checks frequency, updates docs, prunes stale content
```

**Why capture-only, no auto-evaluation at hook time** (lesson from claude-diary, /insights):
- Separating capture from evaluation prevents rushed, low-quality assessments under the 60s hook timeout
- The SubagentStop agent-hook has a known bug: prompt feedback is received but the subagent never gets another turn (#20221)
- The `/insights` system found that single-occurrence learnings are unreliable — frequency matters
- The doc-maintainer can take unlimited time to do thorough evaluation

#### Session Capture Script

**`.claude/hooks/capture-session.sh`**

A shell script that copies the transcript and creates a lightweight report stub. No LLM involved.

```bash
#!/usr/bin/env bash
set -euo pipefail

# Read hook input from stdin
INPUT=$(cat)

# Extract paths and metadata
TRANSCRIPT_PATH=$(echo "$INPUT" | jq -r '.agent_transcript_path // .transcript_path // empty')
HOOK_EVENT=$(echo "$INPUT" | jq -r '.hook_event_name // "unknown"')
AGENT_TYPE=$(echo "$INPUT" | jq -r '.agent_type // "main-session"')
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id // "unknown"')

# Skip if no transcript path
[ -z "$TRANSCRIPT_PATH" ] && exit 0
[ ! -f "$TRANSCRIPT_PATH" ] && exit 0

# Determine project root (where feedback/ lives)
PROJECT_ROOT="$(git -C "$(echo "$INPUT" | jq -r '.cwd')" rev-parse --show-toplevel 2>/dev/null || echo "$INPUT" | jq -r '.cwd')"
FEEDBACK_DIR="${PROJECT_ROOT}/feedback"
TRANSCRIPT_DIR="${FEEDBACK_DIR}/transcripts"
mkdir -p "$TRANSCRIPT_DIR"

# Generate filenames
TIMESTAMP=$(date +%Y-%m-%d_%H-%M-%S)
TRANSCRIPT_COPY="${TRANSCRIPT_DIR}/${TIMESTAMP}_${AGENT_TYPE}.jsonl"
REPORT_FILE="${FEEDBACK_DIR}/${TIMESTAMP}_${AGENT_TYPE}.md"

# Copy transcript (not embed — keeps reports lightweight)
cp "$TRANSCRIPT_PATH" "$TRANSCRIPT_COPY"

# Write lightweight report stub
cat > "$REPORT_FILE" << REPORT_EOF
# Session Report
**Date**: $(date -Iseconds)
**Agent type**: ${AGENT_TYPE}
**Session ID**: ${SESSION_ID}
**Hook event**: ${HOOK_EVENT}
**Status**: unprocessed
**Transcript**: transcripts/${TIMESTAMP}_${AGENT_TYPE}.jsonl

## Issues Found
_Not yet evaluated. Will be analyzed by /maintain-docs._

## Notes
_Will be evaluated by /maintain-docs._
REPORT_EOF

exit 0
```

Key difference from previous design: transcript is **copied as a separate JSONL file**, not embedded in the markdown report. This keeps reports scannable and avoids bloating the feedback directory with multi-thousand-line markdown files.

#### Hook Configuration

Command hooks only — no agent-type hooks at capture time:

```json
{
  "hooks": {
    "SubagentStop": [
      {
        "matcher": "analyzer-implementer|test-writer|researcher",
        "hooks": [
          {
            "type": "command",
            "command": "bash .claude/hooks/capture-session.sh"
          }
        ]
      }
    ],
    "SessionEnd": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash .claude/hooks/capture-session.sh"
          }
        ]
      }
    ]
  }
}
```

**Why command hooks only** (no agent-type SubagentStop hook):
- Avoids the 60s timeout and SubagentStop prompt feedback bug (#20221)
- Capture is deterministic — no LLM variance
- Evaluation happens later in `/maintain-docs` with no time pressure
- No voluntary action required from agents — everything is automatic

#### `/maintain-docs` Skill — Processing Feedback

```yaml
---
name: maintain-docs
description: Process accumulated session reports, identify documentation issues, and apply fixes. Run this periodically.
user-invocable: true
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---
```

Workflow has three phases:

**Phase 1: Evaluate unprocessed reports**
1. Read all files in `feedback/` where status is `unprocessed`
2. For each report, read the referenced transcript JSONL
3. Identify doc/skill issues: incorrect information, missing guidance, confusing instructions, stale examples
4. Record findings in `feedback/issue-tracker.md` with file path, issue description, and source report

**Phase 2: Apply fixes (frequency-gated)**
5. Read `feedback/issue-tracker.md`
6. Only act on issues reported by **2+ independent sessions** (or 1 session if backed by concrete evidence like a build failure in the transcript)
7. For each actionable issue:
   - Read the referenced doc/skill file
   - Only edit sections within `<!-- AUTO-MANAGED: feedback-driven -->` markers (or add new auto-managed sections)
   - Make minimal, targeted fixes
8. If an issue requires substantial work, create a plan in `plans/` instead of fixing directly

**Phase 3: Prune and consolidate**
9. Scan all docs/skills for:
   - Redundant instructions (same thing said in multiple places)
   - Conflicting guidance
   - Instructions that no feedback report has ever referenced (candidates for removal)
10. Report pruning candidates but don't auto-delete — flag them for human review

**Phase 4: Archive**
11. Move processed reports to `feedback/archived/`
12. Print summary of all changes made and issues deferred

#### `doc-maintainer` Agent

```yaml
---
name: doc-maintainer
description: Analyzes session reports and updates documentation, skills, and conventions. Use when running /maintain-docs.
tools: Read, Write, Edit, Glob, Grep, Bash
model: opus
maxTurns: 30
---
```

Prompt:
- You maintain the project's documentation, skills, and agent instructions
- Read session reports from `feedback/` (status: unprocessed)
- For each report, read the transcript JSONL to understand what the agent did and where it struggled
- Record issues in `feedback/issue-tracker.md` with tallies
- Only fix issues with 2+ independent reports (or 1 report with concrete evidence like build/test failure)
- Only edit content within `<!-- AUTO-MANAGED: feedback-driven -->` markers — never touch manually curated content outside markers
- Make minimal, targeted fixes — don't rewrite entire files
- After fixing, scan docs for redundancy and staleness — flag candidates for pruning
- Move processed reports to `feedback/archived/`
- If an issue requires substantial work, create a plan in `plans/` instead of fixing directly

#### Auto-Managed Markers

Documentation files that the doc-maintainer can edit use marker comments to delineate auto-managed sections:

```markdown
## Some Manual Section
This content was written by a human or an implementing agent. The doc-maintainer
will never modify this section.

<!-- AUTO-MANAGED: feedback-driven -->
## Common Pitfalls (Auto-Updated)
- When testing record structs, use `record struct` not `struct record` (reported in 3 sessions)
- The `[|...|]` markup only works with `DefaultVerifier` — if using a custom verifier, use explicit `DiagnosticResult` (reported in 2 sessions)
<!-- /AUTO-MANAGED -->

## Another Manual Section
...
```

This prevents the doc-maintainer from accidentally overwriting intentional decisions while still allowing it to keep frequently-reported lessons visible.

#### Issue Tracker

**`feedback/issue-tracker.md`** — persistent file tracking issue frequency:

```markdown
# Issue Tracker

## Open Issues

| File | Issue | Reports | Sessions | Status |
|------|-------|---------|----------|--------|
| `.claude/rules/test-conventions.md` | Markup syntax example is wrong | 3 | 2026-03-09, 2026-03-10, 2026-03-11 | **actionable** (3 reports) |
| `docs/contributing.md` | Missing ref struct guidance | 1 | 2026-03-09 | waiting (need 2+) |

## Resolved Issues

| File | Issue | Fixed On | Reports Before Fix |
|------|-------|----------|--------------------|
| ... | ... | ... | ... |
```

#### Directory Structure

```
feedback/
├── 2026-03-09_14-30-00_analyzer-implementer.md   # lightweight report stub
├── 2026-03-09_16-45-00_test-writer.md             # auto-captured report stub
├── 2026-03-09_18-00-00_main-session.md            # SessionEnd capture
├── issue-tracker.md                                # frequency tracking across reports
├── transcripts/
│   ├── 2026-03-09_14-30-00_analyzer-implementer.jsonl  # full JSONL transcript
│   ├── 2026-03-09_16-45-00_test-writer.jsonl
│   └── 2026-03-09_18-00-00_main-session.jsonl
├── archived/
│   ├── 2026-03-08_10-00-00_researcher.md           # processed by /maintain-docs
│   └── ...
.claude/
├── hooks/
│   └── capture-session.sh                          # transcript capture script
```

#### Report Lifecycle

```
1. [unprocessed]  → Transcript captured by hook, no evaluation yet
2. [processed]    → /maintain-docs evaluated it → moved to archived/
```

Issues go through a separate lifecycle in `issue-tracker.md`:
```
1. [waiting]      → Reported once, needs more evidence
2. [actionable]   → Reported 2+ times, ready to fix
3. [resolved]     → Fix applied, moved to resolved table
```

---

## File Creation Order

When implementing this plan, create files in this order:

1. **Solution & projects** — `dotnet new` commands, .csproj files, Directory.Build.props
2. **CLAUDE.md** — Immediately, so subsequent agent work follows conventions
3. **`.claude/rules/`** — Path-specific conventions for analyzers, tests, docs
4. **Template files** — Analyzer, code fix, test, and doc templates
5. **Skills** — `/new-rule`, `/test`, `/verify-rule`, `/maintain-docs`
6. **Agents** — `analyzer-implementer`, `test-writer`, `researcher`, `doc-maintainer`
7. **Settings & hooks** — Permissions, formatting hooks, SubagentStop + SessionEnd capture hooks
8. **Feedback directory** — `feedback/`, `feedback/transcripts/`, `feedback/archived/`, `feedback/issue-tracker.md`
9. **Auto-managed markers** — Add `<!-- AUTO-MANAGED: feedback-driven -->` sections to docs that the doc-maintainer should be able to update
10. **Documentation** — `architecture.md`, `contributing.md`

---

## Verification

After setup is complete, verify by:
1. Run `dotnet build` — solution compiles
2. Run `dotnet test` — test infrastructure works (even with zero rules)
3. Run `/new-rule SA0001 "Test Rule"` — scaffolding creates all expected files
4. Run `/verify-rule SA0001` — validation passes
5. Run `/test SA0001` — tests execute (even if they're stubs)
6. Verify capture hook works — trigger a subagent, check that `feedback/transcripts/` and `feedback/*.md` are created
7. Run `/maintain-docs` — processes reports, updates issue tracker
8. A fresh Claude Code session loads CLAUDE.md and can navigate the project without additional context
