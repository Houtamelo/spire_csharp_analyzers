# Changelog

## 6.1.2

- Fix CS8509/CS8524 suppression for exhaustive switch expressions passed to
  method or delegate calls, including null-conditional event invocations.
- Preserve compiler warnings for incomplete, guarded, and missing-null cases,
  with regression coverage for sibling and nested switches.

## 6.1.1

- Warn when readonly record-struct variants contain positional data that Unity
  cannot serialize (SPIRE030), with a fix to remove `readonly` across partial
  declarations. Fieldless variants remain exempt.
- Reject outer `record struct` union declarations with SPIRE_DU020 and offer
  to remove `record`, preserving nested record-struct payloads.
- Fix generated modifier ordering for serializable partial readonly variants.

## 6.1.0

- Generate union/union and union/payload equality and hashing from the variant
  tag and active logical fields, excluding serialization payloads and caches.
- Allow public nested `[Variant] record struct` payloads alongside method
  variants. Record-struct syntax requires C# 10 or newer.
- Add automatic serialization attributes for partial record variants,
  serialization warnings and fixes (SPIRE028), and a warning for authored
  positional members that affect reconstruction (SPIRE029).
- Preserve field captures when adding missing nullable-union switch arms,
  including a separate missing `null` case.

## 6.0.0

- Bundle `Houtamelo.Spire.Functional.dll` and its XML documentation alongside
  the core runtime. Add the offline Result and Option guide.
- Mark closure inliner attributes, interfaces, and generated APIs experimental.
  Supporting compilers require opting in by suppressing `SPIREEXP001`; older
  Unity compilers may ignore the annotation.
- Add self-contained `.unitypackage` export with fresh-import acceptance tests.

The new Functional runtime was exercised in Unity 6000.5.6f1 Editor Mono.
That check does not extend the existing player validation to the new API.

## 4.7.6 — release candidate

- Verified Unity 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1 on Linux and Windows
  x64, including Editor, Inspector, and Mono/IL2CPP player checks.
- Verified WebGL/IL2CPP runtime behavior in desktop Linux Chrome and Firefox
  using full exception support with stack traces.
- Added a render-pipeline-independent Quick Start sample and signed-tarball
  delivery checks.
- Lowered the declared minimum Unity version to 2022.3.62f3, matching the
  validated Unity 2022 baseline.

The optional URP Showcase requires Unity 6000.5.6f1 and separate dependencies. Unity 2022 native
serialization of generic unions is unsupported. Unity 6000.6.5f1 has a known
generic-reference Editor crash. See the included README and serialization guide.

This is a release-candidate record, not an announcement of Store publication.
