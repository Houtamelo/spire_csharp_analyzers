# Generated reference-storage nullability coverage

Regression tests: [GeneratedStringStorageNullabilityTests.cs](../../../Unity/GeneratedStringStorageNullabilityTests.cs).

The reported Unity warning was `CS8604` in generated
`EqualityComparer<string>.Equals(string x, string y)` calls. Authored record
variants sharing storage between `string` and `string?` also produced payload
constructor warnings. Storage annotations must merge both method and record
parameters, while reads retain the active payload's declared nullability.

| Test | Cases | Assertions |
|---|---|---|
| `AuthoredRecordStringFields_AreWarningFreeWithPortableReferences` | Typed, Auto, Shared | Real generator with host BCL and Unity metadata; no compilation errors or generated warnings; public string annotations preserved. |
| `AuthoredRecordStringFields_PreserveAnnotationsInEitherDeclarationOrder` | Nullable first, nonnullable first | Typed storage handles both record declaration orders without warnings or public annotation changes. |
| `AuthoredRecordStringFields_AreWarningFreeWithUnityBclReferences` | Both declaration orders | Compile against Unity's actual BCL; no errors or generated warnings; public annotations preserved; comparer parameters verify the intended metadata boundary. |
| `FactoryStringFields_AreWarningFreeWithUnityReferences` | Static method variants | Nullable and nonnullable factory parameters share storage without warnings under Unity's BCL. |
| `MixedFactoryAndAuthoredRecordStringFields_AreWarningFree` | Static factory plus record variants | Both declaration forms contribute storage annotations; no generated warnings under Unity's BCL. |
| `NestedGenericRecordFields_AreWarningFreeWithUnityBclReferences` | `List<string>` and `List<string?>` | Nested annotation merging and record-to-union transfers compile without warnings. |
| `AuthoredRecordStringFields_PreserveNullEqualityAndPayloadOperators` | Optional, Required, IrisShape, Penis | Compile, emit, load, and execute: null/default equality, equal hashes, different keys/discriminators, union/payload equality, and operators in both directions. |

The 11 cases use real Roslyn compilations. Warning assertions inspect generated
syntax trees; compilation errors are rejected regardless of location.
The normal CoreCLR harness uses host framework references, so the explicit
Unity-BCL cases instead load `UnityReferenceAssemblies/unity-4.8-api` and
`Facades`, UnityEngine metadata, and the Spire runtime. They do not substitute
mock annotations or suppress `CS8604`.

Validation on Unity 6000.5.6f1's bundled Roslyn 4.10 compiler recompiled the
affected game assembly into temporary outputs: the baseline had 12 generated
`CS8604` warnings (3 constructors, 9 equality); the fix had zero generated
warnings and no errors. User-authored diagnostics remain outside this change.

Existing generator snapshots and equality/serialization runtime tests remain
in the full generator suite. No snapshot updates are required. These checks
do not claim Unity Player or IL2CPP execution.
