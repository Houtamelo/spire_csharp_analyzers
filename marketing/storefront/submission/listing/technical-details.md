- **Validated Unity version:** 6000.5.6f1.
- **Unity 2020:** will not work.
- **IDE support:** Spire uses Roslyn's standard analysis/code-fix providers, any IDE that supports that should also support Spire. Verified in JetBrains Rider and Visual Studio Community.
- **Tested platform and backends:** Linux x64, Windows 10, Editor Mono, and IL2CPP players.
  Other Unity versions and platforms require separate validation.
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
