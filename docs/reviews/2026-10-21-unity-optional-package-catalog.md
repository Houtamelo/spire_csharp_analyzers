# Optional Unity package struct inventory

The search now covers **30,665 C# files from 143 optional packages**, pinned to
the installed Unity **6000.5.6f1** Editor's package catalog. After production-source
refinement, **877 public, non-generic, non-ref-like unmanaged structs** have at
least two instance fields. Of these, **478 types in inferred runtime scope** have
no known native pointer dependency and are candidates for manual review.

The [per-type selection review](2026-10-21-unity-optional-package-selection.md)
now suggests **291 additions**, defers 172, and excludes 15. It identifies two
Editor-only declarations that the original folder/assembly heuristic included.

- [Runtime candidates: 478 rows, with a blank Include column](unity-optional-package-candidates.csv)
- [All qualifying unmanaged structs: 877 rows](unity-optional-package-unmanaged.csv)
- [Unresolved public composite structs: 150 rows](unity-optional-package-unresolved.csv)
- [Package versions, coverage, and source/archive hashes](unity-optional-package-coverage.csv)

The [approved initial target](../designs/unity-initial-layout-targets.csv) now has
664 types: 373 engine entries and all 291 accepted package suggestions.
Size/alignment and target validation remain pending. The original candidate CSV
includes helper, iterator, system, and diagnostic state as well as ordinary payload data.

## Package coverage

The Editor catalog has 272 entries. Two additional bundled packages with source,
`com.unity.project-auditor` and `com.unity.shaderanalysis`, are also inventoried.

| Outcome | Packages |
|---|---:|
| C# sources scanned | 143 |
| Acquired, with no C# source files | 20 |
| Engine modules covered by the earlier reference-source survey | 38 |
| Catalog marks package deprecated; excluded | 61 |
| Native compiler/SDK/sysroot distributions; excluded | 8 |
| Source unavailable from the registry | 4 |

The four unavailable packages returned HTTP 401: `com.unity.platformtoolkit.gamekit`,
`com.unity.platformtoolkit.gdk`, `com.unity.platformtoolkit.playgamesservices`, and
`com.unity.platformtoolkit.steam`. No source coverage is claimed for them.

Sources come from the installed Editor's `BuiltInPackages` directories, its
bundled package archives, and Unity's public package registry. Registry archives
use the catalog's pinned version, or its explicit minimum when no version is
specified; they do not track the latest release. Archive SHA-1 values were
checked against registry metadata when supplied, and SHA-256 hashes are recorded.
This survey covers this catalog snapshot, rather than every historical package
or version available through UPM.

The DOTS packages `com.unity.entities`, `com.unity.physics`, `com.unity.netcode`,
`com.unity.entities.graphics`, and `com.unity.collections` are all **6.5.0** in
this installed Editor. Other examples include Input System **1.20.0**, Splines
**2.9.0**, Addressables **2.9.1**, Cinemachine **2.10.7**, AR Foundation **6.5.0**,
and URP/HDRP **17.5.0**. The coverage CSV records every surveyed package version.

The bundled Mathematics **1.4.0** package has two C# files and adds no qualifying
structs to this result. Its ordinary math types are already present in the
Editor's `UnityEngine.MathematicsModule`; package sources do not establish layout
compatibility with separately distributed Mathematics assemblies.

## Struct results

| Public composite outcome | Count |
|---|---:|
| Unmanaged inferred-runtime types, without a known pointer path | 478 |
| Unmanaged runtime types, with a known pointer path | 262 |
| Unmanaged Editor/test/sample/documentation/code-generation types | 137 |
| Managed | 387 |
| Unresolved runtime types | 65 |
| Unresolved outside runtime scope | 85 |

All rows exclude non-public, generic, ref-like, empty, and single-field types.
Field counts refer to physical instance storage, including private/backing
fields; fixed buffers are inline storage. No zero-byte managed size is assumed.

Runtime examples include `Unity.Entities.Entity`,
`Unity.Transforms.LocalTransform`, `Unity.Physics.PhysicsMass`,
`Unity.Physics.PhysicsVelocity`, `Unity.Physics.CollisionFilter`,
`Unity.Physics.RaycastHit`, and `Unity.Physics.ColliderCastHit`.

Scope comes from the nearest assembly definition and source path. Test, sample,
documentation, Editor, and code-generation sources remain searchable in the raw
inventory but are excluded from the runtime candidate CSV. GUID assembly
references are retained as `asmref:GUID:...` rather than guessed assembly names.
The pointer-path check follows source fields, including generic substitutions,
and the previous engine source inventory. An empty path is a discovery result,
not a target-layout validation.

## Validation and reproducibility

The full-source pass found **11,018 struct types** and no unparsed declarations.
A separate pass over **11,759 production C# files** found **5,630 struct types**.
Its runtime declarations replace matching broad-pass entries during refinement:
tests and generator examples define stubs with real API names, including
`Unity.Entities.Entity`, and must not contribute fields to the runtime types.
Other raw entries retain their original classifications.

The reference set excludes root `UnityEngine.dll` and `UnityEditor.dll` because
their type definitions duplicate the concrete modular assemblies in this Editor.
Framework references use its actual Linux Mono runtime assemblies. The scanner
still exposes all preprocessor branches conservatively; conflicting layouts,
unsupported syntax after branch exposure, and unresolved field types remain
explicitly unresolved. These sources were not compiled into a Unity player, and
no optional-package size/alignment measurements were performed.

All **20 real CLI integration tests passed**. Reconciliation also checks source
counts, package membership and versions, public/field-count filters, pointer
paths, scope selection and the preserved 373-entry engine-only snapshot. The
later accepted package additions were reconciled against the 664-entry target.

Research artifacts and helpers are retained under
`tmp/unity-package-catalog-20261021`:

- `collect_packages.py`: stage package sources and managed dependency DLLs.
- `packages.json`: complete package provenance and acquisition outcomes.
- `editor-package-catalog.json`: exact catalog, SHA-256
  `dee46515b4623e19593ea2c8af7c2a867aa66ff43f6b9034e78cf19ba6ff5f42`.
- `references/` and `reference-policy.json`: selected assembly references.
- `all-structs/` and `production-structs/`: raw scanner output and source hashes.
- `production-source-selection.json`: exact production source selection.
- `summarize.py` and `summary.json`: review CSV generation and reconciliation.

From the retained source/reference trees, reproduce the two scanner passes:

```sh
dotnet run --project tools/UnityStructCatalog -- \
  --source tmp/unity-package-catalog-20261021/source \
  --output tmp/unity-package-catalog-20261021/all-structs \
  --framework /home/houtamelo/Unity/Hub/Editor/6000.5.6f1/Editor/Data/MonoBleedingEdge/lib/mono/unityjit-linux \
  --reference tmp/unity-package-catalog-20261021/references
dotnet run --project tools/UnityStructCatalog -- \
  --source tmp/unity-package-catalog-20261021/production-source \
  --output tmp/unity-package-catalog-20261021/production-structs \
  --framework /home/houtamelo/Unity/Hub/Editor/6000.5.6f1/Editor/Data/MonoBleedingEdge/lib/mono/unityjit-linux \
  --reference tmp/unity-package-catalog-20261021/references
python3 tmp/unity-package-catalog-20261021/summarize.py
dotnet test tests/UnityStructCatalog.Tests/UnityStructCatalog.Tests.csproj
```
