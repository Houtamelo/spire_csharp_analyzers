- Unity 2020 compatibility is ruled out; document it publicly as "will not work."
  Unity 2021 cannot load the current Roslyn API baseline. Unity 2022 passes
  native serialization write/read checks for non-generic unions with the new
  concrete wrappers; generic unions remain unsupported by its serializer.
  All three listed versions pass the full Editor harness, applicable native
  serialization, rendered Inspector interaction, and actual Mono/IL2CPP players
  on Linux and Windows. The publisher selected 2022.3.62f3 as the package minimum
  on November 7, 2026. See the
  [current validation](reviews/2026-11-05-unity-2022-2023-current-validation.md)
  and [rendered follow-up](reviews/2026-11-05-rendered-inspector-validation.md)
  for exact versions, DLLs, and test scope.
- Windows 10 validation is complete on all three listed versions: 2,692 complete
  Editor cases per version, applicable native/player/Inspector gates, and Visual
  Studio Community live diagnostics, generated-symbol navigation, a representative
  Quick Action, and Unity recompilation. See the
  [Windows matrix](reviews/2026-11-06-windows-unity-version-matrix.md).
- Release WebGL/IL2CPP validation passes in Chrome and Firefox on Linux for all
  three listed versions: 100 original behavioral tests per browser, plus 59
  native core checks on Unity 2022 and 60 on Unity 2023/6000. Each browser/version
  also propagates an intentional assertion failure. These runs use High stripping
  and full exception support; the initial default-exception build passes 99/100
  behavioral tests. Software rendering establishes correctness, not GPU
  performance. Safari, mobile browsers, and WebGPU are unverified. See the
  [Web results and exception-setting limitation](unity.md#web-runtime).
- Unity serialization follow-up: the engine repro and preserved Spire probe pass
  on `7000.0.0a12` with CoreCLR, including a fresh-process asset read. The same
  inputs still crash on 6000.6.5f1 with Mono. Use a shared root cause with issue
  `25665` as the working assumption. The storefront states that the crash is
  fixed in Unity 7000, verified on `7000.0.0a12`. A `6000.6.x` backport is expected
  soon; its availability and timing remain unconfirmed.
  See the [Unity 7 comparison](reviews/2026-11-03-unity-7-serialization-recheck.md).
- Unity 7 Linux IL2CPP verification: all 100 original behavioral tests and 60
  native runtime/serialization checks pass with High stripping. The normal NUnit
  run passes all 107 cases without filtering after removal of an unrelated
  subprocess capability probe.
  See the [validation report](reviews/2026-11-03-unity-7-il2cpp-validation.md).
- The minimum Unity version is 2022.3.62f3, matching the oldest validated Editor.
  Retain the documented platform and serialization limits; the optional URP
  Showcase remains a Unity 6000 sample.
- Delivery uses a single UPM package. Version 4.7.6 was uploaded through Unity's
  official SDK, and the publisher confirmed submission for vetting on November 7.
  Unity approval and published Store acquisition remain pending; see the
  [submission evidence](reviews/2026-11-07-official-upm-validation.md).
- The public [Getting Started guide](https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation~/GettingStarted.md)
  and license URL were rechecked successfully on November 6. The package now
  includes documentation/license links and offline guides/changelog. The new
  changelog's public URL remains unset until that page is published.
- Keep storage modes, boxing, serialization cache behavior, and detailed Burst
  restrictions in the user guides, following the approved marketing plan.
- The short third-party notice and explicit requirements follow Unity's
  [submission guidelines, sections 1.1.c, 1.2.a, and 3.1.a](https://marketplace.unity.com/publishing/submission-guidelines).

Source references for review: [marketing decisions](marketing_plan.md),
[package overview](unity-package/README.md),
[setup](unity-package/Documentation~/GettingStarted.md),
[usage](unity-package/Documentation~/Usage.md),
[compatibility](unity.md), and
[third-party notices](unity-package/Third%20Party%20Notices.md).
