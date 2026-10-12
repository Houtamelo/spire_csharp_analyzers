# Commercial readiness review

Reviewed October 11, 2026, on `unity-6000`, starting at `8717f62`.

Spire has a credible technical foundation for a paid Unity tool, but the current
installation bundle is not ready for store submission. The main remaining work
is customer delivery, support scope, and release acceptance. This review also
found a genuine unchanged-save bug for Unity value types and repaired it.

The review covered package construction and its tests, generated serialization
and drawer code, public layout guidance, the URP showcase, publication automation,
and current Unity submission requirements. It did not repeat the competitor
survey, validate other operating systems, or establish market demand.

An additional isolated Roslyn roundtrip reproduced a source-name collision:
ordinary `Left.Result` and `Right.Result` produced duplicate `Result.g.cs`,
`CS8785`, and four downstream `CS8795` errors. Distinct names compiled cleanly.
The qualified-source naming correction and seven compile/execute regressions
now cover namespaces, nesting, generic arities, and Unicode names.

## Findings that still need work

| Priority | Finding | Evidence and next action |
|---|---|---|
| Before submission | The customer bundle omits the demonstration and full guides. | `PackageWorkflow.BuildAsync` exports four DLLs, metadata, LICENSE, and a short README. `examples/UnityShowcase` and `docs/unity.md` remain repository-only. Build a self-contained customer distribution with offline setup, troubleshooting, migration limits, and importable examples. |
| Before submission | Source distribution and notices need a deliberate release decision. | The package contains no C# source or Third-Party Notices file. Build projects use PolySharp, whose generated contributions need review along with the shipped DLLs. Determine source delivery and audit the actual included third-party material; the MIT license is unchanged. |
| Before submission | Unity and platform claims need a bounded support policy. | Native serialization is validated on Linux x64, Unity 6000.5.6f1. The separately reported 6000.6.5f1 generic-struct callback crash reproduces without Spire. The manifest cannot express a maximum Editor version. Keep this exception visible and validate Windows/macOS and other player targets before advertising them. |
| Before submission | Customer upgrade acceptance is missing. | Package tests verify deterministic repeated export and protect unrelated files; native runners install fresh fixtures. They do not exercise an older released bundle upgrading through Package Manager while preserving authored assets. Add that acceptance case against the eventual release bundle. |
| Before submission | The inherited NuGet tag workflow has no branch separation. | `.github/workflows/publish.yml` runs for every `v*` tag; `check-tag-version.sh` checks only numeric versions. Both branches still use 4.7.1. Choose Unity release tags/versioning and prevent a Unity tag from publishing the separate NuGet product. |
| Product improvement | Managed-reference serialization does not provide subtype authoring. | The drawer supplies a union-variant picker and delegates variant fields to ordinary Unity `PropertyField`s. The showcase creates Effect subclasses in code. Users cannot rely on Spire to construct or replace arbitrary managed-reference subtypes in the Inspector. Decide whether to add that workflow or disclose the C# authoring requirement. |
| Product improvement | Performance evidence is primarily desktop .NET evidence. | BenchmarkDotNet measurements in README are explicitly .NET 11 results. The Unity suites establish correctness, not comparative allocation/time results for player serialization, cloning, or Inspector editing. Measure representative Mono/IL2CPP workloads before making Unity performance comparisons. |
| Product improvement | Public API documentation is not available as DLL tooltips. | The packager exports no XML documentation, and the runtime project does not enable documentation output. Complete the public API docs and ship their XML alongside the runtime DLL. |
| Needs focused reproduction | Numeric fallback coverage is narrower than the UI type checks. | `ReadSerializedPropertyValue` reads integers/floats through `intValue`/`floatValue`; its setter accepts only int/long for integer properties, while `IsSupportedChangeProperty` also accepts unsigned types. Rendered long/double tests pass, but unsigned and fallback/multi-object paths need a native probe. These helper limitations are established by code; a customer-facing failure was not reproduced in this review. |

Unity's current [submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines)
require accurate limitations, comprehensive documentation, third-party notices,
and AI disclosure. They also raise unsafe-code and script-readability review
questions; seek clarification for `UnsafeOverlap` and compiled distribution.
The publisher must supply maintained contact information. The `unityRelease`
manifest requirement is now fixed. Fast Enter Play Mode with domain reload off
becomes mandatory for 6.6 submissions; the current lifecycle tests explicitly
exercise reload rather than certifying that setting.

The support boundary is a commercial concern as well as a test-matrix gap.
Unity's [6.5 announcement](https://discussions.unity.com/t/unity-6-5-is-now-available/1723176)
now marks that release end of life. Its
[release support page](https://unity.com/releases/unity-6/support) lists 6.3 as the
current LTS, supported through December 2027. Spire has not been validated there.
Prioritize a maintained Editor baseline: investigate 6.3 compatibility or verify
a fixed 6.6 patch before advertising maintained native serialization support.
The engine-only reproduction explains the fault, but customers still experience
the compatibility limit.

## Repairs made during this review

- Corrected snapshot equality for Unity value types. Unity's assemblies contain
  both native reference objects and value types; equal separately boxed Vector2,
  Vector3, Quaternion, Color, Rect, and Bounds must compare by value. Unity object
  references retain identity comparison. The existing showcase persistence test
  reproduced changed managed-reference IDs on an unchanged Move command before
  the fix.
- Corrected nullable drawer helper signatures and added explicit guards rather
  than suppressing generated warnings. Added a real C# 9/UnityEditor compilation
  regression check. Replaced obsolete `PreventDefault()` with the equivalent
  public focus-controller operation while preserving immediate propagation
  stopping, following [Unity's implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/Events/EventBase.cs#L316-L333).
- Added `unityRelease: "6f1"`; narrowed the package description to verified Linux
  x64 support and put the Rider requirement and 6.6 engine exception in the
  distributed README.
- Replaced eleven placeholder diagnostic help links with the published matching
  rule pages. Added real Roslyn flagged/control checks and descriptor/doc checks,
  including the copied documentation resource contract for Editor Mono.
- Enabled nullable annotations in the sample union declarations. Corrected
  inaccurate layout/generic/allocation guidance and clarified that editing
  Unity-generated `.csproj` files does not configure Unity's compilation.
- Made the URP Play Mode test explicitly submit a camera render request.
  Batch-mode coroutine frames alone had not constructed a render pipeline. The
  test retains its active-URP and gameplay assertions.
- Qualified source filenames for all union strategies, including containing
  type generic arity. Serializable and ordinary unions can share short names in
  separate scopes. Filename escaping preserves Unicode identity. Generated C#
  APIs and payload type identities are unchanged.

## Validation and limits

The final .NET suite passes 2,255 tests: 826 analyzer, 904 generator, 339 pattern
analysis, 129 behavioral, and 57 tooling checks. The new source-hint tests first
reproduced four scope/arity failures, then an escaping collision; all seven now
compile, emit, and invoke their generated factories.

On Unity 6000.5.6f1, Linux x64, under Xvfb:

- The URP showcase passes 13/13; its scene and preset remain byte-identical.
- The rendered native Inspector fixture passes 120 assertions, with no failures
  and three documented observations. Generated source has no compiler warnings;
  the internal test fixture still has its own nullable warnings.
- The complete Editor Mono harness passes 2,279 checks and matches all 2,140
  portable identities, including the seven source-hint cases.
- The complete IL2CPP/High fixture builds and runs successfully. All required
  main, stability, and managed-reference assertions pass, including graph
  collection, shared reconstruction markers, and struct-copy isolation. The
  final rebuilt bundle passes the same complete fixture.

Logs and machine-readable reports are under `tmp/asset-review-*`, including
`asset-review-complete-dotnet-results`, `asset-review-native-editor`,
`asset-review-showcase-results.xml`, `asset-review-complete-unity-harness`, and
`asset-review-il2cpp-player.json`. `asset-review-validation.json` verifies the
counts, native results, unchanged authored assets, and identical package DLLs
across the tested projects. The installable bundle was refreshed at
`tmp/unity-package/com.houtamelo.spire`.

Earlier evidence remains recorded in [the Unity guide](../unity.md): the complete
Editor Mono compiler harness, eight-assembly acceptance, Rider quick-fix probe,
and High-stripping IL2CPP serialization fixture. Those checks establish their
specific environments; they do not establish additional supported platforms.

The two engine repros are separate from current Spire defects:
[6.6 generic callbacks](../issues/unity-6.6-generic-struct-serialization-callback.md)
and [invalid embedded MonoScript loading](../issues/unity-il2cpp-opaque-managed-reference-slots.md).
The latter was an incorrectly registered fixture component and was repaired
before this review. Neither report makes the outstanding customer-delivery
work disappear.

Recommended next pass: prepare the customer bundle, then run clean install,
upgrade, Inspector/Rider, and player acceptance against that exact bundle. Keep
the existing schema-change limits explicit; this review does not propose
expanding the approved one-alias migration design.
