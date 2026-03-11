# Plan 013: Documentation Deduplication

**Status**: Ready for review
**Goal**: Eliminate duplicated information across project docs to prevent inconsistencies.

---

## Findings

### Case 1: Analyzer structure conventions repeated across CLAUDE.md, rules file, code-reviewer agent, and analyzer-implementer agent

**What**: The checklist of required analyzer conventions (inherit DiagnosticAnalyzer, use EnableConcurrentExecution, ConfigureGeneratedCodeAnalysis, CompilationStartAction, SymbolEqualityComparer.Default, IOperation API, descriptors from Descriptors.cs, netstandard2.0 target, no code fixes, PrivateAssets="all") is restated in four separate locations.

**Where**:
- `CLAUDE.md` — "Hard Constraints" section (lines 42-48) and "Analyzer Conventions > Analyzer structure" section (lines 64-71)
- `.claude/rules/analyzer-conventions.md` — full file (lines 8-20), nearly identical content to CLAUDE.md
- `.claude/agents/code-reviewer.md` — "Convention compliance" section (lines 33-41), restates each convention as a checklist
- `.claude/agents/analyzer-implementer.md` — "Technical constraints" section (lines 43-49), restates the same conventions again

**Suggestion**: Make `.claude/rules/analyzer-conventions.md` the single canonical source. In CLAUDE.md, keep only a pointer: "See `.claude/rules/analyzer-conventions.md` for conventions." In the code-reviewer and analyzer-implementer agents, reference the rules file ("Follow conventions from `.claude/rules/analyzer-conventions.md`") instead of restating each item. The AnalyzerTemplate.cs already demonstrates the conventions structurally, so it doesn't need changes.

---

### Case 2: Test case file format repeated across CLAUDE.md, test-conventions rule, and test-case-format doc

**What**: The test case header format (`//@ should_fail`/`//@ should_pass`, line 2 description), error marker table (`//~ ERROR`, `//~^ ERROR`), and file naming conventions are specified in three places with slightly different levels of detail.

**Where**:
- `CLAUDE.md` — "Test Conventions > Test case file format" section (lines 97-118) and "Error markers" table (lines 113-118)
- `.claude/rules/test-conventions.md` — "File format" section (lines 28-43) and "Error markers" table (lines 45-51), includes extra "validation rules" (lines 54-59) and "All comments are stripped" note (line 52) not in CLAUDE.md
- `.claude/docs/test-case-format.md` — full file, the most complete version with examples, headers, markers, and file naming

**Suggestion**: Make `.claude/docs/test-case-format.md` the single canonical reference for test case file format. In CLAUDE.md, keep only a brief mention ("Test cases use the format described in `.claude/docs/test-case-format.md`"). In `.claude/rules/test-conventions.md`, remove the duplicated format details and reference the doc file instead — the rule already references this doc indirectly through the test-case-writer agent. The test-case-writer and edge-case-finder agents already correctly point to `.claude/docs/test-case-format.md` with "Read `.claude/docs/test-case-format.md` for the full format reference."

---

### Case 3: Test runner structure and minimum case counts repeated across CLAUDE.md and test-conventions rule

**What**: The test runner pattern (`AnalyzerTestBase<TAnalyzer>` inheritance, `RuleId` override), minimum case count (3 detection, 3 false-positive), and "test against both struct and record struct" instruction appear in both files.

**Where**:
- `CLAUDE.md` — "Test Conventions" section: test runner example (lines 123-127), minimum counts (line 129), struct/record struct (line 130)
- `.claude/rules/test-conventions.md` — "Test Runner Structure" section (lines 63-69), minimum counts (line 72), "Edge Cases" section (lines 74-77)

**Suggestion**: Keep test runner structure and minimums in `.claude/rules/test-conventions.md` (canonical source for test conventions). In CLAUDE.md, keep only a brief summary pointing to the rules file. The verify-rule skill (lines 32-33) also references these minimums — that's acceptable since it's an enforcement check, not a definition.

---

### Case 4: File naming conventions repeated across CLAUDE.md, analyzer-conventions rule, and test-case-format doc

**What**: File naming patterns for analyzers, test folders, test runners, test cases, shared preambles, docs, and descriptors.

**Where**:
- `CLAUDE.md` — "File naming" table (lines 52-62), covers all file types
- `.claude/rules/analyzer-conventions.md` — lines 11-12, covers analyzer file naming and descriptor field naming
- `.claude/docs/test-case-format.md` — "File naming" section (lines 48-52), covers test case file naming
- `.claude/agents/test-researcher.md` — "File naming conventions" section (lines 76-78), same detection/false-positive naming pattern

**Suggestions (written by user)**: 
- Make test-case-format a project-wide enforcement, move ".claude/docs/test-case-format.md" -> "docs/test-case-format.md"
- Update `.claude/agents/test-researcher.md`: tell it to reference "docs/test-case-format.md".

---

### Case 5: "Adding a New Rule" workflow repeated across CLAUDE.md and PlanTemplate.md

**What**: The TDD implementation order (add descriptor, create attribute, scaffold tests, spawn test-case-writer, run tests, spawn implementer, run tests, create docs) is described in both places.

**Where**:
- `CLAUDE.md` — "Adding a New Rule" section (lines 132-148), 15-step workflow
- `.claude/skills/new-rule/templates/PlanTemplate.md` — "Implementation Order" section (lines 71-80), 8-step condensed version

**Suggestion (written by user)**: The PlanTemplate version is a condensed per-rule checklist, which serves a different purpose from the master workflow in CLAUDE.md. 

PlanTemplate.md should be the source of truth, have CLAUDE.md reference it. 
Also remove the "Implementation Order" section from the template entirely, since the lead follows CLAUDE.md's workflow regardless.

---

### Case 6: "When stuck" / "message the lead" constraint repeated across all agents

**What**: The instruction "If you're blocked, unsure, or can't find what you need -- message the lead explaining the problem. Do not improvise workarounds or run arbitrary commands to unblock yourself" appears verbatim (or near-verbatim) in every agent file.

**Where**:
- `CLAUDE.md` — "When You're Stuck" section (lines 155-157)
- `.claude/agents/history-keeper.md` — line 65
- `.claude/agents/code-reviewer.md` — line 89
- `.claude/agents/test-case-writer.md` — line 48
- `.claude/agents/edge-case-finder.md` — line 47
- `.claude/agents/analyzer-implementer.md` — line 54
- `.claude/agents/test-researcher.md` — line 96
- `.claude/agents/doc-maintainer.md` — line 26
- `.claude/agents/researcher.md` — line 29

**Suggestion**: Keep it only in CLAUDE.md, but put more emphasis on its importance.

---

### Case 7: "Do NOT install external tools, run Python scripts, or decompile DLLs" constraint repeated across agents

**What**: The prohibition on external tools, Python scripts, decompiling DLLs, searching the NuGet cache, and using /tmp is restated in multiple agent files.

**Where**:
- `.claude/agents/analyzer-implementer.md` — lines 50-53
- `.claude/agents/test-case-writer.md` — lines 44-47
- `.claude/agents/test-researcher.md` — lines 92-95

**Suggestion**: Same rationale as Case 6 — agents are self-contained and may not read shared docs. This duplication is acceptable by design, but the wording should be standardized. Currently the phrasing is identical across all three, which is good. No action needed unless the constraint changes — then all three must be updated simultaneously (use `/update-docs` skill).

---

### Case 8: Roslyn API resources block repeated across analyzer-implementer and test-researcher agents

**What**: The block listing MCP tools (microsoft-learn, sherlock) and the SyntaxTreeViewer tool appears in two agent files with nearly identical content.

**Where**:
- `.claude/agents/analyzer-implementer.md` — "Roslyn API resources" section (lines 34-39)
- `.claude/agents/test-researcher.md` — "Roslyn API resources" section (lines 82-84)

**Suggestion**: Like Cases 6-7, agent self-containment justifies this. However, the analyzer-implementer version is more detailed (includes sherlock usage examples and docs/roslyn-api/reference mention), while the test-researcher version is sparser. Standardize the content to be identical in both, or create a shared snippet referenced by both (though the agent format doesn't support includes). Keep the duplication but ensure consistency.

---

### Case 9: Descriptor pattern shown in CLAUDE.md and AnalyzerTemplate.cs

**What**: The pattern for defining a DiagnosticDescriptor (field in Descriptors.cs, specific constructor arguments) is shown in CLAUDE.md and partially embodied in the AnalyzerTemplate.cs.

**Where**:
- `CLAUDE.md` — "Descriptor pattern" section (lines 75-87), shows full constructor call with all named parameters
- `.claude/rules/analyzer-conventions.md` — line 9-10, brief description of descriptor placement and naming
- `.claude/skills/new-rule/templates/AnalyzerTemplate.cs` — line 11, references `Descriptors.{{DESCRIPTOR_FIELD}}`

**Suggestion**: analyzer-conventions should be the source of truth, make other files reference it.

---

### Case 10: TDD ordering described in CLAUDE.md and test-conventions rule

**What**: The instruction that tests are written before the analyzer, and the expected pass/fail behavior before the analyzer exists.

**Where**:
- `CLAUDE.md` — "Test Conventions" first bullet (line 91): "TDD: Tests are written BEFORE the analyzer implementation"
- `.claude/rules/test-conventions.md` — "TDD Ordering" section (lines 8-16), much more detailed (includes expected fail/pass behavior per test type)
- `.claude/skills/write-test-cases/SKILL.md` — line 46: "tests will FAIL at this stage because no analyzer exists yet"
- `.claude/agents/test-case-writer.md` — line 50: "tests will FAIL at this stage because no analyzer exists yet"

**Suggestion**: Make `.claude/rules/test-conventions.md` the canonical TDD ordering reference. In CLAUDE.md, keep only the one-line mention with a pointer. The skill and agent mentions are contextual reminders, which are fine to keep.

---

### Case 11: Project overview line ("Roslyn-based C# analyzer for struct correctness...") repeated

**What**: The one-line project description appears in multiple places.

**Where**:
- `CLAUDE.md` — line 3: "Roslyn-based C# analyzer for struct correctness and performance pitfalls. Replaces ErrorProne.NET.Structs."
- MEMORY.md (system context) — "What This Is" section, same text

**Suggestion**: MEMORY.md is auto-maintained and serves a different purpose (cross-session memory), so this duplication is acceptable. No action needed.

---

### Case 12: Build commands repeated in CLAUDE.md and MEMORY.md

**What**: The `dotnet restore`, `dotnet build`, `dotnet test`, and SyntaxTreeViewer commands.

**Where**:
- `CLAUDE.md` — "Build Commands" section (lines 11-17)
- MEMORY.md — "Build Commands" section at the bottom

**Suggestion**: MEMORY.md is auto-maintained external context. No action needed — but when updating CLAUDE.md build commands, MEMORY.md should also be updated.

---

### Case 13: stackalloc exception documented in CLAUDE.md and analyzer-conventions rule (implied)

**What**: The note that `stackalloc` has no IOperation kind and requires syntax-based detection.

**Where**:
- `CLAUDE.md` — "Hard Constraints" bullet (line 46): explicit exception with syntax types to use
- `.claude/rules/analyzer-conventions.md` — line 16: "Prefer `RegisterOperationAction` (IOperation API) over `RegisterSyntaxNodeAction`" (general rule, no exception mentioned)

**Suggestion**: Make analyzer-conventions the source of truth here, make CLAUDE.md reference it. 

---

### Case 14: verify-rule skill duplicates convention checks from analyzer-conventions rule

**What**: The verify-rule skill (lines 19-34) re-enumerates specific convention checks (DiagnosticAnalyzer attribute, descriptor in Descriptors.cs, test runner inherits AnalyzerTestBase, minimum case counts) that are already defined in `.claude/rules/analyzer-conventions.md` and `.claude/rules/test-conventions.md`.

**Where**:
- `.claude/skills/verify-rule/SKILL.md` — sections 1-4 (lines 18-39)
- `.claude/rules/analyzer-conventions.md` — full file
- `.claude/rules/test-conventions.md` — full file

**Suggestion**: This is acceptable — the verify-rule skill is an enforcement mechanism that must know exactly what to check. It can't just reference "analyzer-conventions.md" at runtime; it needs explicit check instructions. However, when conventions change, the verify-rule skill must be updated too. The cross-references.md already tracks this relationship (line 23). No structural change needed, but add a comment in verify-rule noting that checks should match the rules files.

---

### Case 15: update-history skill duplicates history-keeper agent workflow

**What**: The update-history skill describes a workflow that is nearly identical to the history-keeper agent's workflow.

**Where**:
- `.claude/skills/update-history/SKILL.md` — lines 16-23 (5-step process: read HISTORY.md, find commits, check for new commits, process diffs, report)
- `.claude/agents/history-keeper.md` — lines 25-47 (6-step process, with more detail on entry format and session summary lookup)

**Suggestion**: The skill says `disable-model-invocation: true`, meaning it spawns the history-keeper agent. The skill's workflow description is therefore redundant with the agent's own instructions. Simplify the skill to just "Spawn the history-keeper agent" with minimal context, removing the duplicated workflow steps. The agent already has the full instructions.

---

### Case 16: "Do NOT edit test runner" / "cases are discovered automatically" repeated

**What**: The instruction that adding a test case requires only adding a file (no test runner edits) is stated in multiple places.

**Where**:
- `CLAUDE.md` — line 93: "Adding a test = adding one file -- no test runner edits needed"
- `.claude/rules/test-conventions.md` — line 21: "No `[InlineData]` entries, no test runner edits needed" and line 22: "Cases are discovered at runtime"
- `.claude/docs/test-case-format.md` — line 4: "Cases are discovered automatically at runtime -- no test runner edits needed"
- `.claude/agents/test-case-writer.md` — line 42: "Do NOT edit the test runner -- cases are discovered automatically from files"
- `.claude/agents/edge-case-finder.md` — line 46: "Do NOT modify the test runner"

**Suggestion**: This is a critical operational instruction that agents must know. Keep it in agent files (self-containment). In CLAUDE.md and the rules/docs files, consolidate: make `.claude/rules/test-conventions.md` the canonical source, and have CLAUDE.md and test-case-format.md reference it rather than restating it.

---

### Case 17: Documentation sections required in rule docs listed in two places

**What**: The required sections for rule documentation files (Title, Property table, Description, Examples, When to Suppress).

**Where**:
- `.claude/rules/documentation-conventions.md` — lines 8-14, explicit list of 5 required sections
- `.claude/skills/verify-rule/SKILL.md` — line 39: "Verify it has sections: Description, Examples, When to Suppress"
- `.claude/skills/new-rule/templates/DocTemplate.md` — demonstrates the structure with all 5 sections

**Suggestion**: Make `.claude/rules/documentation-conventions.md` the canonical list. The DocTemplate.md embodies it structurally (correct, no change needed). The verify-rule skill only checks 3 of the 5 sections — update it to check all 5 (Title and Property table too), and reference the documentation-conventions rule as the source of truth.

---

### Case 18: Stale SAS prefix in CLAUDE.md examples

**What**: Several examples in CLAUDE.md still use the old `SAS` prefix instead of `SPIRE`.

**Where**:
- `CLAUDE.md` — line 15: `dotnet test --filter "FullyQualifiedName~SAS001"` (build commands)
- `CLAUDE.md` — lines 57-62: file naming examples use `SAS001` (e.g., `SAS001/SAS001Tests.cs`)
- `CLAUDE.md` — lines 77-86: descriptor pattern example uses `SAS001_ShortName`, `id: "SAS001"`
- `.claude/skills/test/SKILL.md` — line 3: description says "SAS rule"

**Suggestion**: Update all `SAS` references to `SPIRE` to match the actual rule prefix. This is not duplication per se, but inconsistency caused by incomplete updates when the prefix was changed.

---

## Implementation Notes (for post-compaction context)

### User decisions from discussion

1. **Case 4 (user-edited suggestion)**: Move `.claude/docs/test-case-format.md` → `docs/test-case-format.md` to make it project-wide. Update test-researcher to reference the new path.

2. **Case 5 (user-edited suggestion)**: PlanTemplate.md should NOT have an "Implementation Order" section — remove it. CLAUDE.md is the source of truth for the workflow.

3. **Case 6 (user decision)**: Agents DO inherit CLAUDE.md as system context (it's always loaded). The "message the lead" instruction in each agent is truly redundant. **Remove from all agents**, keep only in CLAUDE.md but emphasize its importance.

4. **Case 9 (user-edited suggestion)**: analyzer-conventions.md should be the source of truth for the descriptor pattern. Make other files reference it.

### Cases that need no action
- Case 7: Acceptable duplication, already consistent
- Case 8: Acceptable duplication, standardize content
- Case 11: MEMORY.md is separate context
- Case 12: MEMORY.md is separate context
- Case 14: Enforcement mechanism, acceptable

### What was already done in this session (before plan 013)
- Created `.claude/docs/test-case-format.md` — agents already reference it
- Created `.claude/docs/cross-references.md` — maps all file relationships
- Created `.claude/rules/cross-reference-check.md` — rule to remind checking the map
- Created `.claude/skills/update-docs/SKILL.md` — skill for propagating changes
- Removed `plans/` references from all agent files in `.claude/`
- Renamed `test-writer` → `edge-case-finder`
- Implemented Plan 012 (self-contained test cases, AnalyzerTestBase, 87 tests passing)

### Implementation order for remaining work
1. **Case 18**: Fix stale SAS→SPIRE prefix (quick, no dependencies)
2. **Case 6**: Remove "message the lead" from all agents, emphasize in CLAUDE.md
3. **Case 1**: Consolidate analyzer conventions → rules file as canonical source
4. **Case 9**: Move descriptor pattern to analyzer-conventions.md
5. **Case 13**: Add stackalloc exception to analyzer-conventions.md
6. **Case 2**: Consolidate test case format → reference docs file from CLAUDE.md and rules
7. **Case 3**: Consolidate test runner structure → rules file
8. **Case 10**: Consolidate TDD ordering → rules file
9. **Case 4**: Move test-case-format.md to docs/, update references
10. **Case 16**: Consolidate "no runner edits" to rules file
11. **Case 5**: Remove "Implementation Order" from PlanTemplate.md
12. **Case 15**: Simplify update-history skill
13. **Case 17**: Update verify-rule to check all 5 doc sections

## Summary

| # | Severity | Type | Action |
|---|----------|------|--------|
| 1 | High | True duplication | Consolidate analyzer conventions to rules file; reference from CLAUDE.md and agents |
| 2 | High | True duplication | Consolidate test case format to docs file; reference from CLAUDE.md and rules |
| 3 | Medium | True duplication | Consolidate test runner structure to rules file; brief summary in CLAUDE.md |
| 4 | Medium | Scattered definition | Keep master table in CLAUDE.md; remove partial copies elsewhere |
| 5 | Low | Workflow echo | Add cross-reference note to PlanTemplate; no structural change |
| 6 | None | Intentional redundancy | Standardize wording but keep in each agent |
| 7 | None | Intentional redundancy | Already consistent; no action |
| 8 | Low | Incomplete duplication | Standardize content across both agents |
| 9 | Low | Stale example | Fix SAS->SPIRE in CLAUDE.md descriptor example |
| 10 | Medium | True duplication | Consolidate TDD ordering to rules file |
| 11 | None | Acceptable cross-context | No action |
| 12 | None | Acceptable cross-context | No action |
| 13 | Medium | Missing info | Add stackalloc exception to analyzer-conventions rule |
| 14 | None | Acceptable enforcement | No structural change; cross-ref already tracked |
| 15 | Medium | True duplication | Simplify update-history skill; agent has full instructions |
| 16 | Medium | True duplication | Consolidate "no runner edits" to rules file; agents keep their own copy |
| 17 | Low | Incomplete enforcement | Update verify-rule to check all 5 doc sections |
| 18 | Medium | Stale content | Fix SAS->SPIRE in all examples |
