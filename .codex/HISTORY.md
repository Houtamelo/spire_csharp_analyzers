# Agent Infrastructure Change History

Append-only log of changes to agent infrastructure files.
Newest entries first. Maintained by the history-keeper agent.

---

## [2026-10-25] `manual` — `.codex/config.toml`

**What**: Added `DOTNET_ROOT=/home/houtamelo/.dotnet` to the Sherlock MCP server environment.
**Why**: Manual edit — no session summary available.
**Commit**: e51f2b5

## [2026-10-25] `manual` — `.codex/hooks.json`

**What**: Reduced the SessionEnd lifecycle hook timeout from 30 seconds to 3 seconds.
**Why**: Manual edit — no session summary available.
**Commit**: e51f2b5

## [2026-10-25] `manual` — `AGENTS.md`

**What**: Updated the documented union API from `Layout`/`GenerateDeconstruct` to `ReferenceStorage` and described the variant-payload emitter structure.
**Why**: Manual edit — no session summary available.
**Commit**: e51f2b5

## [2026-10-07] `Houtamelo` — `.codex/hooks/lifecycle.sh`

**What**: Replaced the Python lifecycle implementation with a Bash wrapper that builds `tools/Spire.Tool` in Release, runs its `hook` command, and sends build output to stderr so stdout remains reserved for event-response JSON.
**Why**: The tooling refactor consolidated Codex lifecycle handling in the C# CLI while preserving machine-readable hook behavior; validation reported 2,027 .NET tests, 2,036 Unity Mono tests, and passing graphics, behavioral, feasibility, asmdef/InspectCode, and Rider checks.
**Commit**: 5a213b3
**Session**: feedback/summaries/01a10e14-cba6-7613-9a3c-50f7cc938dd2.md

## [2026-10-07] `Houtamelo` — `.codex/hooks.json`

**What**: Pointed the SessionStart, Stop, and SessionEnd hook commands at `lifecycle.sh`, using 30-second timeouts for SessionStart/SessionEnd and retaining 600 seconds for Stop.
**Why**: The migration consolidated tooling in one C# CLI and required Codex hook dispatch to use the new wrapper entry point.
**Commit**: 5a213b3
**Session**: feedback/summaries/01a10e14-cba6-7613-9a3c-50f7cc938dd2.md

## [2026-10-07] `Houtamelo` — `.codex/hooks/lifecycle.py`

**What**: Removed the Python lifecycle hook now superseded by `lifecycle.sh` and Spire.Tool’s `hook` subcommand.
**Why**: The migration eliminated the Python runtime dependency while preserving lifecycle events through the C# CLI.
**Commit**: 5a213b3
**Session**: feedback/summaries/01a10e14-cba6-7613-9a3c-50f7cc938dd2.md
