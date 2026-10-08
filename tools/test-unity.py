#!/usr/bin/env python3
"""Run the generated-code integration fixture in the installed Unity Editor."""

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import runpy
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, default=Path(os.environ.get(
        "SPIRE_UNITY_EDITOR",
        str(Path.home() / "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"),
    )))
    parser.add_argument("--project", type=Path, default=ROOT / "tmp/unity-integration")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--graphics", action="store_true")
    parser.add_argument("--method", default="Spire.UnityIntegration.SpireUnityIntegration.Run")
    parser.add_argument("--timeout", type=int, default=240)
    args = parser.parse_args()
    if not args.editor.is_file():
        parser.error("Set SPIRE_UNITY_EDITOR or --editor to the installed Editor executable.")

    fixture = ROOT / "tests/UnityIntegration"
    if not (fixture / "Assets").is_dir():
        parser.error("The Unity integration fixture is missing.")
    project = args.project.resolve()
    if project == fixture.resolve():
        parser.error("Use a disposable project path; the tracked fixture must remain unchanged.")
    marker = project / ".spire-unity-integration"
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        parser.error("The project path contains unrelated files; choose an empty disposable directory.")
    project.mkdir(parents=True, exist_ok=True)
    marker.write_text("Spire generated-code integration fixture\n", encoding="utf-8")
    for name in ("Assets", "Packages", "ProjectSettings"):
        source = fixture / name
        if source.is_dir():
            shutil.copytree(source, project / name, dirs_exist_ok=True)

    builder = runpy.run_path(str(ROOT / "tools/build-unity-package.py"))["build_package"]
    builder(project / "Packages/com.houtamelo.spire", args.configuration, not args.no_build)

    previous_reports = (
        list(project.glob("*results*.json"))
        + list(project.glob("*build-report*.json"))
        + list(project.glob("unity-integration-*.log"))
    )
    lifecycle_stage = project / "Library/SpireUnityIntegration-stage.json"
    if lifecycle_stage.is_file():
        previous_reports.append(lifecycle_stage)
    if previous_reports:
        archive = project / "TestResults" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        archive.mkdir(parents=True)
        for report in previous_reports:
            shutil.move(str(report), archive / report.name)

    environment = os.environ.copy()
    environment.update({"CI": "true", "GIT_TERMINAL_PROMPT": "0"})
    command = [
        str(args.editor.resolve()), "-batchmode",
        "-projectPath", str(project), "-executeMethod", args.method,
    ]
    if not args.graphics:
        command.append("-nographics")

    # Retry only the observed pre-entrypoint Linux startup assertion. Test failures
    # and other crashes remain failures, rather than being hidden by a retry.
    result = None
    for attempt in range(1, 3):
        log = project / ("unity-integration-" + str(attempt) + ".log")
        try:
            result = subprocess.run(
                command + ["-logFile", str(log)],
                cwd=ROOT,
                env=environment,
                timeout=args.timeout,
            )
        except subprocess.TimeoutExpired:
            print("Unity timed out. Log: " + str(log))
            return 124
        text = log.read_text(errors="replace") if log.exists() else ""
        startup_assertion = (
            "ToFileDescriptor" in text and "Assertion" in text
            and "SPIRE_UNITY_INTEGRATION_BEGIN" not in text
        )
        if result.returncode == 0 or not startup_assertion:
            break
        print("Unity startup assertion; retrying once. Log: " + str(log))

    print("Unity exit code: " + str(result.returncode))
    print("Project and logs: " + str(project))
    reports = list(project.glob("*results*.json")) + list(project.glob("*build-report*.json"))
    for report in sorted(reports):
        try:
            data = json.loads(report.read_text())
        except (ValueError, OSError):
            continue
        print("Report: " + str(report))
        for key in ("unityVersion", "hardFailureCount", "failureCount", "passed", "failed"):
            if key in data:
                print(key + ": " + str(data[key]))
    return result.returncode if result.returncode >= 0 else 128 - result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
