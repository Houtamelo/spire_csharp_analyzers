# Changelog

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
