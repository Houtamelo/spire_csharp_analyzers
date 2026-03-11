# Plan 006: Research — Claude Code Skills Best Practices

**Status**: Complete
**Goal**: Research how to write effective Claude Code skills, how to track skill usage, and how to improve our existing skills.

---

## 1. SKILL.md File Format

### Directory layout

```
skill-name/
├── SKILL.md           # Main instructions (required)
├── templates/         # Templates for scaffolding
├── scripts/           # Helper scripts
└── references/        # Detailed docs loaded on-demand
```

Only `SKILL.md` is required. Other files load on-demand when referenced, keeping context lean.

### Frontmatter schema

All fields optional except `description` (strongly recommended):

| Field | Type | Purpose |
|-------|------|---------|
| `name` | string | Slash command name (lowercase, hyphens, 64 chars max). Uses directory name if omitted. |
| `description` | string | **Primary trigger mechanism.** Must state WHAT the skill does AND WHEN to use it. Third person. |
| `argument-hint` | string | Autocomplete hint (e.g., `[RuleId]`) |
| `user-invocable` | boolean | `false` = hidden from `/` menu, only Claude can invoke. Use for background knowledge. |
| `disable-model-invocation` | boolean | `true` = only user can invoke via `/`. Claude cannot auto-trigger. Use for side effects. |
| `allowed-tools` | list | Tools Claude can use without permission during skill execution. Supports patterns: `Bash(dotnet *)`. |
| `model` | string | Override model when skill runs. |
| `context` | string | `fork` = run in isolated subagent context (loses conversation history). |
| `agent` | string | Subagent type when `context: fork`. Values: `Explore`, `Plan`, `general-purpose`. |
| `hooks` | object | Skill-scoped hooks (PreToolUse, PostToolUse, Stop). |

### String substitutions

Available in the markdown body:

| Variable | Value |
|----------|-------|
| `$ARGUMENTS` | All arguments passed to skill |
| `$ARGUMENTS[N]` or `$N` | Specific argument by index |
| `${CLAUDE_SESSION_ID}` | Current session ID |
| `${CLAUDE_SKILL_DIR}` | Path to skill directory |

### Command injection syntax

`!`command`` runs a shell command **before** Claude sees the prompt. Output replaces the placeholder:

```markdown
## Current test results
!`dotnet test --no-build 2>&1 | tail -5`

## Your task
Fix any failing tests shown above.
```

Claude receives the command output, not the command itself.

---

## 2. Characteristics of a Good Skill

### Description is critical

The `description` field is the primary trigger mechanism. Claude uses it to decide whether to invoke a skill. A vague description means the skill never fires; an overly broad one means it fires too often.

**Bad**: `"Helps with tests"`
**Good**: `"Build and run analyzer tests for a specific SAS rule. Use when asked to test, verify, or check a rule's behavior."`

Must include:
- WHAT the skill does
- WHEN to use it (trigger conditions)

### One concern per skill

Mega-skills that cover multiple workflows are an anti-pattern. They bloat context and confuse triggering. Each skill should have a single, clear purpose.

### Progressive disclosure

- Keep `SKILL.md` under 500 lines
- Move detailed reference docs to separate files
- Reference them with relative links: `For API specs, see [reference.md](reference.md)`
- Supporting files load on-demand, not at startup

### Instruction style

- Active, directive language ("Extract...", "Run...", not "I will extract...")
- Numbered sequential steps
- Concrete examples and exact output formats
- Explain the *why* behind non-obvious steps

### Risk-appropriate settings

| Skill type | Setting |
|------------|---------|
| Read-only / analysis | Default (Claude can auto-invoke) |
| Scaffolding / file creation | `disable-model-invocation: true` |
| Deployment / external effects | `disable-model-invocation: true` |
| Background knowledge | `user-invocable: false` |

---

## 3. Tracking Skill Usage

### Skill-scoped hooks

Skills can define hooks in their frontmatter that only fire during that skill's execution:

```yaml
---
name: example-skill
hooks:
  PostToolUse:
    - matcher: "Bash"
      command: "echo 'Tool used in skill' >> /tmp/skill-log.txt"
  Stop:
    - command: "bash .claude/hooks/log-skill-usage.sh"
---
```

Available hook events scoped to skills:
- `PreToolUse` — before tool execution (validate, deny, transform)
- `PostToolUse` — after tool execution (log, audit)
- `Stop` — when skill execution ends

### Integration with our feedback loop

The `Stop` hook on each skill can log invocation metadata:

```
Skill invoked → Stop hook fires → Appends to feedback/skill-usage.log
                                   (skill name, session ID, timestamp, outcome)
```

Then `/maintain-docs` reads `skill-usage.log` alongside session transcripts to identify:
- Which skills are actually used vs never invoked
- Which skills fail frequently (build errors after `/new-rule`, etc.)
- Which skills agents bypass (indicates they're not useful)

### Tracking data to capture

| Field | Source |
|-------|--------|
| Skill name | Hardcoded in hook script |
| Session ID | `${CLAUDE_SESSION_ID}` from hook input |
| Timestamp | `date -Iseconds` |
| Arguments | `$ARGUMENTS` (from hook input) |
| Outcome | Exit code of the last Bash command, or presence of errors in output |

---

## 4. Anti-Patterns

1. **Vague descriptions** — "helps with code" instead of specific trigger conditions
2. **Mega-skills** — one skill covering multiple workflows (our `/maintain-docs` has 4 phases)
3. **No invocation guards** — skills with side effects that Claude can auto-trigger
4. **No constraints** — skills that say what to do but never what NOT to do. Without explicit boundaries, Claude over-applies, edits files outside scope, or makes assumptions. Every skill should have a constraints section listing what to avoid, when to stop, and what's out of scope.
5. **Too many skills** — swells startup context. Sweet spot is ~5 regularly-used skills
6. **Skill replaces exploration** — skills should automate hardened workflows, not replace thinking
7. **No risk assessment** — letting Claude jump straight to implementation without validation
8. **Project-specific behavior in skills** — put project specifics in CLAUDE.md or rules; skills should be workflow patterns

---

## 5. Audit of Our Current Skills

### `/new-rule`
| Aspect | Current | Issue | Fix |
|--------|---------|-------|-----|
| Description | "Scaffold all files needed..." | Missing WHEN to trigger | Add "Use when adding a new SAS diagnostic rule to the project" |
| Invocation guard | None | Could auto-trigger on mentions of "new rule" | Add `disable-model-invocation: true` |
| Validation | None | No check that rule ID follows SAS format | Add validation step before creating files |
| Constraints | None | No guidance on what NOT to do (e.g., don't implement logic, don't modify existing rules) | Add constraints section |
| Hooks | None | No usage tracking | Add `Stop` hook |

### `/test`
| Aspect | Current | Issue | Fix |
|--------|---------|-------|-----|
| Description | "Build the solution and run all tests..." | Missing WHEN | Add "Use when asked to test, verify, or check analyzer behavior" |
| Command injection | None | Could pre-inject test results | Consider `!`dotnet test`` for immediate feedback |
| Constraints | None | No guidance on what NOT to do (e.g., don't modify tests to make them pass, don't skip failing tests) | Add constraints section |
| Hooks | None | No usage tracking | Add `Stop` hook |

### `/verify-rule`
| Aspect | Current | Issue | Fix |
|--------|---------|-------|-----|
| Description | "Check that a rule has all required files..." | Missing WHEN | Add "Use after implementing or modifying a rule to confirm completeness" |
| Constraints | None | No guidance on what NOT to do (e.g., don't create missing files, only report) | Add constraints section |
| Hooks | None | No usage tracking | Add `Stop` hook |

### `/syntax-tree`
| Aspect | Current | Issue | Fix |
|--------|---------|-------|-----|
| Description | Good — includes WHEN ("Use to discover which SyntaxNode types...") | OK | None needed |
| Implementation | Uses `echo "$ARGUMENTS" > /tmp/...` | Breaks on multiline code | Use `--stdin` flag with heredoc |
| Constraints | None | No guidance on what NOT to do (e.g., don't interpret the output, just return it) | Add constraints section |
| Hooks | None | No usage tracking | Add `Stop` hook |

### `/maintain-docs`
| Aspect | Current | Issue | Fix |
|--------|---------|-------|-----|
| Description | "Process accumulated session reports..." | Missing WHEN | Add "Use periodically to process feedback and update documentation" |
| Scope | 4 phases in one skill | Mega-skill anti-pattern | Consider splitting, or accept complexity since it's a deliberate workflow |
| Invocation guard | None | Should not auto-trigger | Add `disable-model-invocation: true` |
| Constraints | None | No guidance on what NOT to do (e.g., don't edit outside auto-managed markers, don't act on single reports) | Add constraints section |
| Hooks | None | No usage tracking | Add `Stop` hook |

### Cross-cutting gaps

1. **No skill tracks its own usage** — need `Stop` hooks logging to `feedback/skill-usage.log`
2. **No `disable-model-invocation`** on any skill — `/new-rule` and `/maintain-docs` are risky without it
3. **Descriptions lack WHEN** — 4 of 5 skills only say WHAT, not WHEN to trigger
4. **No constraints** — no skill specifies what NOT to do, leading to over-application and scope creep
5. **No command injection** — none use `!`command`` for pre-computed context
6. **No `${CLAUDE_SKILL_DIR}`** — `/new-rule` hardcodes template paths instead of using the variable

---

## 6. Recommended Changes

### A. Add skill usage logging

Create `.claude/hooks/log-skill-usage.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail
INPUT=$(cat)
SKILL_NAME="${1:-unknown}"
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id // "unknown"')
PROJECT_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
LOG_FILE="${PROJECT_ROOT}/feedback/skill-usage.log"
echo "$(date -Iseconds) | skill=${SKILL_NAME} | session=${SESSION_ID}" >> "$LOG_FILE"
```

Each skill adds a `Stop` hook calling this script with its name.

### B. Rewrite all skill descriptions

Format: `"{WHAT}. Use when {WHEN}."`

### C. Add invocation guards

- `/new-rule`: `disable-model-invocation: true`
- `/maintain-docs`: `disable-model-invocation: true`
- `/test`, `/verify-rule`, `/syntax-tree`: keep default (safe to auto-invoke)

### D. Use `${CLAUDE_SKILL_DIR}` in `/new-rule`

Replace hardcoded `.claude/skills/new-rule/templates/` with `${CLAUDE_SKILL_DIR}/templates/`.

### E. Fix `/syntax-tree` multiline handling

Use `--stdin` flag with heredoc instead of `echo` to a temp file.

### F. Add constraints to every skill

Every skill needs a "Do NOT" section specifying boundaries:

| Skill | Key constraints |
|-------|----------------|
| `/new-rule` | Don't implement detection logic (only scaffold). Don't modify existing rules. Don't create files if validation fails. |
| `/test` | Don't modify test code to make tests pass. Don't skip or ignore failing tests. Report failures as-is. |
| `/verify-rule` | Don't create missing files — only report what's missing. Don't modify existing code. Read-only verification. |
| `/syntax-tree` | Don't interpret or analyze the output. Just return the raw AST. Don't modify any project files. |
| `/maintain-docs` | Don't edit outside `<!-- AUTO-MANAGED -->` markers. Don't act on single-occurrence reports. Don't delete content — only flag for human review. |

### G. Integrate skill-usage.log into /maintain-docs

Add a step that reads `feedback/skill-usage.log` and reports:
- Skills never invoked (candidates for removal)
- Skills invoked frequently (candidates for optimization)
- Skills with consistent post-invocation errors (need fixing)

---

## Sources

- [Extend Claude with skills — Claude Code Docs](https://code.claude.com/docs/en/skills.md)
- [Automate workflows with hooks — Claude Code Docs](https://code.claude.com/docs/en/hooks-guide.md)
- [Skill authoring best practices — Claude API Docs](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)
- [Claude Agent Skills: A First Principles Deep Dive](https://leehanchung.github.io/blogs/2025/10/26/claude-skills-deep-dive/)
- [Inside Claude Code Skills: Structure, prompts, invocation](https://mikhail.io/2025/10/claude-code-skills/)
- [GitHub — anthropics/skills](https://github.com/anthropics/skills)
- [GitHub — travisvn/awesome-claude-skills](https://github.com/travisvn/awesome-claude-skills)
- [GitHub — mgechev/skills-best-practices](https://github.com/mgechev/skills-best-practices)
