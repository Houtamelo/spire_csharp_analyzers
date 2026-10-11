# Unity variant managed references

The API and managed-reference contracts below remain applicable. The recorded
October 9 validation predates the [one-reference serialization redesign](2026-10-10-unity-serialization-footprint.md).

Accepted API name, 2026-10-09: `[UnitySerializeReference]` in `Houtamelo.Spire`.
The marker targets variant parameters and has no Unity assembly dependency.

```cs
[Variant(FormerlySerializedAs = "OldValue")]
public static partial Result Value([UnitySerializeReference] Node? value);
```

For Unity-serializable unions, annotated parameters add Unity's
`[SerializeReference]` to the corresponding generated current-name and former-name
payload fields. Keep the existing explicit `[SerializeField]` annotation.
Unannotated fields keep inline serialization. Ordinary non-Unity generation
ignores the marker. Match the actual Spire attribute, including qualified and
aliased syntax; an unrelated same-name attribute must not enable the feature.

Unity owns field-type eligibility. Do not restore field-type diagnostics or
silently replace a reference annotation with inline serialization. Collection
annotations follow Unity's rule: managed references apply to elements, not the
array or list container.

Adding or removing the marker changes Unity's field encoding between inline and
managed-reference data. No automatic migration of existing inline field data is
provided; variant-name aliases do not migrate that encoding.

Managed-reference fields must preserve null, concrete runtime subclasses, and
shared object identity within one Unity serialization host. Preparing or reading
nested generated unions must retain those reference identities, including cyclic
helper graphs and private `[SerializeReference]` fields. Avoid cloning a managed
reference into a new detached helper on each save. Generated union cache updates
and hydration may traverse the managed graph; user callbacks remain Unity's
responsibility. Preserve independent union snapshots and existing inline-field
behavior.

Traversal state is shared through generated-union reentry for one synchronous
operation, then released on success or exception. Runtime bridge code does not
call Unity APIs or helper serialization callbacks.

Verification covers all five layouts, generic/interface/base-class declarations,
canonical and former-name payloads, null/default variant switching, reference
sharing, derived classes, arrays/lists, nested unions, cycles, Inspector
reconstruction and Undo, stable YAML/managed-reference IDs, and standalone Mono
and High-stripping IL2CPP. Use genuine generated compilation and native Unity
serialization, with no fake engine APIs. Preserve the previous uncommitted work
and user `.codex/config.toml` change. No version bump or commit was requested.

## Validation, 2026-10-09

Unity 6000.5.6f1, Linux x64; native runs use Xvfb.

| Environment | Result |
|---|---|
| .NET solution | 2,134 tests passed: 785 generator, 824 analyzer, 339 pattern, 129 behavioral, 57 tooling |
| Full Unity Editor Mono harness | 2,158 passed; all 2,019 portable case identities matched exactly and all original behavioral cases executed |
| Native Editor integration | 106 scenarios, including null/derived persistence, all-layout collections, private fields/cycles, initial-null YAML/RID stability, aliases, Inspector Undo/Redo, and independent snapshots |
| Standalone Linux Mono | Full scene and cloning passed; all managed-reference and previous stability checks true |
| Standalone Linux IL2CPP, High stripping | Fresh full scene and cloning passed; all managed-reference and previous stability checks true; no consumer `link.xml` |

The native players used byte-identical runtime DLLs. The IL2CPP confirmation
project is `tmp/unity-il2cpp-managed-reference-clean-confirmation`; its runtime
report is `tmp/managed-reference-il2cpp-confirmation-player.json`. Mono evidence
is `tmp/managed-reference-mono-final-player.json`. These are ignored artifacts.

An earlier disposable IL2CPP project failed with `level0` / `Position out of
bounds` before its report hook. Baseline-only and managed-reference null/shared/
cyclic isolation scenes passed. The complete fixture subsequently passed in the
fresh confirmation project with the same runtime DLL. This does not establish
the earlier failure's cause; do not attribute it to unsupported reference graphs
or treat its missing report as a pass. Logs remain in
`tmp/managed-reference-il2cpp-final-player.log` and the corresponding disposable
project. The successful complete player is the evidence reported above.
