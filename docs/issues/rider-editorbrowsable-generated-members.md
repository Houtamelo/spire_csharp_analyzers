# Rider completion probe for generated implementation members

Status: bounded probe, inconclusive for EditorBrowsable filtering.

The probe used the installed Rider **2026.2.3.1**, build `RD-262.10968.170`, with a disposable two-project .NET 8 solution under `tmp/rider-member-visibility-probe/`. The fixture contained a representative same-assembly struct and a referenced-library struct. Each type exposed `VisibleProperty`, a public `__SpirePublicHidden`, and an internal `__SpireInternalHidden`; the latter two had `[EditorBrowsable(EditorBrowsableState.Never)]`.

The custom Rider driver invoked Rider’s actual completion path through `CodeCompletionHandlerBase.createHandler(...)`, `invokeCompletion(...)`, and `LookupManager.getActiveLookup(...)`. Rider produced a real `LookupImpl` at both caret locations:

```text
PROBE SAME_CARET
LOOKUP com.intellij.codeInsight.lookup.impl.LookupImpl
ITEMS []

PROBE EXTERNAL_CARET
LOOKUP com.intellij.codeInsight.lookup.impl.LookupImpl
ITEMS []
```

The result is not evidence that Rider hides or shows the annotated members. The completion popup was empty for both the same-assembly and external-assembly control, so the probe did not reach a useful member-list comparison. The project and ReSharper backend loaded successfully, but this minimal headless solution did not yield C# member completion items through the direct API invocation. The probe was intentionally stopped at this boundary rather than expanding into a new IDE completion harness.

Reusable artifacts are kept in `tmp/rider-member-visibility-probe/`, including the fixture, driver source, compiled driver, isolated Rider logs, and `run-introspection/completion-report.txt`. No production source or Rider workflow was changed.

Until a real editor-completion fixture produces a populated list, `[EditorBrowsable(EditorBrowsableState.Never)]` should be treated as metadata emitted for IDE cooperation, with its practical Rider hiding behavior left unverified.
