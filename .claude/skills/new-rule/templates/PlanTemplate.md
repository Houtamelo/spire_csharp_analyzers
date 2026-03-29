# Plan XXX: {{RULE_ID}} — {{RULE_TITLE}}

**Status**: Ready for implementation
**Goal**: {{ONE_SENTENCE_GOAL}}

## Overview

**ID**: {{RULE_ID}}
**Title**: {{RULE_TITLE}}
**Category**: Correctness | Performance
**Default severity**: Error | Warning | Info
**Message format**: `{{MESSAGE_WITH_PLACEHOLDERS}}`
**Enabled by default**: Yes

### What this rule does

{{DESCRIPTION — what the rule detects and why it matters. Include C# semantics context.}}

## What {{RULE_ID}} Detects

### Flagged

| Code | Why |
|------|-----|
| `{{example code}}` | {{reason}} |
| `{{example code}}` | {{reason}} |

### NOT flagged

| Code | Why |
|------|-----|
| `{{example code}}` | {{reason}} |
| `{{example code}}` | {{reason}} |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `{{example code}}` | {{reason}} |

## Implementation Notes

### Attribute/marker type (if needed)

{{Describe any new attribute or type needed, or "None" if the rule works without one.}}

### Detection strategy

- **IOperation kind(s)**: {{which OperationKind(s) to register for}}
- **Key checks**: {{what conditions determine flagging}}
- **Use `CompilationStartAction`**: {{yes/no, and what to resolve}}

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Houtamelo.Spire.Analyzers/Rules/{{RULE_ID}}{{ShortName}}Analyzer.cs` | The analyzer | Implementer |
| `tests/Houtamelo.Spire.Analyzers.Tests/{{RULE_ID}}/{{RULE_ID}}Tests.cs` | Test runner | Lead |
| `tests/Houtamelo.Spire.Analyzers.Tests/{{RULE_ID}}/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Houtamelo.Spire.Analyzers.Tests/{{RULE_ID}}/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/{{RULE_ID}}.md` | Rule documentation | Lead |

