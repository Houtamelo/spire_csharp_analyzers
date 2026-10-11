# Closed generic union property-pattern warning

Observed while compiling the Unity showcase on 6000.5.6f1, Linux, against the
`unity-6000` package. The non-generic `Shape` property-pattern switch receives
Spire's normal suppression of `CS8524`. This analogous closed generic switch
retained the compiler warning:

```cs
var result = preset.result; // OperationResult<int>
var label = result switch
{
    { kind: OperationResult<int>.Kind.Success, value: var value } => $"Success({value})",
    { kind: OperationResult<int>.Kind.Failure, error: var error } => $"Failure({error})",
};
```

The warning identifies an unnamed `OperationResult<int>.Kind` value, despite
both declared variants being handled. This observation concerns suppression of
the compiler's exhaustiveness warning; it does not establish a runtime or
serialization failure. The deeper cause has not been investigated.

The showcase uses guarded `is` property patterns for this display instead. Its
non-generic union example still demonstrates exhaustive switch checking. A
follow-up should reproduce the generic suppression case in the Roslyn suite and
check the closed generic union/domain symbol handling before changing analysis.
The initial native compile report was `tmp/unity-showcase-authoring.log`; later
successful imports replaced that scratch log.
