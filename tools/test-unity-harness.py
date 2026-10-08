#!/usr/bin/env python3
"""Run Roslyn feasibility probes and the repository test harness inside Unity Mono."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import runpy
import shutil
import signal
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROBE_ASSEMBLY = "Spire.UnityCompilerProbeTests"
HARNESS_ASSEMBLY = "Spire.UnityTestHarness"
SUITES = (
    ("Houtamelo.Spire.Analyzers.Tests", "Houtamelo.Spire.Analyzers.Tests.csproj"),
    ("Houtamelo.Spire.SourceGenerators.Tests", "Houtamelo.Spire.SourceGenerators.Tests.csproj"),
    ("Houtamelo.Spire.PatternAnalysis.Tests", "Houtamelo.Spire.PatternAnalysis.Tests.csproj"),
    ("DevTools.Tests", "DevTools.Tests.csproj"),
    ("Houtamelo.Spire.GlobalConfig.EndToEnd", "Spire.GlobalConfig.EndToEnd.csproj"),
)


def runtime_dependencies(output, assembly_name):
    manifest = json.loads((output / (assembly_name + ".deps.json")).read_text())
    names = sorted({Path(path).name for target in manifest["targets"].values()
                    for library in target.values() for path in library.get("runtime", {})
                    if path.endswith(".dll")})
    files = [output / name for name in names]
    for path in files:
        if not path.is_file():
            raise ValueError("Missing manifest dependency: " + str(path))
    return files


def run_unity(command, timeout, environment):
    with subprocess.Popen(command, cwd=ROOT, env=environment, start_new_session=True) as process:
        try:
            process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.communicate()
            raise
        return subprocess.CompletedProcess(command, process.returncode)


def import_dependency(source, dependencies):
    target = dependencies / source.name
    if target.exists():
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        target_hash = hashlib.sha256(target.read_bytes()).hexdigest()
        if source_hash != target_hash:
            raise ValueError(f"Conflicting test dependency: {source} ({source_hash}) and {target} ({target_hash})")
        return
    shutil.copy2(source, target)
    guid = hashlib.sha256(("unity-compiler-tests/" + source.name).encode()).hexdigest()[:32]
    (dependencies / (source.name + ".meta")).write_text(
        "fileFormatVersion: 2\nguid: " + guid + "\nPluginImporter:\n"
        "  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n"
        "  isOverridable: 0\n  isExplicitlyReferenced: 1\n  validateReferences: 1\n"
        "  platformData:\n  - first:\n      '': Any\n    second:\n      enabled: 0\n      settings: {}\n"
        "  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n      settings:\n        DefaultValueInitialized: true\n"
        "  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")


def prepare_probe(project, editor, configuration, build):
    behavioral = runpy.run_path(str(ROOT / "tools/test-unity-behavioral.py"))
    behavioral["prepare_project"](project, configuration, build, editor)
    dependency_project = ROOT / "tools/UnityCompilerTestDependencies/UnityCompilerTestDependencies.csproj"
    if build:
        subprocess.run(["dotnet", "build", str(dependency_project), "-c", configuration, "--nologo"],
                       cwd=ROOT, check=True)
    fixture = ROOT / "tests/UnityBehavioral/Assets/CompilerProbe"
    target = project / "Assets/CompilerProbe"
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(fixture, target)
    (target / "csc.rsp").write_text("-langversion:12\n-nullable:enable\n", encoding="utf-8")
    dependencies = project / "Assets/Tests/Dependencies"
    shutil.rmtree(dependencies)
    dependencies.mkdir()
    names = set()
    output = dependency_project.parent / "bin" / configuration / "netstandard2.1"
    for dll in runtime_dependencies(output, "UnityCompilerTestDependencies"):
        if dll.name in ("UnityCompilerTestDependencies.dll", "UnityTestDependencies.dll", "Houtamelo.Spire.dll"):
            continue
        source = editor.resolve().parent / "Data/Managed/Newtonsoft.Json.dll" if dll.name == "Newtonsoft.Json.dll" else dll
        import_dependency(source, dependencies)
        names.add(dll.name)
    for required in ("Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll", "Microsoft.CodeAnalysis.Workspaces.dll"):
        if required not in names:
            raise ValueError("Missing compiler test dependency: " + required)
    asmdef = target / (PROBE_ASSEMBLY + ".asmdef")
    definition = json.loads(asmdef.read_text())
    definition["precompiledReferences"] = sorted(set(definition.get("precompiledReferences", [])) | names)
    asmdef.write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8")


def prepare_harness(project, editor, configuration, build):
    prepare_probe(project, editor, configuration, build)
    dependencies = project / "Assets/Tests/Dependencies"
    bundle_project = ROOT / "tools/UnityHarnessDependencies/UnityHarnessDependencies.csproj"
    if build:
        subprocess.run(["dotnet", "build", str(bundle_project), "-c", configuration,
                        "-p:SpireUnityTestHost=true", "--nologo"], cwd=ROOT, check=True)
    bundle = bundle_project.parent / "bin" / configuration / "netstandard2.1"
    if not (bundle / "UnityHarnessDependencies.dll").is_file():
        raise ValueError("Missing full harness dependency bundle; run without --no-build.")
    shutil.rmtree(dependencies)
    dependencies.mkdir()
    dependency_inputs = []
    for dll in runtime_dependencies(bundle, "UnityHarnessDependencies"):
        if dll.name in ("UnityHarnessDependencies.dll", "Houtamelo.Spire.dll"):
            continue
        source = editor.resolve().parent / "Data/Managed/Newtonsoft.Json.dll" if dll.name == "Newtonsoft.Json.dll" else dll
        import_dependency(source, dependencies)
        dependency_inputs.append({"name": dll.name, "source": str(source),
                                  "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
    data_root = project / "TestData"
    if data_root.exists():
        shutil.rmtree(data_root)
    data_root.mkdir()
    assemblies = []
    sources = []
    for folder, filename in SUITES:
        source_project = ROOT / "tests" / folder / filename
        output = source_project.parent / "bin" / configuration / "netstandard2.1"
        assembly = "Spire.GlobalConfig.EndToEnd" if folder == "Houtamelo.Spire.GlobalConfig.EndToEnd" else folder
        test_dll = output / (assembly + ".dll")
        if not test_dll.is_file():
            raise ValueError("Missing portable test assembly: " + str(test_dll))
        assemblies.append(assembly)
        if hashlib.sha256(test_dll.read_bytes()).digest() != hashlib.sha256((dependencies / test_dll.name).read_bytes()).digest():
            raise ValueError("Stale test assembly in dependency bundle: " + str(test_dll))
        target = data_root / assembly
        target.mkdir()
        for path in output.iterdir():
            if path.is_dir() and not path.name.startswith("runtimes"):
                shutil.copytree(path, target / path.name)
        unity_fixtures = source_project.parent / "Unity/fixtures"
        if unity_fixtures.exists():
            shutil.copytree(unity_fixtures, target / "Unity/fixtures", dirs_exist_ok=True)
        for source in source_project.parent.rglob("*.cs"):
            if any(part in ("bin", "obj") for part in source.relative_to(source_project.parent).parts):
                continue
            sources.append({"source": str(source.relative_to(ROOT)), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})

    packs = list((Path.home() / ".nuget/packages/microsoft.netcore.app.ref").glob("8*/ref/net8.0"))
    if not packs:
        raise ValueError("The .NET 8 reference pack is missing; restore the normal test suite first.")
    reference_source = sorted(packs, key=lambda path: tuple(int(x) for x in path.parents[1].name.split(".")))[-1]
    references = project / "ReferenceAssemblies/net8.0"
    if references.exists():
        shutil.rmtree(references)
    references.mkdir(parents=True, exist_ok=True)
    for path in reference_source.glob("*.dll"):
        shutil.copy2(path, references / path.name)

    target = project / "Assets/Harness"
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(ROOT / "tests/UnityTestHarness/Assets/Tests", target)
    asmdef = target / (HARNESS_ASSEMBLY + ".asmdef")
    definition = json.loads(asmdef.read_text())
    definition["precompiledReferences"] = sorted(set(definition.get("precompiledReferences", [])) |
                                                 {path.name for path in dependencies.glob("*.dll")})
    asmdef.write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8")
    discovery = project / "harness-discovery.json"
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/UnityHarnessManifest/UnityHarnessManifest.csproj"),
                    "-c", configuration, "--", str(ROOT), configuration,
                    str(project / "expected-harness-discovery.json")], cwd=ROOT, check=True)
    baseline = json.loads((project / "expected-harness-discovery.json").read_text())["cases"]
    expected_counts = {assembly: sum(item["assembly"] == assembly for item in baseline) for assembly in assemblies}
    manifest = {"expectedCounts": expected_counts, "inputs": sources, "referenceSource": str(reference_source),
                "dependencies": dependency_inputs,
                "references": [{"name": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                               for path in sorted(references.glob("*.dll"))]}
    (project / "harness-inputs.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {
        "SPIRE_UNITY_TEST_DATA_ROOT": str(data_root),
        "SPIRE_UNITY_TEST_REFERENCE_ROOT": str(references),
        "SPIRE_UNITY_TEST_ASSEMBLIES": ";".join(assemblies),
        "SPIRE_UNITY_TEST_DISCOVERY_PATH": str(discovery),
        "SPIRE_UNITY_TEST_CONFIGURATION": configuration,
        "SPIRE_UNITY_TEST_REPOSITORY_ROOT": str(ROOT),
        "SPIRE_UNITY_EDITOR": str(editor.resolve()),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, default=Path(os.environ.get(
        "SPIRE_UNITY_EDITOR", str(Path.home() / "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"))))
    parser.add_argument("--project", type=Path, default=ROOT / "tmp/unity-compiler-tests")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--probe", action="store_true")
    parser.add_argument("--timeout", type=int, default=300)
    args = parser.parse_args()
    if not args.editor.is_file():
        parser.error("Set SPIRE_UNITY_EDITOR or --editor to the installed Editor.")
    project = args.project.resolve()
    os.environ.update({"CI": "true", "GIT_TERMINAL_PROMPT": "0"})
    environment = os.environ.copy()
    marker = project / ".spire-unity-behavioral"
    if marker.is_file() and marker.read_text(encoding="utf-8") == "Spire runtime behavioral tests\n":
        names = ("compiler-probe-results.xml", "unity-compiler-probe.log") if args.probe else (
            "harness-results.xml", "unity-harness.log", "harness-inputs.json", "harness-discovery.json",
            "expected-harness-discovery.json", "behavioral-inputs.json")
        previous = [project / name for name in names if (project / name).exists()]
        if previous:
            archive = project / "TestResults" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
            archive.mkdir(parents=True)
            for path in previous:
                shutil.move(str(path), archive / path.name)
    if args.probe:
        prepare_probe(project, args.editor, args.configuration, not args.no_build)
    else:
        environment.update(prepare_harness(project, args.editor, args.configuration, not args.no_build))
    if args.prepare_only:
        print("Prepared: " + str(project))
        return 0
    report = project / ("compiler-probe-results.xml" if args.probe else "harness-results.xml")
    log = project / ("unity-compiler-probe.log" if args.probe else "unity-harness.log")
    selected = PROBE_ASSEMBLY if args.probe else HARNESS_ASSEMBLY + ";Spire.UnityBehavioralTests"
    command = [str(args.editor.resolve()), "-batchmode", "-nographics", "-projectPath", str(project),
               "-runTests", "-testPlatform", "EditMode", "-assemblyNames", selected,
               "-testResults", str(report), "-logFile", str(log)]
    try:
        result = run_unity(command, args.timeout, environment)
    except subprocess.TimeoutExpired:
        print("Unity timed out: " + str(log))
        return 124
    if not report.exists():
        print("No probe results. Unity exit code: " + str(result.returncode) + ". Log: " + str(log))
        return 1
    root = ET.parse(report).getroot()
    print("Unity results: " + json.dumps(root.attrib, sort_keys=True))
    print("Report: " + str(report))
    failed_cases = [case for case in root.iter("test-case") if case.get("result") != "Passed"]
    for case in (list(root.iter("test-case")) if args.probe else failed_cases[:20]):
        if args.probe or case.get("result") != "Passed":
            print(case.get("fullname", "") + ": " + case.get("result", ""))
        if case.get("result") != "Passed":
            print((case.findtext("failure/message") or "")[:900])
    if len(failed_cases) > 20:
        print("Additional failures: " + str(len(failed_cases) - 20) + "; full details are in the XML report.")
    cases = list(root.iter("test-case"))
    complete = bool(cases)
    if not args.probe:
        manifest_path = project / "harness-discovery.json"
        if not manifest_path.exists():
            print("Missing harness discovery manifest.")
            complete = False
        else:
            manifest = json.loads(manifest_path.read_text())
            counts = {name: sum(item["assembly"] == name for item in manifest["cases"])
                      for name in json.loads((project / "harness-inputs.json").read_text())["expectedCounts"]}
            expected = json.loads((project / "harness-inputs.json").read_text())["expectedCounts"]
            print("Discovered original suites: " + json.dumps(counts, sort_keys=True))
            baseline = json.loads((project / "expected-harness-discovery.json").read_text())["cases"]
            expected_ids = sorted(item["identity"] for item in baseline)
            actual_ids = sorted(item["identity"] for item in manifest["cases"])
            expected_names = sorted(item["displayName"] for item in manifest["cases"])
            actual_names = sorted(case.get("name", "") for case in cases
                                  if case.get("classname", "").endswith("XunitReflectionNUnitRunner"))
            complete = counts == expected and expected_ids == actual_ids and expected_names == actual_names
            behavioral_expected = json.loads((project / "behavioral-inputs.json").read_text())["expectedFactNames"]
            behavioral_actual = sorted(case.get("name", "") for case in cases
                                       if case.get("name", "").startswith("Spire.BehavioralTests.Tests."))
            complete = complete and behavioral_actual == behavioral_expected
            print("Exact .NET/Unity case identity match: " + str(expected_ids == actual_ids))
            print("All discovered cases executed: " + str(expected_names == actual_names))
            print("All original behavioral cases executed: " + str(behavioral_actual == behavioral_expected))
    return 0 if result.returncode == 0 and complete and all(case.get("result") == "Passed" for case in cases) else 1


if __name__ == "__main__":
    raise SystemExit(main())
