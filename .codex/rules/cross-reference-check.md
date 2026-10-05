# Cross-reference check

When changing Codex instructions, skills, agents, hooks, or project docs, check the relevant related files:

- Test case format: `docs/test-case-format.md`, `.codex/rules/test-conventions.md`, `.codex/agents/test_case_writer.toml`, `.agents/skills/write-test-cases/SKILL.md`, `tests/Houtamelo.Spire.Analyzers.Tests/AnalyzerTestBase.cs`.
- Analyzer conventions and new rules: `.codex/rules/analyzer-conventions.md`, `AGENTS.md`, `.agents/skills/new-rule/`, `.agents/skills/verify-rule/`, `.codex/agents/analyzer_implementer.toml`, `.codex/agents/code_reviewer.toml`, `src/Houtamelo.Spire.Analyzers/Descriptors.cs`.
- Source generators and code fixes: `.agents/skills/new-emitter/`, `.agents/skills/new-coupled-analyzer/`, `.agents/skills/new-codefix/`, matching `.codex/agents/` roles, and the corresponding test projects.
- Documentation style: `docs/style-guide.md`, `.codex/rules/documentation-conventions.md`, documentation skills, and rule doc templates.
- Agent infrastructure: `AGENTS.md`, `.agents/skills/`, `.codex/agents/`, `.codex/rules/`, `.codex/config.toml`, `.codex/hooks.json`, `.codex/hooks/`, and `.codex/HISTORY.md`.
- Session feedback: `.agents/skills/reflect/`, `.agents/skills/maintain-docs/`, `.codex/hooks/`, `feedback/`, and `session-reviews/`.

Historical plans under `plans/` and `docs/superpowers/plans/` document past decisions. Do not rewrite them solely to update agent-tool references.
