#!/usr/bin/env python3
"""Build the Unity package without vendoring compiler-host dependencies."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
ASSEMBLIES = (
    ("Houtamelo.Spire", "Runtime", False),
    ("Houtamelo.Spire.Analyzers", "Analyzers", True),
    ("Houtamelo.Spire.PatternAnalysis", "Analyzers", True),
    ("Houtamelo.Spire.CodeFixes", "Analyzers", True),
)


def guid(relative):
    return hashlib.sha256(("Houtamelo.Spire.Unity/" + relative).encode()).hexdigest()[:32]


def plugin_metadata(relative, analyzer):
    enabled = 0 if analyzer else 1
    labels = "labels:\n- RoslynAnalyzer\n" if analyzer else ""
    return f"""fileFormatVersion: 2
guid: {guid(relative)}
{labels}PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: {enabled}
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: {enabled}
      settings:
        DefaultValueInitialized: true
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def build_package(output, configuration, build=True):
    versions = [
        ET.parse(ROOT / "src" / name / (name + ".csproj")).findtext("./PropertyGroup/Version")
        for name in ("Houtamelo.Spire", "Houtamelo.Spire.Analyzers", "Houtamelo.Spire.CodeFixes")
    ]
    if len(set(versions)) != 1 or not versions[0]:
        raise ValueError("The three Spire package versions must match.")

    if build:
        subprocess.run(
            ["dotnet", "build", "src/Houtamelo.Spire/Houtamelo.Spire.csproj", "-c", configuration],
            cwd=ROOT,
            check=True,
        )

    sources = []
    expected = {"package.json", "package.json.meta", "README.md", "README.md.meta", "LICENSE", "LICENSE.meta"}
    for name, folder, analyzer in ASSEMBLIES:
        source = ROOT / "src" / name / "bin" / configuration / "netstandard2.0" / (name + ".dll")
        if not source.is_file():
            raise FileNotFoundError(source)
        relative = folder + "/" + source.name
        sources.append((source, relative, analyzer))
        expected.update((relative, relative + ".meta"))
        expected.add(folder + ".meta")

    output = output.resolve()
    if output.exists():
        unexpected = {
            str(p.relative_to(output)) for p in output.rglob("*") if p.is_file()
        } - expected
        if unexpected:
            raise ValueError("Output contains unrelated files: " + ", ".join(sorted(unexpected)))
    output.mkdir(parents=True, exist_ok=True)

    for source, relative, analyzer in sources:
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        target.with_suffix(target.suffix + ".meta").write_text(
            plugin_metadata(relative, analyzer), encoding="utf-8"
        )

    for folder in {folder for _, folder, _ in ASSEMBLIES}:
        (output / (folder + ".meta")).write_text(
            f"fileFormatVersion: 2\nguid: {guid(folder)}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
            encoding="utf-8",
        )

    shutil.copyfile(ROOT / "LICENSE", output / "LICENSE")

    manifest = {
        "name": "com.houtamelo.spire",
        "version": versions[0],
        "displayName": "Houtamelo.Spire",
        "description": "Spire analyzers and discriminated unions for Unity 6000.5 with Mono.",
        "unity": "6000.5",
        "dependencies": {
            "com.unity.modules.imgui": "1.0.0",
            "com.unity.modules.uielements": "1.0.0",
        },
        "author": {"name": "Houtamelo"},
        "license": "MIT",
    }
    (output / "package.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (output / "README.md").write_text(
        "# Houtamelo.Spire for Unity\n\n"
        "Built from the unity-6000 branch for Unity 6000.5 and Mono.\n"
        "Add this folder as a local package using its package.json.\n"
        "Apply System.Serializable to a mutable struct union to enable Unity serialization.\n"
        "Serialization defaults to Additive; explicit BoxedFields, BoxedTuple, UnsafeOverlap, and Overlap are supported.\n"
        "Variant fields must use native Unity-supported serialization types.\n"
        "UnsafeOverlap requires unsafe code; Overlap cannot be generic or nested in a generic type.\n"
        "Rider is the supported IDE; compiler-host DLLs are supplied by the host.\n",
        encoding="utf-8",
    )
    for name in ("package.json", "README.md", "LICENSE"):
        (output / (name + ".meta")).write_text(
            f"fileFormatVersion: 2\nguid: {guid(name)}\nTextScriptImporter:\n"
            "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
            encoding="utf-8",
        )
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "tmp/unity-package/com.houtamelo.spire")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    args = parser.parse_args()
    output = build_package(args.output, args.configuration, not args.no_build)
    print("Unity package: " + str(output))
    print("DLLs: " + ", ".join(sorted(str(p.relative_to(output)) for p in output.rglob("*.dll"))))


if __name__ == "__main__":
    main()
