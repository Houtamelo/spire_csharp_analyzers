---
name: syntax-tree
description: Print the Roslyn syntax tree for C# code. Use to discover which SyntaxNode types and properties to match in an analyzer.
---

# Print Syntax Tree

Use arguments supplied with the skill invocation.

1. Call the `parse_syntax_tree` MCP tool with the invocation arguments as the `code` parameter.
2. Return the formatted AST output.

## Constraints

- Do NOT interpret, analyze, or summarize the output — return the raw AST as-is
- Do NOT modify any project files
