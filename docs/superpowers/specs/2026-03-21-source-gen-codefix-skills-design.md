# Source Generator, Coupled Analyzer, and Code Fix Skills

**Date**: 2026-03-21
**Status**: Approved

## Problem

The project has structured skills and agents for standalone analyzer rules (`/new-rule` pipeline) but no equivalent workflows for:
1. Source generator emitters (`src/Spire.SourceGenerators/Emit/`)
2. Generator-coupled analyzers (`src/Spire.SourceGenerators/Analyzers/`)
3. Code fix providers (`src/Spire.CodeFixes/`)

Each has distinct test formats and project scopes that the existing pipeline doesn't cover.

## Design

### Three New Skills

#### `/new-emitter [EmitterName]`

Scaffolds a new source generator emitter with test structure.

**Scaffolds:**
- Emitter stub in `src/Spire.SourceGenerators/Emit/{EmitterName}Emitter.cs`
- Snapshot test folder: `tests/Spire.SourceGenerators.Tests/cases/{emitter_category}/`
- Behavioral test stubs: `tests/Spire.BehavioralTests/Types/{Strategy}Unions.cs`, `tests/Spire.BehavioralTests/Tests/{Strategy}Tests.cs`

**Arguments:** `[EmitterName]` (e.g., `InlineArrayEmitter`)

**Two modes** (passed as first argument):
- `/new-emitter new [EmitterName]` — full scaffold for a new emitter
- `/new-emitter verify [EmitterName]` — run existing tests against a modified emitter

#### `/new-coupled-analyzer [RuleId] [RuleTitle]`

Scaffolds a generator-coupled analyzer with descriptor and test structure.

**Scaffolds:**
- Descriptor in `src/Spire.SourceGenerators/AnalyzerDescriptors.cs`
- Analyzer stub in `src/Spire.SourceGenerators/Analyzers/`
- Test folder under `tests/Spire.SourceGenerators.Tests/`
- Shared preamble and test runner inheriting `GeneratorAnalyzerTestBase`

**Arguments:** `[RuleId] [RuleTitle]` (e.g., `SPIRE016 "Variant field type mismatch"`)

**Validates:** Rule ID matches SPIRE prefix, no duplicate descriptor exists.

#### `/new-codefix [RuleId]`

Scaffolds a code fix provider for an existing diagnostic.

**Scaffolds:**
- Code fix provider stub in `src/Spire.CodeFixes/{RuleId}{ShortName}CodeFix.cs`
- Test folder: `tests/Spire.SourceGenerators.Tests/CodeFix/cases/` (flat structure — each case is a subdirectory like `{FixName}_{Scenario}/`)
- Template before.cs/after.cs pair
- Test runner inheriting `CodeFixTestBase`

**Arguments:** `[RuleId]` (e.g., `SPIRE009`)

**Validates:** Diagnostic already exists in `Descriptors.cs` or `AnalyzerDescriptors.cs`.

### Ten New Agents

#### Emitter Workflow (4 agents)

**`emitter-test-researcher`** (Sonnet, 30 turns)
- Reads emitter description from lead
- Studies existing emitters and test patterns
- Produces coverage matrix with three sections: snapshot cases, behavioral type definitions, behavioral test cases
- Output: `tests/Spire.SourceGenerators.Tests/cases/{emitter_category}/coverage-matrix.md`
- Tools: Read, Write, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn
- Scope: only writes to test directories

**`emitter-snapshot-writer`** (Sonnet, 40 turns)
- Writes input.cs/output.cs snapshot pairs from coverage matrix
- Scope: only writes to `tests/Spire.SourceGenerators.Tests/cases/`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`emitter-behavioral-writer`** (Sonnet, 40 turns)
- Writes union type definitions in `tests/Spire.BehavioralTests/Types/` and corresponding `[Fact]` test methods in `tests/Spire.BehavioralTests/Tests/`
- Types and tests are tightly coupled — single agent handles both
- Scope: only writes to `tests/Spire.BehavioralTests/`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`emitter-implementer`** (Sonnet, 30 turns)
- Implements or modifies the emitter to make all snapshot and behavioral tests pass
- Reads snapshot expected outputs and behavioral test assertions as the contract
- Scope: writes to `src/Spire.SourceGenerators/Emit/`, `src/Spire.SourceGenerators/Model/`, `src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs` (to register new emitter), and `src/Spire.SourceGenerators/Parsing/` (if new strategy enum or attribute changes needed)
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

#### Coupled Analyzer Workflow (3 agents)

**`coupled-analyzer-test-researcher`** (Sonnet, 30 turns)
- Same role as standalone `test-researcher` but for generator-coupled context
- Understands that generator runs first, then analyzer on combined output
- Produces coverage matrix using `GeneratorAnalyzerTestBase` format (should_fail/should_pass files)
- Output: `tests/Spire.SourceGenerators.Tests/{Category}/coverage-matrix.md`
- Tools: Read, Write, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`coupled-analyzer-test-case-writer`** (Sonnet, 40 turns)
- Writes should_fail/should_pass test case files for generator-coupled analyzers
- Cases include `[DiscriminatedUnion]` declarations + code that exercises the analyzer
- Scope: only writes to `tests/Spire.SourceGenerators.Tests/{Category}/cases/`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`coupled-analyzer-implementer`** (Sonnet, 30 turns)
- Implements analyzer in `src/Spire.SourceGenerators/Analyzers/`
- Reads test cases and descriptor as the contract
- Scope: only writes to `src/Spire.SourceGenerators/Analyzers/` and `src/Spire.SourceGenerators/AnalyzerDescriptors.cs`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

#### Code Fix Workflow (3 agents)

**`codefix-test-researcher`** (Sonnet, 30 turns)
- Studies the target diagnostic and existing code fix patterns
- Produces coverage matrix of before/after test case pairs
- Output: `tests/Spire.SourceGenerators.Tests/CodeFix/coverage-matrix-{RuleId}.md`
- Tools: Read, Write, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`codefix-test-case-writer`** (Sonnet, 40 turns)
- Writes before.cs/after.cs pairs from coverage matrix
- `before.cs` contains code that triggers the diagnostic
- `after.cs` contains expected code after fix is applied
- Scope: only writes to `tests/Spire.SourceGenerators.Tests/CodeFix/cases/`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

**`codefix-implementer`** (Sonnet, 30 turns)
- Implements code fix provider in `src/Spire.CodeFixes/`
- Reads before/after test pairs as the contract
- Scope: only writes to `src/Spire.CodeFixes/`
- Tools: Read, Write, Edit, Glob, Grep, Bash, mcp__sherlock, mcp__microsoft-learn

### Workflow Orchestration

#### Emitter Pipeline

1. `/new-emitter` scaffolds emitter stub + test folders
2. Lead designs emitter behavior with user
3. Lead spawns `emitter-test-researcher` → combined coverage matrix
4. Lead reviews matrix
5. Lead spawns `emitter-snapshot-writer` agents (one per category) — writes input.cs/output.cs
6. Lead spawns `emitter-behavioral-writer` — writes Types/ + Tests/ files
7. Lead runs `dotnet test` — tests fail (no emitter logic)
8. Lead spawns `emitter-implementer` → implements emitter
9. Lead runs `dotnet test` — all pass
10. Lead spawns existing `code-reviewer` agent for audit

#### Coupled Analyzer Pipeline

1. `/new-coupled-analyzer` scaffolds descriptor, test folder, runner
2. Lead designs rule with user
3. Lead spawns `coupled-analyzer-test-researcher` → coverage matrix
4. Lead reviews matrix
5. Lead spawns `coupled-analyzer-test-case-writer` agents (one per category)
6. Lead runs `dotnet test` — detection tests fail, false-positive pass
7. Lead spawns `coupled-analyzer-implementer` → implements analyzer
8. Lead runs `dotnet test` — all pass
9. Lead spawns existing `code-reviewer` agent for audit

#### Code Fix Pipeline

1. `/new-codefix` validates diagnostic exists, scaffolds code fix stub + test folder
2. Lead designs fix behavior with user
3. Lead spawns `codefix-test-researcher` → before/after coverage matrix
4. Lead reviews matrix
5. Lead spawns `codefix-test-case-writer` agents (one per category)
6. Lead runs `dotnet test` — code fix tests fail
7. Lead spawns `codefix-implementer` → implements code fix
8. Lead runs `dotnet test` — all pass
9. Lead spawns existing `code-reviewer` agent for audit

#### Combined Session (diagnostic + code fix)

1. Run `/new-rule` or `/new-coupled-analyzer` pipeline first — get diagnostic implemented and passing
2. Run `/new-codefix` pipeline second — build fix for the now-existing diagnostic
3. Both pipelines run sequentially, never interleaved

### Scope Constraints

Every agent gets these standard constraints:
- Do NOT use `/tmp` or any absolute temp path — use the project-local `tmp/` folder (gitignored) for any temporary files
- Use sherlock via MCP tools (`mcp__sherlock__*`), never CLI
- Use `Write` tool, not `cat` or heredocs in Bash
- Do NOT search or grep the NuGet cache (`~/.nuget/packages/`)
- Do NOT install external tools, run Python scripts, or decompile DLLs

Per-agent write scope:

| Agent | Can write to | Cannot touch |
|-------|-------------|--------------|
| `emitter-test-researcher` | `tests/Spire.SourceGenerators.Tests/cases/` (coverage matrix only) | Source code, BehavioralTests |
| `emitter-snapshot-writer` | `tests/Spire.SourceGenerators.Tests/cases/` | Source code, BehavioralTests |
| `emitter-behavioral-writer` | `tests/Spire.BehavioralTests/Types/`, `tests/Spire.BehavioralTests/Tests/` | Source code, snapshot tests |
| `emitter-implementer` | `src/Spire.SourceGenerators/Emit/`, `src/Spire.SourceGenerators/Model/`, `src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs`, `src/Spire.SourceGenerators/Parsing/` | Tests, Analyzers, CodeFixes |
| `coupled-analyzer-test-researcher` | `tests/Spire.SourceGenerators.Tests/{Category}/` (coverage matrix only) | Source code, other test folders |
| `coupled-analyzer-test-case-writer` | `tests/Spire.SourceGenerators.Tests/{Category}/cases/` | Source code, other test folders |
| `coupled-analyzer-implementer` | `src/Spire.SourceGenerators/Analyzers/`, `src/Spire.SourceGenerators/AnalyzerDescriptors.cs` | Tests, Emit, CodeFixes |
| `codefix-test-researcher` | `tests/Spire.SourceGenerators.Tests/CodeFix/` (coverage matrix only) | Source code, other test folders |
| `codefix-test-case-writer` | `tests/Spire.SourceGenerators.Tests/CodeFix/cases/` | Source code, other test folders |
| `codefix-implementer` | `src/Spire.CodeFixes/` | Tests, Analyzers, Emitters |

### Test Formats

**Snapshot tests** (emitter):
```
tests/Spire.SourceGenerators.Tests/cases/discriminated_union/{strategy}/{CaseName}/
  input.cs      ← [DiscriminatedUnion] declaration
  output.cs     ← expected generated source (hand-written)
```
Example: `cases/discriminated_union/struct_additive/BasicStruct/input.cs`. Discovered by `SnapshotCaseDiscoveryAttribute`. Each leaf directory = one test case.

**Behavioral tests** (emitter):
```
tests/Spire.BehavioralTests/Types/{Strategy}Unions.cs    ← union declarations
tests/Spire.BehavioralTests/Tests/{Strategy}Tests.cs     ← [Fact] runtime assertions
```
Plain xUnit `[Fact]` methods. Generator runs at build time via `OutputItemType="Analyzer"` in .csproj.

**Coupled analyzer tests**:
```
tests/Spire.SourceGenerators.Tests/{Category}/cases/
  {Name}.cs    ← first line: //@ should_fail or //@ should_pass
```
File naming is descriptive (e.g., `Struct_MissingVariant.cs`, `Pass_ClassFullCoverage.cs`) — the `//@ should_fail` or `//@ should_pass` header on line 1 determines the test type, not the filename prefix. Discovered by `GeneratorAnalyzerCaseDiscoveryAttribute`. Generator runs first, then analyzer on combined output.

**Code fix tests**:
```
tests/Spire.SourceGenerators.Tests/CodeFix/cases/{FixName}_{Scenario}/
  before.cs     ← code with diagnostic
  after.cs      ← code after fix applied
```
Example: `CodeFix/cases/AddMissingArms_Struct/before.cs`. Flat structure — no RuleId grouping. Discovered by `CodeFixCaseDiscoveryAttribute`. Pipeline: generator → analyzer → apply fix → compare with after.cs.

### New Files

```
.claude/skills/
  new-emitter/SKILL.md
  new-coupled-analyzer/SKILL.md
  new-codefix/SKILL.md

.claude/agents/
  emitter-test-researcher.md
  emitter-snapshot-writer.md
  emitter-behavioral-writer.md
  emitter-implementer.md
  coupled-analyzer-test-researcher.md
  coupled-analyzer-test-case-writer.md
  coupled-analyzer-implementer.md
  codefix-test-researcher.md
  codefix-test-case-writer.md
  codefix-implementer.md
```
