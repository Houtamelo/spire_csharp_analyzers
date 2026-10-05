---
name: test
description: Build and run analyzer tests, optionally filtered to a specific SPIRE rule. Use when asked to test, verify, or check analyzer behavior.
---

# Build & Run Tests

Use arguments supplied with the skill invocation.

1. If no rule ID is supplied, run all tests:
   Use the `dev-tools` MCP `dotnet_test` tool for the solution, or run `dotnet test` if the MCP tool is unavailable.

2. If a rule ID is given (e.g., `{RuleId}`), run only that rule's tests:
   Use the `dev-tools` MCP `dotnet_test` tool with filter `FullyQualifiedName~<RuleId>`, or run `dotnet test --filter "FullyQualifiedName~<RuleId>"` if the MCP tool is unavailable.

3. Report results concisely — pass/fail counts and any failure details.

## Constraints

- Do NOT modify test code to make tests pass
- Do NOT skip, ignore, or suppress failing tests
- Do NOT modify analyzer code — report failures as-is
- If the build fails, report the build error and stop — do not attempt fixes
