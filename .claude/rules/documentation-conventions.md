---
paths:
  - "docs/rules/**/*.md"
---

# Rule Documentation Conventions

- One `.md` file per rule, named after the rule ID (e.g., `{RuleId}.md`)
- Required sections:
  1. **Title** — `# {RuleId}: Short description`
  2. **Property table** — ID, Category, Severity, Enabled
  3. **Description** — What the rule detects and why it matters
  4. **Examples** — Violating code and compliant code (with code blocks)
  5. **When to Suppress** — Valid reasons and `#pragma` example
