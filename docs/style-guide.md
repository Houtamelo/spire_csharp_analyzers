## General principles

- Be concise, clear, pragmatic. Say what needs to be said, nothing more.
- No emojis. No emojis!
- Clarity and brevity should be prioritized over grammar.
- Avoid excessive markdown formatting. Use very few headers, sections, and horizontal rules; bold/italic even less.
- This project is LLM-orchestrated. Docs are consumed by agents — keep token count low.

## Code comments

Code should be self-documenting: Comments explain why, not what.

- Don't comment obvious code.
- Don't add comments to code you didn't write or change.
- Use comments when the reasoning behind a decision isn't apparent from the code itself. If in doubt, don't write.

## C# docs

**Public API only** (attributes like `[MustBeInit]`, public utilities meant for consumers) gets full XML doc tags:

```csharp
/// <summary>
/// Marks a struct as requiring explicit initialization.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class MustBeInitAttribute : Attribute { }
```

**Internal code** (analyzers, helpers, private/internal methods, tests) uses plain `///` comments without XML tags:

GOOD:
```csharp
/// Returns true if the type has at least one instance field.
private static bool HasInstanceFields(INamedTypeSymbol type)
```

BAD:
```csharp
/// <summary>
/// Returns true if the type has at least one instance field.
/// </summary>
/// <param name="type">The type symbol to check.</param>
/// <returns>True if the type has instance fields.</returns>
private static bool HasInstanceFields(INamedTypeSymbol type)
```

## Rule documentation (docs/rules/)

One `.md` file per rule, named `{RuleId}.md`. Required sections:

1. Title — `# {RuleId}: Short description`
2. Property table — ID, Category, Severity, Enabled
3. Description — what it detects, when it fires, when it doesn't
4. Examples — violating and compliant code
5. When to suppress — valid reasons + `#pragma` example

Keep descriptions short. The spec tables (flagged / not flagged) are more useful than prose.

## Markdown files

- Don't repeat information that exists in other docs — link or reference instead.
- Don't create README files or documentation unless explicitly asked to.
