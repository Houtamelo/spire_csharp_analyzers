# Generated code readability requests

User requests recorded before implementation on 2026-10-24. The user finished
the batch and authorized validation and delivery. Implementation, validation,
and the local UnityShowcase package refresh for that batch are complete.
The follow-up requests below supersede its payload-alias policy.

## Earlier requests in this batch

- Apply `[EnforceExhaustiveness]` to generated discriminated-union `Kind` enums.
- Split factory constructor arguments into readable storage locals. Keep
  `Kind.Variant` inline, as in the user's example. Avoid local-name collisions.
- Rename the unmanaged overlay `UnmanagedData` to `UnmanagedFields`.
- Rename overlay members from `__VariantName` to `__variantName` (camelCase).
- Rename variant helpers from `{VariantName}UnmanagedFields` to
  `UnmanagedFields{VariantName}`; the latest request below supersedes this name.
- Explain generated formatting: `SourceBuilder` manages indentation; emitters
  write the source statements without a formatter pass over generated output.

## Latest requests

1. Replace repeated globally qualified framework/Spire references with using
   aliases whose targets remain globally qualified. Requested shortcuts:

   ```csharp
   using SpireSerialization = global::Houtamelo.Spire.UnitySerializationBridge;
   using SerializeField = global::UnityEngine.SerializeField;
   using Serializable = global::System.SerializableAttribute;
   using Preserve = global::UnityEngine.Scripting.Preserve;
   using NonSerialized = global::System.NonSerialized;
   using StructLayout = global::System.Runtime.InteropServices.StructLayout;
   using LayoutKind = global::System.Runtime.InteropServices.LayoutKind;
   using ISerializationCallbackReceiver = global::UnityEngine.ISerializationCallbackReceiver;
   using EditorBrowsable = global::System.ComponentModel.EditorBrowsable;
   using EditorBrowsableState = global::System.ComponentModel.EditorBrowsableState;
   ```

   Alias targets must use the actual CLR type names, including `Attribute`
   suffixes where required. Preserve qualification where aliases cannot safely
   represent a type, including generic-dependent types.

   Attribute uses spell short aliases explicitly, for example `[@Serializable]`.
   This prevents CS1614 when C# also finds an imported `SerializableAttribute`;
   the requested using-alias names and their globally qualified targets remain.

2. Add a serialized-payload alias for each variant, for example:

   ```csharp
   using SerializedPayload_Idle = global::Houtamelo.Spire.Generated.SpireSerializedPayload<GameplayCommandIdle>;
   ```

   Account for namespaces, nesting, and former-variant payload aliases.

   **Subsequent user clarification, recorded before applying it:** do not alias
   generic payloads or any references involving potentially generic types. Leave
   those references globally qualified; do not introduce a namespace shortcut as
   a workaround. Fixed nongeneric framework types may still use aliases within
   a generic union. Concrete nongeneric payloads retain the requested closed
   `SerializedPayload_{VariantName}` aliases.

3. Remove the `__` prefix from functions declared in the library-owned static
   `UnitySerializationBridge` class, including
   `__SpireSerializedManagedReferenceValuesEqual`. Update all callers.

4. Rename the shared generated wrapper `__SpireSerializedPayload` to
   `SpireSerializedPayload`, updating all references and test expectations.

5. Omit explicit `private` keywords everywhere in generated code. Preserve
   accessibility semantics where `private` cannot simply be omitted (for example,
   `private protected`). This request concerns generated output, rather than an
   unrelated repository-wide source-style rewrite.

6. Rename `UnmanagedFields{VariantName}` to `UnmanagedFields_{VariantName}`.

7. Generate explicit implementations of both Unity serialization callbacks:

   ```csharp
   [EditorBrowsable(EditorBrowsableState.Never)]
   void ISerializationCallbackReceiver.OnBeforeSerialize()
       => this = this.__SpirePrepareForSerialization();
   ```

   Apply the same interface visibility to `OnAfterDeserialize`. Update reflection
   and direct-callback consumers to invoke the interface contract correctly.

8. Explain why `__SpirePrepareForSerialization()` is separate from
   `OnBeforeSerialize()`; determine its non-callback callers before considering
   inlining. This is a question, not authorization to remove required callers.

9. Explain why `__SpireCreateDefault` accepts `int` instead of the generated
   `Kind` enum. Inspect the shared editor/reflection contract before proposing
   a signature change. This is a question, not a request to change the signature.

10. Record every request in the filesystem before making implementation changes.

## Implementation consequences

- The wrapper rename requires updating the nine serialized type references in
  the authored UnityShowcase preset. Preserve all payload values and managed
  reference IDs; this is an authored sample update, not a compatibility layer.
  The asset was updated alongside the matching rebuilt package. A byte-level
  comparison against the pre-update asset confirms only those nine names changed.
- Keep `__SpirePrepareForSerialization` as the shared entry point for recursive
  bridge preparation and isolated Inspector previews, in addition to the Unity
  callback.
- Keep `__SpireCreateDefault(int)` as the shared drawer's reflection contract;
  the drawer uses an integer index without knowing the individual union's enum.

## Earlier batch validation

The current source edits and regression coverage implement this batch, including
the clarification to keep generic references globally qualified. Callback
consumers use the interface contract with constrained by-reference calls where
the original struct must be mutated. The shared preparation/default-creation
helpers retain their existing visibility and reflection contracts.

No tests, builds, or package refreshes ran during the user's hold. After the user
finished the batch, validation passed on 2026-10-24:

| Check | Result |
|---|---|
| Source generator tests, including snapshots and code fixes | 615 passed |
| Analyzer tests | 826 passed |
| Behavioral tests | 73 passed |
| Release package build | 0 warnings, 0 errors |
| Unity Editor serialization write / fresh read / recovery | 109 / 70 / 59 checks passed |
| Unity player asset preparation | 101 checks passed |
| Mono release player, High stripping | 54 checks passed |
| IL2CPP release player, High stripping | 54 checks passed |
| Native Inspector editing, focus, and Undo | 16 cases, 147 checks passed |

Native validation used Unity 6000.5.6f1 on Linux x64. Player builds had zero
errors and three fixture warnings each. Both players reported non-development
builds. Reports are under `tmp/readability-results` and
`tmp/unity-union-core-phase4`; earlier Editor phase reports are archived under
that project's `TestResults` directory.

The local package remains version 4.7.6, with all three package project versions
synchronized. `examples/UnityShowcase/Packages/manifest.json` references the
refreshed `tmp/unity-package/com.houtamelo.spire`. Source Release DLLs, exported
package DLLs, and the native fixture's installed DLLs match byte for byte.
The exported preset also matches the authored Showcase preset byte for byte.

Final analyzer SHA256:
`5f44e330e3ad44c2588ad7dffda1d3d69cfc66313a57b58e45259c3b20416f13`.
Final runtime SHA256:
`a929447002e095cc61b621c47593428c48f1bf767044501db7fc1717958c5b36`.

## Follow-up requests from docs/PENDING.md

Recorded before implementation; the user authorized handling the complete file.

- Generate `__SpireTryFromSerialized` as a type-pattern switch, retaining current
  and former payload handling and invalid/null fallback behavior.
- Use K&R braces in generated union sources, adapters, and shared Unity support
  templates. Keep the custom source builder; add no runtime formatter dependency.
- Mark pure struct instance properties/methods readonly where appropriate,
  including `kind` and `IsVariant` properties. Keep mutating serialization methods
  mutable and respect class/readonly-struct declaration rules.
- Remove the blank line immediately before generated factory returns.
- Declare the union before its generated sibling payload/helper types, within
  the same namespace and containing types. Companion JSON converter files also
  declare their attributed union partial before converter/factory types.
- Remove per-variant `SerializedPayload_Variant` using aliases. The user clarified
  that fixed closed generic types may use aliases, but types whose declarations
  support generics should not gain aliases merely because one particular
  declaration is nongeneric. Union-owned payload types fall into this category;
  spell their constructed wrapper references fully qualified in every case.

After validation, remove completed request sections from `docs/PENDING.md`.

The user added another request to `docs/PENDING.md` during implementation:
namespace contents must have no extra base indentation. Type bodies and nested
types keep their normal indentation. Apply this to generated union sources,
adapters, converters, and shared Unity support templates.

The new ref-struct regression also exposed `CS8343`: the emitted
`IDiscriminatedUnion<Kind>` interface is unsupported for ref structs by the
project's older C# compiler baseline. Ref structs now expose the same generated
members without that interface. Ordinary struct unions retain the interface.

## Follow-up validation

All seven requests are implemented. Generated templates are authored in the
requested style, without a formatter pass or new dependencies.

- Source generator suite: 627 passed, including compile/emit/load recovery tests,
  nested/generic declarations, JSON companion ordering, and output formatting.
- Runtime behavioral suite: 73 passed.
- Release build: zero warnings and errors.
- Unity 6000.5.6f1 Editor: write 109, fresh-process read 70, recovery 59 checks
  passed. The final recovery process compiled the namespace-formatting changes;
  write/read preceded the final whitespace and JSON declaration-order cleanup.
- Exported the rebuilt package to the local path already used by UnityShowcase.
  Release, export, and native-fixture DLLs match. The authored Showcase preset
  remains byte-for-byte unchanged.

Reports: `tmp/pending-results/`; build log: `tmp/pending-release-msbuild.log`.
Final analyzer SHA256:
`9b96efd31b121df782c44b77c8bcef03fa6228b238b54e5d72c88e88f3ba2262`.
