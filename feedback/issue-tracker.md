# Issue Tracker

Last processed: 2026-03-21. Summaries processed: 66 (2026-03-10 through 2026-03-21).

## Open Issues

| File | Issue | Reports | Sessions | Status |
|------|-------|---------|----------|--------|
| `.claude/rules/test-conventions.md` | Missing guidance: "do not read analyzer source when writing new test cases for existing rules" | 1 | `2026-03-11_06-09-00_main-session` | deferred (needs 2+ reports) |

## Deferred Observations

These were observed but do not warrant action:

- **Duplicate session captures**: `2026-03-10_05-09-41` through `2026-03-10_05-28-35` are 5 captures of the same session (`9cbc836a`) at different timestamps. Similarly `2026-03-10_06-16-00` and `2026-03-10_06-16-40` are from the same session. Infrastructure issue, not a doc issue.
- **No skill-usage.log exists**: Phase 2 (skill usage review) skipped entirely.
- **Zero build/test failures**: All 66 summaries report no errors. The workflow is functioning as designed.
- **tmp/ snippet files**: Test-researcher agents create temporary snippet files under `tmp/` for AST exploration. Observed in 6+ sessions. Working pattern, no action needed.

## Resolved Issues

| File | Issue | Fixed On | Reports Before Fix |
|------|-------|----------|--------------------|
| `.claude/skills/new-rule/SKILL.md` | Line 38 referenced stale `TestCaseLoader.LoadCase`/`[InlineData]` pattern; updated to `AnalyzerTestBase` | 2026-03-13 | 1 (staleness scan) |
| 6 agent files | Added sherlock MCP constraint, `/tmp` constraint, and lead delegation guidance | 2026-03-21 | user report (direct feedback) |
