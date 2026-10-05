#!/usr/bin/env python3
"""Codex lifecycle hooks for local reflection, feedback, and C# test verification."""

from datetime import datetime, timezone
from pathlib import Path
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EVENT = json.load(sys.stdin)
KIND = EVENT.get("hook_event_name")
SESSION = re.sub(r"[^a-zA-Z0-9_-]", "_", str(EVENT.get("session_id") or "unknown"))

def write_json(value):
    print(json.dumps(value))

if KIND == "SessionStart":
    review_dir = ROOT / "session-reviews"
    review_dir.mkdir(exist_ok=True)
    review_file = review_dir / f"{SESSION}.md"
    if not review_file.exists():
        review_file.write_text(
            f"# Session Review\n\n- **Session ID**: {SESSION}\n"
            f"- **Transcript**: {EVENT.get('transcript_path') or 'unavailable'}\n"
            f"- **Started at**: {datetime.now(timezone.utc).isoformat()}\n"
        )
    write_json({"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": f"Session reflection file: {review_file}"}})

elif KIND == "Stop":
    summary_dir = ROOT / "feedback" / "summaries"
    summary_dir.mkdir(parents=True, exist_ok=True)
    message = EVENT.get("last_assistant_message") or "_No final assistant message available._"
    (summary_dir / f"{SESSION}.md").write_text(
        f"# Codex Session Summary\n\n**Session ID**: {SESSION}\n"
        f"**Updated at**: {datetime.now(timezone.utc).isoformat()}\n\n"
        f"## Last assistant message\n\n{message}\n"
    )
    # The old TaskCompleted gate ran these two projects individually. Codex
    # does not have TaskCompleted, so check source-changing turns at Stop.
    status = subprocess.run(
        ["git", "status", "--porcelain", "--untracked-files=all"],
        cwd=ROOT, text=True, capture_output=True, timeout=10, check=False,
    )
    changed_files = [line[3:] for line in status.stdout.splitlines()]
    changed_cs_files = [
        name for name in changed_files
        if (name.startswith("src/") or name.startswith("tests/") or name.startswith("tools/"))
        and name.endswith(".cs") and (ROOT / name).is_file()
    ]
    if status.returncode != 0:
        write_json({"decision": "block", "reason": f"Could not inspect changed files: {status.stderr[-600:]}"})
        sys.exit(0)
    if changed_cs_files:
        test_env = os.environ.copy()
        test_env.setdefault("CI", "true")
        test_env.setdefault("NUGET_HTTP_CACHE_PATH", str(Path(tempfile.gettempdir()) / "spire-nuget-http-cache"))
        try:
            formatted = subprocess.run(
                ["dotnet", "format", "Spire.Analyzers.slnx", "--include", *changed_cs_files,
                 "--no-restore", "--verbosity", "quiet"],
                cwd=ROOT, text=True, capture_output=True, timeout=180,
                check=False, env=test_env,
            )
        except (OSError, subprocess.TimeoutExpired) as exc:
            write_json({"decision": "block", "reason": f"Could not format changed C# files: {exc}"})
            sys.exit(0)
        if formatted.returncode != 0:
            write_json({"decision": "block", "reason": f"C# formatting failed: {(formatted.stdout + formatted.stderr)[-1200:]}"})
            sys.exit(0)
        for project in (
            "tests/Houtamelo.Spire.SourceGenerators.Tests/",
            "tests/Houtamelo.Spire.Analyzers.Tests/",
            "tests/DevTools.Tests/",
        ):
            try:
                test = subprocess.run(
                    ["dotnet", "test", project, "--nologo"], cwd=ROOT,
                    text=True, capture_output=True, timeout=480, check=False, env=test_env,
                )
            except (OSError, subprocess.TimeoutExpired) as exc:
                write_json({"decision": "block", "reason": f"Could not finish {project} tests: {exc}"})
                break
            if test.returncode != 0:
                detail = (test.stdout + "\n" + test.stderr)[-1800:]
                write_json({"decision": "block", "reason": f"{project} tests failed. Fix and rerun.\n{detail}"})
                break
        else:
            write_json({"continue": True})
    else:
        write_json({"continue": True})

elif KIND == "SessionEnd":
    transcript = EVENT.get("transcript_path")
    if transcript and Path(transcript).is_file():
        transcript_dir = ROOT / "feedback" / "transcripts"
        transcript_dir.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(transcript, transcript_dir / f"{SESSION}.jsonl")
