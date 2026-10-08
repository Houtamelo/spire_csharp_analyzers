#!/usr/bin/env python3
"""Verify JetBrains inspection and real Rider quick-fix execution on the Unity fixture."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import shutil
import signal
import subprocess
import time
import zipfile

ROOT = Path(__file__).resolve().parents[1]
FIX_TITLE = "Replace with SpireEnum<T>.From(value)"


def validate_project(project):
    marker = project / ".spire-unity-asmdefs"
    if not marker.is_file() or marker.read_text() != "Spire assembly definition integration\n":
        raise ValueError("Use the disposable project created by test-unity-asmdefs.py.")
    state = json.loads((project / "asmdef-results.json").read_text())
    if state.get("status") != "pass":
        raise ValueError("The Unity asmdef acceptance stage must pass first.")
    solution = Path(state["solutionPath"]).resolve()
    if solution.parent != project or not solution.is_file():
        raise ValueError("The generated solution must belong to the disposable project.")
    return state


def issue_rows(report):
    data = json.loads(report.read_text())
    if not isinstance(data, dict) or data.get("version") != "2.1.0" or not isinstance(data.get("runs"), list) or not data["runs"]:
        raise ValueError("Expected a nonempty SARIF 2.1.0 report.")
    rows = []
    for run in data.get("runs", []):
        for item in run.get("results", []):
            locations = item.get("locations", [])
            if not locations:
                raise ValueError("An inspection result has no physical location.")
            for location in locations:
                physical = location.get("physicalLocation", {})
                if not physical.get("artifactLocation", {}).get("uri") or not physical.get("region", {}).get("startLine"):
                    raise ValueError("An inspection result has no source URI or line.")
                rows.append({"id": item.get("ruleId", ""),
                             "uri": physical.get("artifactLocation", {}).get("uri", ""),
                             "region": physical.get("region", {}), "message": item.get("message", {})})
    return rows


def inspect(project, cli, timeout, expected_version=None):
    source_report = json.loads((project / "asmdef-results.json").read_text())
    solution = Path(source_report["solutionPath"])
    report = project / "jetbrains-inspections.sarif"
    report.unlink(missing_ok=True)
    version_result = subprocess.run([str(cli), "inspectcode", "--version"], cwd=project,
                                    capture_output=True, text=True, timeout=30)
    version_match = re.search(r"^Version: ([0-9.]+)$", version_result.stdout, re.MULTILINE)
    if version_result.returncode != 0 or not version_match:
        raise RuntimeError("Could not determine the InspectCode version.")
    version = version_match.group(1)
    if expected_version and version != expected_version:
        raise RuntimeError("InspectCode version " + version + " does not match Rider " + expected_version + ".")
    command = [str(cli), "inspectcode", str(solution), "--no-build", "--no-updates", "--no-swea",
               "--properties=RunAnalyzers=true", "--output=" + str(report),
               "--caches-home=" + str(project / "JetBrainsInspectionCache")]
    with (project / "jetbrains-inspection.log").open("w") as log:
        result = subprocess.run(command, cwd=project, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
    if result.returncode != 0 or not report.is_file():
        raise RuntimeError("InspectCode did not produce a successful report: " + str(project / "jetbrains-inspection.log"))
    rows = issue_rows(report)
    expected = json.loads((ROOT / "tests/UnityAsmdefIntegration/matrix.json").read_text())["scenarios"]
    missing = [row["spire002Location"] for row in expected if not any(
        item["id"].endswith("SPIRE002") and item["uri"].replace("\\", "/").endswith(row["spire002Location"])
        for item in rows)]
    cast = [item for item in rows if item["id"].endswith("SPIRE016") and item["uri"].endswith("_rider.cs")]
    if missing or len(cast) != 1:
        raise RuntimeError("Missing or incorrect JetBrains diagnostics: " + json.dumps({"SPIRE002": missing, "SPIRE016": cast}))
    return {"status": "pass", "version": version, "solution": str(solution), "expectedSPIRE002": len(expected),
            "SPIRE016": cast, "report": str(report)}


def build_driver(rider, output):
    source = ROOT / "tools/JetBrainsIntegrationTestPlugin"
    classes = output / "classes"
    if classes.exists():
        shutil.rmtree(classes)
    classes.mkdir(parents=True)
    jars = list((rider / "lib").glob("*.jar"))
    args = output / "javac.args"
    args.write_text('--release\n21\n-classpath\n"' + ":".join(map(str, jars)) + '"\n-d\n"' + str(classes) + '"\n"' +
                    str(source / "src/spire/integration/SpireIntegrationDriver.java") + '"\n')
    subprocess.run([str(rider / "jbr/bin/javac"), "@" + str(args)], check=True, timeout=90)
    target = output / "plugins/SpireIntegrationDriver/lib"
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(target / "spire-integration-driver.jar", "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(source / "META-INF/plugin.xml", "META-INF/plugin.xml")
        for path in classes.rglob("*.class"):
            archive.write(path, str(path.relative_to(classes)))


def quick_fix(project, rider, timeout):
    state = validate_project(project)
    source = project / "Assets/RuntimeAuto/_rider.cs"
    before = source.read_text()
    token = "(ProbeKind)value"
    if before.count(token) != 1:
        raise RuntimeError("The Rider fixture must contain exactly one unfixed enum cast.")
    expected = before.replace(token, "SpireEnum<ProbeKind>.From(value)")
    work = project / "RiderDriver"
    work.mkdir(exist_ok=True)
    previous = [work / name for name in ("rider-quick-fix.json", "rider-console.log", "logs")
                if (work / name).exists()]
    if previous:
        archive = work / "attempts" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        archive.mkdir(parents=True)
        for path in previous:
            shutil.move(str(path), archive / path.name)
    (work / "before.cs.txt").write_text(before)
    (work / "expected-after.cs.txt").write_text(expected)
    build_driver(rider, work)
    report = work / "rider-quick-fix.json"
    report.unlink(missing_ok=True)
    properties = work / "idea.properties"
    properties.write_text("\n".join([
        "idea.config.path=" + str(work / "config"), "idea.system.path=" + str(work / "system"),
        "idea.plugins.path=" + str(work / "plugins"), "idea.log.path=" + str(work / "logs"),
        "idea.trust.all.projects=true", "ide.show.tips.on.startup.default.value=false",
        "idea.is.integration.test=true",
        "ide.show.whats.new=false",
    ]) + "\n")
    vm_options = work / "rider-test.vmoptions"
    vm_options.write_text((rider / "bin/rider64.vmoptions").read_text() + "\n-Dawt.toolkit.name=XToolkit\n" +
                          "-Dspire.integration.project=" + str(project) + "\n" +
                          "-Dspire.integration.report=" + str(report) + "\n")
    display_read, display_write = os.pipe()
    xvfb_log = (work / "xvfb.log").open("w")
    display = subprocess.Popen(["Xvfb", "-displayfd", str(display_write), "-screen", "0", "1440x1000x24", "-nolisten", "tcp"],
                               pass_fds=(display_write,), stdout=xvfb_log, stderr=subprocess.STDOUT, start_new_session=True)
    os.close(display_write)
    import select
    try:
        if not select.select([display_read], [], [], 15)[0]:
            raise RuntimeError("Xvfb did not allocate a display.")
        number = os.read(display_read, 128).decode().strip()
        environment = dict(os.environ, CI="true", DISPLAY=":" + number, RIDER_PROPERTIES=str(properties),
                           RIDER_VM_OPTIONS=str(vm_options), XDG_SESSION_TYPE="x11")
        environment.pop("WAYLAND_DISPLAY", None)
        with (work / "rider-console.log").open("w") as log:
            process = subprocess.Popen([str(rider / "bin/rider.sh"), state["solutionPath"]], cwd=project,
                                       env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
                raise
        if not report.is_file():
            raise RuntimeError("Rider test driver produced no report; inspect " + str(work / "logs"))
        data = json.loads(report.read_text())
        if (data.get("status") != "pass" or not data.get("discovered") or not data.get("applied")
                or FIX_TITLE not in data.get("detail", "")):
            raise RuntimeError("Rider quick-fix failed: " + json.dumps(data))
        after = source.read_text()
        (work / "after.cs.txt").write_text(after)
        if after != expected:
            (work / "expected-after.cs.txt").write_text(expected)
            raise RuntimeError("Rider source transformation differs from the expected fixture.")
        data["sourceBeforeSha256"] = hashlib.sha256(before.encode()).hexdigest()
        data["sourceAfterSha256"] = hashlib.sha256(after.encode()).hexdigest()
        info = json.loads((rider / "product-info.json").read_text())
        data["riderVersion"] = info["version"]
        data["riderBuild"] = info["buildNumber"]
        report.write_text(json.dumps(data, indent=2) + "\n")
        return data
    finally:
        os.close(display_read)
        if display.poll() is None:
            os.killpg(display.pid, signal.SIGTERM)
            display.wait(timeout=15)
        xvfb_log.close()


def recompile(project, editor, timeout):
    report = project / "rider-recompile-results.json"
    report.unlink(missing_ok=True)
    command = [str(editor), "-batchmode", "-nographics", "-projectPath", str(project),
               "-executeMethod", "Spire.UnityAsmdefIntegration.AsmdefIntegration.VerifyRiderFix",
               "-logFile", str(project / "rider-recompile.log")]
    runner = runpy.run_path(str(ROOT / "tools/test-unity-harness.py"))["run_unity"]
    result = runner(command, timeout, dict(os.environ, CI="true", GIT_TERMINAL_PROMPT="0"))
    if result.returncode != 0 or not report.is_file():
        raise RuntimeError("Unity did not verify the Rider fix: " + str(project / "rider-recompile.log"))
    data = json.loads(report.read_text())
    if data.get("status") != "pass":
        raise RuntimeError("Unity recompilation failed: " + json.dumps(data))
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=ROOT / "tmp/unity-asmdefs")
    parser.add_argument("--rider", type=Path, default=Path(os.environ.get("SPIRE_RIDER_ROOT", str(Path.home() / ".local/share/JetBrains/Toolbox/apps/rider-2"))))
    parser.add_argument("--cli", type=Path, default=ROOT / "tmp/jetbrains-cli/jb")
    parser.add_argument("--editor", type=Path, default=Path(os.environ.get(
        "SPIRE_UNITY_EDITOR", str(Path.home() / "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"))))
    parser.add_argument("--stage", choices=("all", "inspect", "quick-fix", "recompile"), default="all")
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()
    project = args.project.resolve()
    if not (project / "asmdef-results.json").is_file():
        parser.error("Run tools/test-unity-asmdefs.py first to generate the real Unity solution.")
    validate_project(project)
    results = {}
    if args.stage in ("all", "inspect"):
        product = args.rider.resolve() / "product-info.json"
        version = json.loads(product.read_text())["version"] if product.is_file() else None
        results["inspection"] = inspect(project, args.cli.resolve(), args.timeout, version)
    if args.stage in ("all", "quick-fix"):
        results["quickFix"] = quick_fix(project, args.rider.resolve(), args.timeout)
    if args.stage in ("all", "quick-fix", "recompile"):
        results["unityRecompile"] = recompile(project, args.editor.resolve(), args.timeout)
    report = project / "jetbrains-integration-results.json"
    report.write_text(json.dumps(results, indent=2) + "\n")
    print("Report: " + str(report))
    print(json.dumps({stage: data["status"] for stage, data in results.items()}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
