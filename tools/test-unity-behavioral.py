#!/usr/bin/env python3
"""Compile the existing runtime behavioral sources in Unity and run their assertions."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import shutil
import subprocess
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = "Spire.UnityBehavioralTests"
MARKER_CONTENT = "Spire runtime behavioral tests\n"


def prepare_project(project, configuration, build, editor=None):
    project = project.resolve()
    fixture = ROOT / "tests/UnityBehavioral"
    if project == fixture.resolve():
        raise ValueError("Use a disposable project; the tracked fixture must remain unchanged.")
    marker = project / ".spire-unity-behavioral"
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        raise ValueError("The project contains unrelated files; choose an empty disposable directory.")
    if marker.exists() and marker.read_text(encoding="utf-8") != MARKER_CONTENT:
        raise ValueError("The project ownership marker is invalid; choose an empty disposable directory.")
    project.mkdir(parents=True, exist_ok=True)
    marker.write_text(MARKER_CONTENT, encoding="utf-8")
    previous = [project / name for name in ("behavioral-results.xml", "unity-behavioral.log", "behavioral-inputs.json")
                if (project / name).exists()]
    if previous:
        archive = project / "TestResults" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        archive.mkdir(parents=True)
        for path in previous:
            shutil.move(str(path), archive / path.name)

    for name in ("Assets", "Packages", "ProjectSettings"):
        owned = project / name
        if owned.exists():
            shutil.rmtree(owned)
    for name in ("Assets", "Packages"):
        shutil.copytree(fixture / name, project / name, dirs_exist_ok=True)
    shutil.copytree(ROOT / "tests/UnityIntegration/ProjectSettings", project / "ProjectSettings", dirs_exist_ok=True)

    builder = runpy.run_path(str(ROOT / "tools/build-unity-package.py"))["build_package"]
    builder(project / "Packages/com.houtamelo.spire", configuration, build)
    dependency_project = ROOT / "tools/UnityTestDependencies/UnityTestDependencies.csproj"
    if build:
        subprocess.run(["dotnet", "build", str(dependency_project), "-c", configuration, "--nologo"],
                       cwd=ROOT, check=True)

    dependency_output = dependency_project.parent / "bin" / configuration / "netstandard2.1"
    dependencies = project / "Assets/Tests/Dependencies"
    if dependencies.exists():
        shutil.rmtree(dependencies)
    dependencies.mkdir(parents=True, exist_ok=True)
    dependency_names = []
    editor_dependencies = {}
    for dll in sorted(dependency_output.glob("*.dll")):
        if dll.name == "UnityTestDependencies.dll":
            continue
        if "CodeAnalysis" in dll.name or "Workspaces" in dll.name:
            raise ValueError("Compiler-host dependencies are not permitted: " + dll.name)
        source = dll
        if dll.name == "Newtonsoft.Json.dll" and editor is not None:
            source = editor.resolve().parent / "Data/Managed/Newtonsoft.Json.dll"
            if not source.is_file():
                raise ValueError("The Editor's Newtonsoft.Json assembly is missing: " + str(source))
            editor_dependencies[dll.name] = {"source": str(source), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()}
        shutil.copy2(source, dependencies / dll.name)
        guid = hashlib.sha256(("unity-behavioral/" + dll.name).encode()).hexdigest()[:32]
        (dependencies / (dll.name + ".meta")).write_text(
            "fileFormatVersion: 2\nguid: " + guid + "\nPluginImporter:\n"
            "  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n"
            "  isOverridable: 0\n  isExplicitlyReferenced: 1\n  validateReferences: 1\n"
            "  platformData:\n  - first:\n      '': Any\n    second:\n      enabled: 0\n      settings: {}\n"
            "  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n      settings:\n        DefaultValueInitialized: true\n"
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
        dependency_names.append(dll.name)
    for required in ("xunit.assert.dll", "xunit.core.dll", "Newtonsoft.Json.dll", "System.Text.Json.dll"):
        if required not in dependency_names:
            raise ValueError("Missing test dependency: " + required + "; run without --no-build.")

    asmdef = project / "Assets/Tests" / (ASSEMBLY + ".asmdef")
    definition = json.loads(asmdef.read_text())
    definition["precompiledReferences"] = sorted(set(definition.get("precompiledReferences", [])) | set(dependency_names))
    asmdef.write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8")
    (asmdef.parent / "csc.rsp").write_text("-langversion:12\n-nullable:enable\n", encoding="utf-8")

    behavioral = ROOT / "tests/Houtamelo.Spire.BehavioralTests"
    inputs = []
    fact_count = 0
    expected_names = []
    for folder in ("Types", "Tests"):
        target = project / "Assets/Tests/Existing" / folder
        if target.exists():
            shutil.rmtree(target)
        target.mkdir(parents=True)
        for source in sorted((behavioral / folder).glob("*.cs")):
            shutil.copy2(source, target / source.name)
            data = source.read_bytes()
            if folder == "Tests":
                count = len(re.findall(rb"\[Fact(?:\([^\]]*\))?\]", data))
                fact_count += count
                text = data.decode("utf-8-sig")
                namespaces = re.findall(r"^namespace\s+([\w.]+)\s*;", text, re.MULTILINE)
                classes = re.findall(r"^public\s+class\s+(\w+)\b", text, re.MULTILINE)
                methods = re.findall(r"\[Fact(?:\([^\]]*\))?\]\s+public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\s*\(", text)
                if len(namespaces) != 1 or len(classes) != 1 or len(methods) != count:
                    raise ValueError("Unsupported behavioral source shape; cannot verify every Fact name: " + str(source))
                expected_names.extend(namespaces[0] + "." + classes[0] + "." + method for method in methods)
            inputs.append({"source": str(source.relative_to(ROOT)),
                           "sha256": hashlib.sha256(data).hexdigest()})
    if not fact_count:
        raise ValueError("No original behavioral test assertions were found.")
    (project / "behavioral-inputs.json").write_text(json.dumps(
        {"expectedFacts": fact_count, "expectedFactNames": sorted(expected_names), "inputs": inputs,
         "testDependencies": dependency_names, "editorProvidedDependencies": editor_dependencies}, indent=2) + "\n", encoding="utf-8")
    return fact_count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, default=Path(os.environ.get(
        "SPIRE_UNITY_EDITOR", str(Path.home() / "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"))))
    parser.add_argument("--project", type=Path, default=ROOT / "tmp/unity-behavioral")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--timeout", type=int, default=300)
    args = parser.parse_args()
    if not args.editor.is_file():
        parser.error("Set SPIRE_UNITY_EDITOR or --editor to the installed Editor executable.")
    os.environ.update({"CI": "true", "GIT_TERMINAL_PROMPT": "0"})
    try:
        expected = prepare_project(args.project, args.configuration, not args.no_build, args.editor)
    except (ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + "\n")
    project = args.project.resolve()
    print("Existing behavioral assertions: " + str(expected), flush=True)
    print("Project: " + str(project), flush=True)
    if args.prepare_only:
        return 0
    report = project / "behavioral-results.xml"
    log = project / "unity-behavioral.log"
    command = [str(args.editor.resolve()), "-batchmode", "-nographics", "-projectPath", str(project),
               "-runTests", "-testPlatform", "EditMode", "-assemblyNames", ASSEMBLY,
               "-testResults", str(report), "-logFile", str(log)]
    try:
        result = subprocess.run(command, cwd=ROOT, timeout=args.timeout)
    except subprocess.TimeoutExpired:
        print("Unity timed out. Log: " + str(log))
        return 124
    if not report.exists():
        print("Unity produced no test report. Log: " + str(log))
        return 1
    root = ET.parse(report).getroot()
    facts = [case for case in root.iter("test-case")
             if case.get("name", "").startswith("Spire.BehavioralTests.Tests.")]
    failures = [case for case in root.iter("test-case") if case.get("result") != "Passed"]
    expected_names = json.loads((project / "behavioral-inputs.json").read_text())["expectedFactNames"]
    actual_names = sorted(case.get("name") for case in facts)
    print("Unity exit code: " + str(result.returncode))
    print("Original behavioral assertions executed: " + str(len(facts)))
    print("Test result: " + json.dumps(root.attrib, sort_keys=True))
    print("Report: " + str(report))
    for case in failures:
        print(case.get("fullname", "") + ": " + (case.findtext("failure/message") or case.get("result", "")))
    if actual_names != expected_names:
        print("Missing assertions: " + json.dumps(sorted(set(expected_names) - set(actual_names))))
        print("Extra assertions: " + json.dumps(sorted(set(actual_names) - set(expected_names))))
    return 0 if result.returncode == 0 and actual_names == expected_names and not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
