---
name: update-docs
description: Propagate a change across all related docs, agents, skills, and rules using the cross-reference map. Use when adding/removing MCP tools, skills, or modifying conventions.
---

# Update Docs

Use the change description supplied with the skill invocation.

## Step 1: Read the cross-reference map

Read `.codex/rules/cross-reference-check.md` to understand which files are grouped together.

## Step 2: Identify affected groups

Based on the change description, determine which cross-reference groups are affected.

## Step 3: Read all files in affected groups

Read every file in each affected group. Build a list of edits needed.

## Step 4: Handle specific change types

### MCP tool added

1. Search `.codex/agents/`, `.agents/skills/`, and `.codex/config.toml` for references to the tool.
2. For each agent/skill, read its description and role.
3. If the new MCP tool is relevant to the agent's role, update the relevant usage instructions.
4. If the agent/skill body references related tools or workflows, add a mention of the new tool where appropriate.
5. If relevance is unclear, limit edits to agents whose workflows actually require the tool.

### MCP tool removed

1. Search `.codex/` and `.agents/` for the tool name.
2. Remove stale usage instructions and configuration for the tool.
3. Remove or rewrite any instructions that reference the tool.
4. Identify available replacement tools and report any lost capability.

### Skill added

1. Read the new skill's `SKILL.md` to understand what it does.
2. Read all agent definitions in `.codex/agents/`.
3. For agents whose role overlaps with the skill's purpose, add a reference (e.g., "Use `/skill-name` for X").
4. If the skill relates to a workflow step in `AGENTS.md`, update that step.
5. Add references only where the agent needs the workflow.

### Skill removed

1. Search `.codex/` and `.agents/` and `AGENTS.md` for the skill name.
2. Remove or rewrite all references.
3. If the skill was part of a workflow step in `AGENTS.md`, update or remove that step.

### Convention or format changed

1. Identify the cross-reference group for the convention.
2. Read all files in the group.
3. Update each file to reflect the new convention.
4. Check for inline examples or templates that use the old convention.

### Other changes

1. Use the cross-reference map to find all related files.
2. Read each one and determine if it needs updating.
3. Apply edits, using the repository and task context to resolve routine choices.

## Step 5: Apply edits

For each edit:
- If you're confident the edit is correct, apply it.
- If the relationship is unclear, inspect the relevant workflow and make a bounded edit supported by evidence.

## Step 6: Verify

After all edits, search for any remaining references to old names, removed tools, or stale conventions. Report any you find.

## Constraints

- Always read before editing — never edit a file you haven't read in this session.
- Explain unresolved decisions that cannot be inferred from the repository or task.
- Do not delete files — only edit content within them (or report that a file should be deleted).
- Update the cross-reference map itself if the change adds or removes files from a group.
