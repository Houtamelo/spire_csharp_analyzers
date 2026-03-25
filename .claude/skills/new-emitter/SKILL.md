---
name: new-emitter
description: Scaffold test structure for a new source generator emitter, or verify an existing emitter against its tests. Use when adding or modifying an emitter.
disable-model-invocation: true
user-invocable: true
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: [new|verify] [EmitterName]
hooks:
  Stop:
    - command: "bash .claude/hooks/log-skill-usage.sh new-emitter"
---

# Scaffold or Verify a Source Generator Emitter

Arguments: `$ARGUMENTS`
Example: `/new-emitter new InlineArray`
Example: `/new-emitter verify Additive`

## Validation

1. Parse `$1` as the mode (`new` or `verify`) and `$2` as the emitter name — reject if missing
2. If mode is `new`:
   - Verify no existing emitter file matches `src/Spire.Analyzers/SourceGenerators/Emit/{EmitterName}Emitter.cs` — abort if duplicate
3. If mode is `verify`:
   - Verify the emitter file exists at `src/Spire.Analyzers/SourceGenerators/Emit/{EmitterName}Emitter.cs` — abort if missing

## Mode: `new` — Scaffold

4. Create emitter stub: `src/Spire.Analyzers/SourceGenerators/Emit/{EmitterName}Emitter.cs`
   - Include `using Spire.SourceGenerators.Model;` for the `UnionDeclaration` type
   - Minimal class with `internal static class {EmitterName}Emitter` and a `public static string Emit(UnionDeclaration union)` method returning `string.Empty`
5. Create snapshot test category folder: `tests/Spire.SourceGenerators.Tests/cases/discriminated_union/{strategy_snake_case}/`
6. Create behavioral test stubs (empty files with correct namespace/class):
   - `tests/Spire.BehavioralTests/Types/{EmitterName}Unions.cs` — placeholder with `using Spire;`
   - `tests/Spire.BehavioralTests/Tests/{EmitterName}Tests.cs` — placeholder test class with `using Xunit;`
7. Run `dotnet build` — must succeed
8. Report all created files

## Mode: `verify` — Run Tests

4. Run `dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Snapshot"` — report results
5. Run `dotnet test tests/Spire.BehavioralTests/` — report results
6. Report pass/fail summary

## What this skill does NOT do

- **Does NOT write snapshot test cases** (input.cs/output.cs) — that is the snapshot writer's job
- **Does NOT write behavioral tests** — that is the behavioral writer's job
- **Does NOT implement the emitter** — that is the implementer's job
- Only scaffolds the structure so the lead can orchestrate the pipeline

## Constraints

- Do NOT implement emitter logic — the emitter stub returns `string.Empty`
- Do NOT modify existing files (only create new ones in `new` mode)
- Do NOT proceed if validation fails — report the error and stop
