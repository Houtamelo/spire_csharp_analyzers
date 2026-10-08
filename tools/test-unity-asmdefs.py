#!/usr/bin/env python3
"""Verify Spire analyzer/generator scope through real Unity assembly definitions."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import runpy
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "tests/UnityAsmdefIntegration"
MARKER = "Spire assembly definition integration\n"


def prepare_project(project, configuration, build=True):
    project = project.resolve()
    if project == FIXTURE.resolve():
        raise ValueError("Use a disposable project; the tracked fixture must remain unchanged.")
    marker = project / ".spire-unity-asmdefs"
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        raise ValueError("The project contains unrelated files; choose an empty disposable directory.")
    if marker.exists() and marker.read_text() != MARKER:
        raise ValueError("The project ownership marker is invalid.")
    project.mkdir(parents=True, exist_ok=True)
    marker.write_text(MARKER)
    (project / "global.json").write_text(json.dumps({"sdk": {"version": "8.0.318", "rollForward": "latestPatch"}}, indent=2) + "\n")
    state = project / "Library/Spire.UnityAsmdefIntegration-state.json"
    if state.exists():
        state.unlink()
    previous = [p for name in ("asmdef-results.json", "asmdef-inputs.json", "unity-asmdefs.log")
                if (p := project / name).exists()]
    if previous:
        archive = project / "TestResults" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        archive.mkdir(parents=True)
        for p in previous:
            shutil.move(str(p), archive / p.name)
    inputs = []
    for name in ("Assets", "Packages", "ProjectSettings"):
        target = project / name
        if target.exists():
            shutil.rmtree(target)
        source = FIXTURE / name
        if not source.is_dir() and name == "ProjectSettings":
            source = ROOT / "tests/UnityIntegration/ProjectSettings"
        shutil.copytree(source, target)
        for p in sorted(source.rglob("*")):
            if p.is_file():
                inputs.append({"source": str(p.relative_to(ROOT)),
                               "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
    ruleset = project / "Assets/Default.ruleset"
    directories = {project / "Assets"} | {p.parent for p in (project / "Assets").rglob("*.asmdef")}
    for directory in directories:
        (directory / "csc.rsp").write_text('-langversion:9\n-ruleset:"' + str(ruleset) + '"\n')
    build_package = runpy.run_path(str(ROOT / "tools/build-unity-package.py"))["build_package"]
    build_package(project / "Packages/com.houtamelo.spire", configuration, build)
    (project / "asmdef-inputs.json").write_text(json.dumps({"inputs": inputs}, indent=2) + "\n")
    return project


def analyzer_references(project):
    """Read Unity's actual compiler response files, without invoking Roslyn ourselves."""
    result = {}
    for path in (project / "Library/Bee").rglob("*.rsp"):
        text = path.read_text(errors="replace")
        outputs = [line for line in text.splitlines() if line.lstrip().startswith(("/out:", "-out:"))]
        if not outputs:
            continue
        assembly = Path(outputs[0].split(":", 1)[1].strip().strip('"')).stem
        refs = [line.split(":", 1)[1].strip().strip('"') for line in text.splitlines()
                if line.lstrip().startswith(("/analyzer:", "-analyzer:"))]
        if refs:
            result[assembly] = {"responseFile": str(path.relative_to(project)), "analyzers": refs}
    return result


def generated_project_references(project):
    result = {}
    for path in project.glob("*.csproj"):
        tree = ET.parse(path)
        analyzers = [node.get("Include") for node in tree.iter() if node.tag.split("}")[-1] == "Analyzer"]
        result[path.stem] = {"project": str(path), "analyzers": analyzers}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, default=Path(os.environ.get(
        "SPIRE_UNITY_EDITOR", str(Path.home() / "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"))))
    parser.add_argument("--project", type=Path, default=ROOT / "tmp/unity-asmdefs")
    parser.add_argument("--rider", type=Path, default=Path(os.environ.get(
        "SPIRE_RIDER_ROOT", str(Path.home() / ".local/share/JetBrains/Toolbox/apps/rider-2"))))
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()
    if not args.editor.is_file():
        parser.error("Set --editor or SPIRE_UNITY_EDITOR to the installed Unity Editor.")
    project = prepare_project(args.project, args.configuration, not args.no_build)
    if args.prepare_only:
        print("Prepared: " + str(project))
        return 0
    environment = dict(os.environ, CI="true", GIT_TERMINAL_PROMPT="0", SPIRE_RIDER_ROOT=str(args.rider.resolve()))
    command = [str(args.editor.resolve()), "-batchmode", "-nographics", "-projectPath", str(project),
               "-executeMethod", "Spire.UnityAsmdefIntegration.AsmdefIntegration.Run",
               "-logFile", str(project / "unity-asmdefs.log")]
    runner = runpy.run_path(str(ROOT / "tools/test-unity-harness.py"))["run_unity"]
    try:
        result = runner(command, args.timeout, environment)
    except subprocess.TimeoutExpired:
        print("Unity timed out: " + str(project / "unity-asmdefs.log"))
        return 124
    report = project / "asmdef-results.json"
    if not report.is_file():
        print("Unity produced no asmdef report; exit=" + str(result.returncode))
        return 1
    data = json.loads(report.read_text())
    data["compilerResponses"] = analyzer_references(project)
    data["generatedProjects"] = generated_project_references(project)
    report.write_text(json.dumps(data, indent=2) + "\n")
    print("Report: " + str(report))
    print(json.dumps({k: v for k, v in data.items() if not isinstance(v, (dict, list))}, sort_keys=True))
    expected = {row["assembly"] for row in json.loads((FIXTURE / "matrix.json").read_text())["scenarios"]}
    required = {"Houtamelo.Spire.Analyzers.dll", "Houtamelo.Spire.PatternAnalysis.dll", "Houtamelo.Spire.CodeFixes.dll"}
    complete = True
    for assembly in sorted(expected):
        for key in ("compilerResponses", "generatedProjects"):
            names = {Path(p.replace("\\", "/")).name for p in data[key].get(assembly, {}).get("analyzers", [])}
            if not required <= names:
                print("Missing analyzer references in " + key + ": " + assembly + " " + str(sorted(required - names)))
                complete = False
    return 0 if result.returncode == 0 and data.get("status") == "pass" and complete else 1


if __name__ == "__main__":
    raise SystemExit(main())
