- **Validated Unity versions:** 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1.
- **Unity 2020:** will not work.
- **Unity 2021:** cannot load the current compiler extensions.
- **IDE support:** Spire uses Roslyn's standard analysis/code-fix providers, any IDE that supports that should also support Spire. Verified in JetBrains Rider and Visual Studio Community.
- **Tested desktop platforms and backends:** Linux x64 and Windows 10 x64 on all
  three versions, including Editor Mono, native serialization, rendered Inspector
  interaction, and standalone Mono/IL2CPP players.
- **Tested Web builds:** WebGL2/IL2CPP on all three versions in Chrome and Firefox
  on desktop Linux, with High managed stripping and `FullWithStacktrace` exception
  support. Unity's default Web exception setting does not preserve all tested
  exception behavior. Other browsers and platforms require separate validation.
- **Unity 2022 serialization:** non-generic unions pass native serialization;
  generic unions cannot use native Unity serialization on this version.
- **Package minimum:** Unity 2022.3.62f3. The optional URP Showcase requires
  Unity 6000.5.6f1, URP 17.5.0, and Unity Test Framework 1.7.0; Quick Start has no
  additional package dependencies.
- **JSON:** install the JSON library you select, System.Text.Json or
  Newtonsoft.Json, in your project.
- **Burst/DOTS:** Spire does not have code that specifically targets Burst/DOTS, our discriminated unions are naturally Burst/DOTS compatible since they are struct-based. Note that the fields of the variants must also comply with Burst's restrictions (Spire isn't magic!). Consult the included Usage and Serialization guides for payload and serialization choices.
- **Unity 6000.6.5f1 limitation (Linux):** an engine serialization bug affects generic
  unions with reference-type arguments, such as `Command<string>`. This crash is
  fixed in Unity 7000 (verified on 7000.0.0a12). We expect a backport to 6000.6.x
  soon, although its availability and timing remain unconfirmed.
- **Third-party notice:** includes compiler compatibility definitions generated
  by PolySharp under the MIT license. See Third Party Notices in the package for
  details.
