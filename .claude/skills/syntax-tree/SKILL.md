---
name: syntax-tree
description: Print the Roslyn syntax tree for C# code. Use to discover which SyntaxNode types and properties to match in an analyzer.
user-invocable: true
allowed-tools: mcp__syntax-tree
argument-hint: <C# code>
hooks:
  Stop:
    - command: "bash .claude/hooks/log-skill-usage.sh syntax-tree"
---

# Print Syntax Tree

Arguments: `$ARGUMENTS`

1. Call the `parse_syntax_tree` MCP tool with `$ARGUMENTS` as the `code` parameter.
2. Return the formatted AST output.

## Constraints

- Do NOT interpret, analyze, or summarize the output — return the raw AST as-is
- Do NOT modify any project files
