# Asset Store feasibility

Updated October 11, 2026. Market comparisons remain the October 8 research
snapshot. The October 10 review covered implementation, saved test reports,
package contents, and publishing requirements; this update incorporates the
Unity 6.5 script-registration reduction and corrected player validation.
The [October 11 commercial readiness review](reviews/2026-10-11-commercial-readiness.md)
adds a clean URP showcase roundtrip, delivery gaps, and follow-up repairs.

Spire is a plausible paid specialist tool: generated unions, compiler enforcement,
Unity serialization, and Inspector authoring form a distinct offering. Technical
feasibility is demonstrated on Unity 6000.5.6f1, Linux, with Mono and IL2CPP.
The original risks from variant reordering and renaming, and the payload-ID churn,
now have implemented, tested solutions. The remaining release work concerns the
advertised support matrix, customer distribution, and store review. Public market
evidence does not establish demand or forecast revenue.

Public listings and publisher documentation were examined. No direct equivalent
surfaced in the searches. This was not an exhaustive catalog audit, and competing
packages were not purchased or tested. Browser automation had no available browser;
research used public web search and page retrieval. Some listings exposed prices and
metadata without their full descriptions, limiting feature comparisons.

## Comparable packages

Prices are the displayed USD prices, before tax, on October 8; this update did not
repeat the competitor survey. Ratings are the counts displayed beside the product
title, not favorite counts or sales.

| Package | Price | Public ratings | Advertised focus and overlap |
|---|---:|---:|---|
| [Code Hygienist Pro](https://assetstore.unity.com/packages/tools/utilities/code-hygienist-pro-static-analysis-auto-fix-376676) | $29.99 | Not enough ratings | Static analysis and automatic fixes; closest analyzer comparison. Detailed rules and implementation were not verified. |
| [ConstSafe](https://assetstore.unity.com/packages/tools/utilities/constsafe-376244) | $19.99 | Not enough ratings | Type-safe generated constants; similar compiler-feedback proposition. |
| [T4 Code Generation](https://assetstore.unity.com/packages/tools/utilities/t4-code-generation-63294) | $9.99 | 4 | Template-based code generation; reducing boilerplate. |
| [Serializable Types](https://assetstore.unity.com/packages/tools/utilities/serializable-types-209170) | $15.99 | 4 | Serialization types and Inspector utilities; adjacent data authoring. Detailed feature coverage was not verified. |
| [Odin Inspector and Serializer](https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041) | $55.00 | 791 | Broad Inspector and serialization tooling; attribute-driven ergonomics with substantially broader scope. |
| [Serialize Interfaces!](https://assetstore.unity.com/packages/tools/utilities/serialize-interfaces-187505) | Free | 26 | Interface serialization in the Inspector; an alternative for some authoring needs. |
| [Csharp Wizard](https://assetstore.unity.com/packages/tools/utilities/csharp-wizard-104887) | Free | 8 | Script scaffolding; basic generation has free competition. |

ConstSafe's [publisher announcement](https://discussions.unity.com/t/released-constsafe-type-safe-unity-constants-generator/1722099)
describes replacing hardcoded tags, layers, scenes, Resources paths, animator
parameters, and other strings with generated C# references. Its generated files
have no runtime dependency on the tool.

The smaller paid packages provide pricing comparisons, but their limited public
ratings do not establish a large market. Odin demonstrates interest in broadly
useful developer tooling; its audience and scope cannot be used to forecast Spire
sales. Neither ratings nor favorites reveal unit sales.

Free alternatives outside the store also affect pricing:

- [Dunet](https://github.com/domn1995/dunet): MIT-licensed C# union source generator.
- [Microsoft.Unity.Analyzers](https://github.com/microsoft/Microsoft.Unity.Analyzers):
  Unity-specific diagnostics, distributed with Microsoft's Unity IDE tooling and
  through NuGet.
- [SerializeReferenceExtensions](https://github.com/mackysoft/Unity-SerializeReferenceExtensions):
  Inspector subtype selection and editing for managed-reference fields.
- [TypeSafe](https://stompyrobot.uk/tools/typesafe/): generated typed resource,
  scene, layer, and tag references, available through GitHub.

Compatibility of these alternatives with Spire's supported Editor was not tested.

## Product proposition and price

Recommended proposition: define Inspector-editable union types and have the
compiler catch missing cases, invalid initialization, and unsafe field access.
This communicates an observable workflow benefit. Roslyn implementation details
and layout choices can follow in technical documentation. Native Unity
serialization and generated drawers require no Odin or other third-party
serializer. `[Serializable]` enables union serialization;
`[UnitySerializeReference]` opts individual variant parameters into managed
references when nulls, polymorphism, or reference sharing matter.

A useful demonstration would add a variant to a gameplay command, show the
incomplete switches being diagnosed, apply a Rider fix, and edit the active variant
in a prefab. Examples for results, gameplay commands, and character states would
make the product easier to evaluate.

$29.99 is an initial pricing hypothesis based on adjacent $10–$30 tools, not a
measured optimum. Under Unity's standard 70% publisher share, a full-price sale
contributes approximately $20.99 before deductions and taxes. One hundred such
sales would contribute approximately $2,099 before those deductions; this is an
illustration, not a sales forecast. Support and ongoing compatibility work must fit
within the resulting economics. See the [provider agreement, section 4.3](https://unity.com/legal/provider).

Spire's current [license](../LICENSE) is MIT. Selling a distribution is permitted by
that license, but does not remove its permissions. Unity's [EULA, section 2.7](https://unity.com/legal/as-terms)
preserves applicable open-source terms when they conflict with the store EULA.
If the Unity implementation remains freely available, the paid offering primarily
sells convenient distribution, maintenance, examples, and support. Do not assume
the store's usual seat licensing creates exclusivity over MIT-covered components.

## Implemented and verified

October 27 update: native Burst material jobs and real DOTS Firefighters state
transitions now run in Spire-integrated sibling projects. See the
[demo integration evidence](reviews/2026-10-27-spire-demo-integration.md).
The current representation uses per-variant payloads and an explicit unmanaged
overlay; the five public Layout strategies mentioned in older validation below
have been removed. Those earlier results retain their historical scope.

The Unity build remains on `unity-6000`, separate from the normal NuGet build on
`main`. The compiler extensions now use a Roslyn 4.3 baseline; the shipping
manifest still targets Unity 6000.5, with Rider as the supported IDE. Native evidence
is from Unity 6000.5.6f1 on Linux x64. A 6000.6.5f1 recheck passes the complete
Editor Mono harness and IL2CPP behavioral suite, but native serialization crashes
in the Editor before player builds finish. Other platforms are not covered.

| Concern | Current status |
|---|---|
| IL2CPP execution | All 129 original runtime behavioral assertions pass in a Linux IL2CPP player, covering all five layouts, generics, JSON converters, and closure inliners. |
| Production stripping | The package passes the complete serialization fixture as one IL2CPP/High player on 6000.5.6f1, without a consumer `link.xml`. The former loading failure was resolved by correcting the fixture component's script registration. The runtime package retains Unity's required `Type.GetType(string)` dependency. |
| Variant reordering | Resolved for valid saved payloads: the payload's concrete type identifies the variant; runtime `Kind` is not serialized. |
| Variant renaming | Implemented through one `[Variant(FormerlySerializedAs = "OldName")]` alias. A former-name payload restores the current variant; the next save writes the current-name payload. |
| Unchanged-save diffs | Immutable snapshot reuse and isolated Inspector previews preserve complete YAML and recursive managed-reference IDs across repeated saves and reloads. Serialization adds one reference field per union. |
| Nulls and polymorphic fields | `[UnitySerializeReference]` preserves nulls, runtime subclasses, shared references, and cyclic helper graphs within a Unity host, including private managed-reference fields and nested unions. |
| Inspector authoring | Generated UI Toolkit and IMGUI drawers are exercised with variant/field edits, multi-object selection, Undo/Redo, prefab overrides, script reload, and Play Mode. |
| asmdef and Rider integration | Eight consuming assemblies pass generated-API and diagnostic checks, including reference failures and repairs. Rider 2026.2.3.1 successfully applies a real quick fix, followed by Unity recompilation and runtime probes. |
| Player snapshot lifetime | Deserialization releases the payload graph and leaves a shared empty marker in the union's sole serialization reference field. Runtime cloning can retain a reusable snapshot on its source until the next reconstruction; this is a documented lifetime boundary. |

Verification totals are separate because each environment establishes different
behavior:

| Environment | Latest verified result |
|---|---|
| .NET repository suite | 2,304 passed on October 13, 2026: 826 analyzer, 918 generator, 339 PatternAnalysis, 129 behavioral, and 92 tooling checks. |
| Complete Unity Editor Mono harness | October 13 recheck on 6000.5.6f1: 2,328 passed, with all 2,189 portable identities matched and all 129 original behavioral assertions executed. The earlier 2,266-case harness also passed on 6000.6.5f1. |
| Native Editor integration | October 11 on 6000.5.6f1: 120 assertions passed with three observations, including custom enum display labels, flags, unavailable choices, and exact numeric Undo/Redo. On 6000.6.5f1, the Editor crashes before its final report. |
| Linux IL2CPP behavioral runner | 137 checks passed on both Editors: 129 original assertions, six adapter checks, and two backend capability probes. This runner uses test-specific linker roots. |
| Standalone Linux Mono and High-stripping IL2CPP serialization | On 6000.5.6f1, Mono Full, both isolated IL2CPP profiles, and the corrected combined IL2CPP Full fixture pass. On 6000.6.5f1, every attempted serialization profile crashes in the Editor before a player is produced. The production fixture has no consumer linker descriptor. |

The compiler/analyzer harness runs in Editor Mono. Consumer-generated code runs
under IL2CPP; dynamic compilation/emission and SDK subprocess tests do not execute
unchanged in an AOT player. SDK preparation uses the C# tooling; there is no Python
requirement. Installing the built package does not require the .NET SDK.

Ordinary `Overlap` now initializes readonly storage in constructors. Its focused
Unity 6.3 Mono and IL2CPP/High fixtures pass without an added Unsafe DLL or consumer
`link.xml`; see [the validation report](reviews/2026-10-13-overlap-constructor-results.md).
`UnsafeOverlap` retains its separate runtime dependency.

The Unity 6.5 native loading failure has a small Unity-only MRP involving an
embedded `MonoScript` and one null managed-reference field. The fixture component
was declared in a different source file; moving it into `StabilityPlayerHost.cs`
and correcting its scene script reference makes the frozen fixture pass with
identical serialized values and all four package DLLs unchanged. A fresh complete
fixture also passes every main, stability, and managed-reference assertion in one
IL2CPP/High player. This removes that fixture failure from the current package
release blockers. See the
[native-loading evidence](issues/unity-il2cpp-opaque-managed-reference-slots.md).
The [6.6 recheck](issues/unity-il2cpp-opaque-managed-reference-slots.md#unity-66-recheck-on-october-10)
encounters a separate earlier Editor serialization crash; 6.6 native serialization
support remains unverified.
The [Unity-only callback reproducer](issues/unity-6.6-generic-struct-serialization-callback.md)
now establishes a 6.6 regression without Spire installed, affecting generic struct
serialization callbacks. Its single-file MRP needs only a null managed-reference
field and a logging callback. A safe package workaround has not been established.
The [November 2 current-package recheck](reviews/2026-11-02-unity-6.6-serialization-recheck.md)
narrows this boundary: 126 native Editor write checks and 87 fresh-process read
checks pass on 6000.6.5f1, while a generic union instantiated with `string` still
crashes. Complete 6.6 Player/Inspector support remains unverified.
See [Unity support and validation](unity.md) for reproducible workflows and IDE
acceptance boundaries.

## Serialization policy to publish

The alias mechanism addresses the original variant-rename concern. Broader
schema changes remain outside automatic migration. Document these limits before
customers depend on the saved representation:

- One previous name per variant: resave affected assets before replacing that
  alias with another rename. Invalid or ambiguous aliases produce `SPIRE_DU017`.
- Aliases do not migrate variant field names/types, removed variants, union or
  containing-type identities, namespaces, assemblies, or changes between inline
  and managed-reference field encodings.
- Missing or unrecognized payloads warn and default to the first declared
  variant. Unsupported field types remain Unity's responsibility; Spire does not
  guarantee persistence merely because a field is generated.
- Inline fields retain Unity's native normalization. Fresh null inline helpers
  can normalize on saving; use `[UnitySerializeReference]` when null values are
  significant. Unchanged saves are tested against saved/reloaded data, with
  additional managed-reference null tests.

Current struct unions support serialization, but the generated managed cache
prevents using that serialized representation as unmanaged Burst/ECS data. Users
must manually declare separate serializable authoring and unmanaged runtime
unions and convert between them. The current overlay requires neither unsafe
code nor an extra Unsafe DLL. Editor cache retention and player cloning-source
snapshots remain documented design limits. See [the Unity guide](unity.md#serialize-a-union)
and [payload stability evidence](unity-managed-reference-stability.md#implemented-behavior).

## Remaining preparation before submission

The [submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines),
rechecked October 9 and dated May 20, 2026, require comprehensive documentation,
accurate requirements/limitations, and third-party notices. Sections 1.6.a–b
require disclosure of AI-assisted functional content and the tools used;
Claude Code and Codex contributions are relevant here.

Section 1.1.g lists unsafe code among rejection grounds. `UnsafeOverlap` works in
the tested players, but store acceptance still requires clarification with Unity.
Section 5.2.e requires `unityRelease`; the local manifest now declares `6f1`
alongside `unity: "6000.5"`, matching the validated 6000.5.6f1 baseline.
Section 2.5.d concerns customer-readable/modifiable scripts: confirm the intended
compiled-DLL distribution and accompanying source materials with Unity rather
than assuming review acceptance.

The current [packager](../tools/Spire.Tool.Core/PackageWorkflow.cs) ships four Spire
DLLs, metadata, runtime XML documentation, MIT licensing, Third Party Notices,
offline customer guides, and an optional Package Manager showcase. It declares Unity module
dependencies and keeps compiler extensions out of player plugin imports;
compiler-host DLLs are not bundled. The sample preserves authored GUIDs and
requires URP/Test Framework packages installed separately. Clean import and
prerelease bundle replacement both pass native Package Manager acceptance and
15 showcase tests; see [customer delivery](reviews/2026-10-12-customer-delivery.md).

Unity [offers UPM publishing for tools and SDKs](https://assetstore.unity.com/publishing/upm-publishing),
including individual enrollment. Identity verification is required, with
additional organization checks where applicable. The existing local UPM layout
is a foundation, not a completed store submission.

Priorities for a commercial release:

1. Choose the advertised Unity-version support boundary. Native serialization is
   validated on 6000.5.6f1; the reported 6.6 generic-struct callback crash still
   prevents extending that claim to 6000.6.5f1. Unity now marks
   [6.5 end of life](https://discussions.unity.com/t/unity-6-5-is-now-available/1723176);
   investigate a maintained baseline, such as the
   [current 6.3 LTS](https://unity.com/releases/unity-6/support), before promising
   production support. The [6000.3.26f1 evaluation](issues/unity-6.3-roslyn-host-compatibility.md)
   now resolves the Roslyn mismatch and passes the native Inspector fixture
   and complete IL2CPP/High serialization player with an additional runtime
   Unsafe DLL. Runtime dependency and sample
   compatibility work remain before lowering the advertised minimum.
2. Choose the advertised support matrix. Validate Windows/macOS Editors and
   additional IL2CPP player targets before advertising them; Linux results alone
   do not establish those combinations.
3. Review the delivered offline guides and showcase as customer materials,
   including the implemented rename workflow and the schema limits above.
4. Resolve the unsafe-code and DLL/source review questions; complete store
   manifest metadata, AI disclosure, and publisher/support
   information.
5. Repeat acceptance checks against the final customer bundle in clean projects
   and prepare the store demonstration. Confirm pricing and support economics
   with prospective users; the comparable listings do not establish demand.

Recommendation: continue toward a focused paid release. The integrated Unity
workflow and variant-order/rename stability are implemented and tested for the
stated environment. IL2CPP behavioral execution and the complete High-stripping
serialization fixture pass on 6000.5.6f1 after correcting script registration.
The reported 6.6 callback crash remains an engine-version boundary. The
installation bundle now includes customer materials and verified delivery
workflows. Maintained-version support and submission preparation remain.
