# Generated accessor assertions and field formatting

User requests from `docs/PENDING.md`, recorded before implementation.

- Put short field attributes on the field declaration's line, including
  `SerializeField`, `NonSerialized`, and `SerializeReference`. Preserve multiple
  field attributes and their serialization semantics.
- Add the globally anchored alias
  `using UnitySerializationPayloadBase = global::Houtamelo.Spire.UnitySerializationPayloadBase;`
  and use it for `_serializedPayload`. Retain fully qualified spelling when a
  generated/user declaration would shadow the alias.
- Generate readonly getter bodies for all variant accessors. Assert
  `this._kind == Kind.Variant` before reading payload storage, using the requested
  message: `Accessing the \`Variant\` variant's fields is only allowed when
  \`kind == Kind.Variant\`. Actual kind: {this._kind}`.

Unity-serialized unions use `UnityEngine.Debug.Assert`; standalone unions use
`System.Diagnostics.Debug.Assert`. Preserve their existing conditional-call
semantics (`UNITY_ASSERTIONS` / `DEBUG`), including elimination of interpolated
messages from release code. Assertions diagnose inactive-variant reads; they do
not replace the existing analyzer checks or change storage layout.

Validate real generated assemblies for active access, assertion failure, and
release-call elimination. Cover fieldless, managed/unmanaged, generic, nested,
keyword/collision cases. Keep K&R braces and namespace indentation unchanged.

## Validation

- The new native regression failed against the preceding package because an
  inactive accessor emitted no assertion; it passes after rebuilding (9 checks).
- Generator suite: 637 passed. Coverage includes exact accessor diagnostics,
  generic/nested/keyword declarations, alias collisions, and inline attributes.
- Standalone debug failure executes in a bounded child process with core dumps
  disabled. Active access succeeds; inactive access produces the assertion.
- Emitted IL confirms assertions and messages are omitted without `DEBUG` or
  `UNITY_ASSERTIONS`, including real Unity references with C# 9.
- Runtime behavioral suite: 73 passed.
- Release build: zero warnings and errors.
- Unity 6000.5.6f1 Editor serialization: write 109 and fresh-process read 70 checks
  passed, with no unexpected variant-access assertions.

Exported the rebuilt package to the existing UnityShowcase local package path.
Release, export, and native-fixture DLLs match byte for byte; the authored
Showcase preset remains unchanged. Reports are in `tmp/pending-20261025-results`.

Final analyzer SHA256:
`ef093777b1efb4fcae4b0d28dc6818c2fcda156fae550fc7411f45a3acb4a2b1`.

## Follow-up: function spacing

User request, recorded before implementation: ensure exactly one blank line
between functions that take more than one line; adjacent one-line functions
such as `int foo() => 5;` do not need a blank line.

Apply this to adjacent generated methods, constructors, and getter properties,
including the new multiline variant accessors. Separate a pair when either
member is multiline; one-line members may stay adjacent. Place spacing before
member attributes and preserve conditional-compilation directives. Keep the
custom source builder and author template spacing directly; no formatter pass.

Implemented with explicit member separators and a source-builder helper that
normalizes the boundary to one blank line. The spacing regression checks actual
generated syntax, including attributes and nullable-warning pragma regions.
One-line variant flags remain adjacent. All 640 generator tests and 73 runtime
behavioral tests pass; the Release build has zero warnings/errors. Rebuilt the
local UnityShowcase package. Reports: `tmp/function-spacing-results`.
