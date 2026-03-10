# Plan 003: Research — Agent Feedback Loops in Practice

**Status**: Complete
**Goal**: Learn from existing implementations of agent feedback loops before building our own.

---

## Existing Implementations

### Transcript Capture & Session Analysis

| Project | Approach | Key Insight |
|---------|----------|-------------|
| **CASS** ([Dicklesworthstone/coding_agent_session_search](https://github.com/Dicklesworthstone/coding_agent_session_search)) | Indexes sessions from 11+ providers (Claude Code, Cursor, Copilot, etc.) into unified schema. BM25 + vector search, sub-60ms queries. | Normalize transcript formats early. Supports agent-to-agent queries via "robot-mode" API. |
| **claude-mem** ([thedotmack/claude-mem](https://github.com/thedotmack/claude-mem)) | 5 lifecycle hooks capture tool usage to SQLite, generates AI summaries, injects relevant memories via vector DB. 33.7k stars. | Progressive disclosure (50-100 tokens initially, expand on demand) gives ~10x token savings. |
| **claude-log-cli** (Martin Alderson) | Parses JSONL history at `~/.claude/projects/` to identify CLAUDE.md improvements. | Agents are bad at parsing JSONL directly — use a dedicated script. |
| **Built-in `/insights`** | Processes up to 50 sessions with Haiku, extracts structured facets, generates CLAUDE.md recommendations. | Prioritizes patterns that appear **multiple times** — single-occurrence learnings are unreliable. |

### Self-Improving Documentation

| Project | Approach | Key Insight |
|---------|----------|-------------|
| **Ralph** ([snarktank/ralph](https://github.com/snarktank/ralph)) | Autonomous loop appends learnings to AGENTS.md after each iteration. Future iterations read it. | "Each improvement makes future improvements easier" — compound effect is real. |
| **claude-diary** ([rlancemartin/claude-diary](https://github.com/rlancemartin/claude-diary)) | Two-phase: `/diary` captures learnings, `/reflect` updates CLAUDE.md. Reflection is deliberately **manual**. | Separating capture from apply prevents bad learnings from auto-propagating. |
| **claude-code-auto-memory** ([severity1/claude-code-auto-memory](https://github.com/severity1/claude-code-auto-memory)) | Fully automatic. PostToolUse tracks files, Stop hook spawns isolated agent to update CLAUDE.md. Uses marker-delimited sections. | Auto-managed sections coexist with manual content via `<!-- AUTO-MANAGED: section-name -->` markers. |
| **claude-meta** ([aviadr1/claude-meta](https://github.com/aviadr1/claude-meta)) | Single reflection trigger: "Reflect on this mistake. Abstract and generalize. Write it to CLAUDE.md." | Compounding works: Session 1 catches basics, Session 2 hits deeper issues, Session 3 reaches architecture. |
| **NitinWabale's self-improving instructions** | When Claude is corrected, it asks "Should I update the rules?" | Correction-triggered updates are more reliable than periodic reflection. |

### Two-Agent Review Loops

| Project | Approach | Key Insight |
|---------|----------|-------------|
| **Spotify Honk** (production-scale) | Deterministic verifiers + LLM judge reviews diffs. Judge vetoes ~25% of changes; agents course-correct ~50% of the time. | Most common rejection: agents exceeding their instructions. Independent reviewers catch what self-review misses. |
| **claude-review-loop** ([hamelsmu/claude-review-loop](https://github.com/hamelsmu/claude-review-loop)) | Uses **Stop hook** to run Codex as independent reviewer. Blocks Claude's exit until feedback is addressed. | Two-agent review (different models) outperforms same-agent self-review. |

### Hook-Based Feedback Systems

| Project | Hooks Used | Key Insight |
|---------|-----------|-------------|
| **disler/claude-code-hooks-mastery** | All 13 lifecycle events. Structured JSON logging, PostToolUse transcript conversion. | Comprehensive reference implementation — good template. |
| **ContextStream** | 5 hooks: session-init, smart-context, on-file-change, on-bash, pre-compact. | Learns from errors in bash commands — captures command + error + resolution. |
| **everything-claude-code** | PreToolUse, PostToolUse, Stop. Confidence-scored "instinct" learning, `/evolve` converts instincts to skills. | Instinct → skill promotion requires validation threshold before becoming permanent. |

---

## Known Hook Limitations (Bugs / Issues)

| Issue | Impact on Our Design |
|-------|---------------------|
| **SubagentStop shares session IDs** across subagents ([#7881](https://github.com/anthropics/claude-code/issues/7881)) | Can't reliably identify *which* subagent finished from session_id alone. Use `agent_type` matcher instead. |
| **Stop hook doesn't fire** when Claude stalls mid-turn after tool result ([#29881](https://github.com/anthropics/claude-code/issues/29881)) | Some sessions may not get transcript capture. SessionEnd is more reliable for capture. |
| **SubagentStop prompt feedback** is received but subagent never gets another turn ([#20221](https://github.com/anthropics/claude-code/issues/20221)) | Our agent-type SubagentStop hook should write to files, not try to give feedback back to the subagent. Our design already does this correctly. |
| **SessionStart hook output** not injected into context for new conversations ([#10373](https://github.com/anthropics/claude-code/issues/10373)) | Don't rely on SessionStart hooks for memory injection. Use CLAUDE.md instead. |

---

## Lessons That Should Change Our Design

### 1. Separate capture from apply (claude-diary pattern)

**Current plan**: SubagentStop agent hook both captures and evaluates in one step.
**Better**: Capture always (command hook), evaluate only on request (`/maintain-docs`). The agent-type hook that auto-evaluates adds latency to every subagent exit and may produce low-quality assessments under the 60s timeout. Let the doc-maintainer do thorough evaluation with unlimited time.

### 2. Require multiple occurrences before acting (/insights pattern)

**Current plan**: doc-maintainer fixes every reported issue.
**Better**: doc-maintainer should track issue frequency. A single agent reporting "this doc is unclear" might be wrong. Two or three agents reporting the same issue is a reliable signal. Add a frequency/confidence threshold.

### 3. Periodic pruning is non-negotiable

**Current plan**: No pruning mechanism.
**Better**: `/maintain-docs` should also consolidate and prune. Remove redundant entries, merge overlapping guidance, and flag instructions that haven't been relevant in N sessions. Without this, docs degrade over time — every project that skipped pruning reported problems.

### 4. Token efficiency matters

**Current plan**: Embed full transcript in feedback reports.
**Better**: Full transcripts can be enormous (thousands of lines). Store the transcript as a separate file and reference it from the report. The doc-maintainer reads the transcript only when needed, not every time it scans the feedback directory.

### 5. Two-agent review beats self-review (Spotify, claude-review-loop)

**Current plan**: The same agent-type hook evaluates its own subagent's work.
**Good news**: Our design already uses a separate doc-maintainer agent for the actual fixes. But we should ensure the SubagentStop evaluator (if we keep it) is a genuinely independent agent, not the same model that just finished the work.

### 6. Auto-managed sections with markers (claude-code-auto-memory)

**Current plan**: doc-maintainer edits docs freely.
**Better**: Use `<!-- AUTO-MANAGED: feedback-driven -->` markers in docs to delineate sections that the doc-maintainer can edit vs sections that are manually curated. Prevents the doc-maintainer from accidentally overwriting intentional human/agent decisions.

### 7. Hard constraints beat optional suggestions (Fanelli's finding)

**Current plan**: CLAUDE.md "optionally" suggests running `/feedback`.
**Consideration**: If we make feedback truly optional, agents will skip it most of the time. The automatic SubagentStop transcript capture is the reliable path. Don't over-invest in the voluntary `/feedback` skill — focus engineering effort on the automatic pipeline.

---

## Recommended Changes to Plan 002

Based on this research:

1. **Drop the SubagentStop agent-type hook** — Capture transcripts via command hook only. Evaluation happens in `/maintain-docs`, not at subagent exit time. This avoids the 60s timeout, the SubagentStop prompt feedback bug (#20221), and low-quality rushed assessments.

2. **Store transcripts as separate files** — Don't embed them in the markdown report. Report references the transcript file. The doc-maintainer reads transcripts on demand.

3. **Add frequency tracking to doc-maintainer** — Keep a `feedback/issue-tracker.md` that tallies how many times each file/section has been flagged. Only act on issues with 2+ independent reports (or a single report with high-confidence evidence like a build failure).

4. **Add pruning to `/maintain-docs`** — After processing reports, scan docs for redundancy, conflicts, and staleness. Remove instructions that no feedback report has ever referenced.

5. **Use auto-managed markers in documentation** — Sections the doc-maintainer can edit are wrapped in `<!-- AUTO-MANAGED -->` comments. Everything else is manual-only.

6. **Keep `/feedback` skill but don't over-promote it** — It's there if agents want it, but the automatic pipeline is the primary mechanism.

---

## Sources

- [Addy Osmani - Self-Improving Coding Agents](https://addyosmani.com/blog/self-improving-agents/)
- [Martin Alderson - Self-improving CLAUDE.md files](https://martinalderson.com/posts/self-improving-claude-md-files/)
- [Claude Code /insights Deep Dive](https://www.zolkos.com/2026/02/04/deep-dive-how-claude-codes-insights-command-works.html)
- [Latent Space - Can coding agents self-improve?](https://www.latent.space/p/self-improving)
- [Lance Martin - Claude Diary](https://rlancemartin.github.io/2025/12/01/claude_diary/)
- [Spotify Engineering - Feedback Loops (Part 3)](https://engineering.atspotify.com/2025/12/feedback-loops-background-coding-agents-part-3)
- [ICLR 2025 - SICA Paper](https://openreview.net/pdf?id=rShJCyLsOr)
- GitHub repos: snarktank/ralph, hamelsmu/claude-review-loop, thedotmack/claude-mem, severity1/claude-code-auto-memory, aviadr1/claude-meta, rlancemartin/claude-diary, disler/claude-code-hooks-mastery, Dicklesworthstone/coding_agent_session_search, affaan-m/everything-claude-code, letta-ai/letta-code
- Claude Code GitHub issues: #7881, #29881, #20221, #10373
