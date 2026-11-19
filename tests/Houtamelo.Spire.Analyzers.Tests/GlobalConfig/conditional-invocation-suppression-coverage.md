# CS8509/CS8524 Conditional Invocation Suppression Coverage Matrix

This is a focused regression matrix for a switch expression used as the argument
to a null-conditional delegate invocation. Invocation arguments and standalone
switches must follow the same exhaustiveness rules. Each exhaustive case must first produce a
compiler `CS8509` or `CS8524` diagnostic and then have that diagnostic marked
suppressed by `CS8509Suppressor`.

The enum is marked `[EnforceExhaustiveness]` in the relevant cases and contains
exactly `Back`, `Front`, and `Color`. The source should define an
`Action<AdjustmentTarget>? on_adjustment_selected` (or an equivalent delegate)
so the invocation is valid while remaining conditional.

## Category A: Switch expression passed to a conditional delegate invocation

### should_fail

| File Name | Description |
|-----------|-------------|
| `ConditionalInvocation_AllHairRoutesCovered` | Reproduce the user report exactly: `on_adjustment_selected?.Invoke(route switch { HairRoute.Back => AdjustmentTarget.HairBack, HairRoute.Front => AdjustmentTarget.HairFront, HairRoute.Color => AdjustmentTarget.HairColor })`; all named enum members are covered and the compiler exhaustiveness diagnostic is suppressed. |
| `ConditionalInvocation_AllHairRoutesCovered_ExpressionBodied` | Suppress the diagnostic when the same exhaustive switch is the expression-bodied return value of a helper that is passed to `?.Invoke`. |
| `ConditionalInvocation_AllHairRoutesCovered_LocalThenInvocation` | Suppress the diagnostic when the exhaustive switch is first assigned to a local `AdjustmentTarget` and that local is passed to `on_adjustment_selected?.Invoke`; this is a control proving the enum checker still handles the surrounding invocation context. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ConditionalInvocation_MissingHairRoute` | Keep `CS8509`/`CS8524` unsuppressed when the conditional invocation switch omits `HairRoute.Color`. |
| `NoReport_ConditionalInvocation_GuardedHairRoute` | Keep the diagnostic unsuppressed when all three enum labels appear but one arm has a guard that leaves part of that member's values unmatched. |
| `NoReport_ConditionalInvocation_NullableHairRouteWithoutNull` | Keep the diagnostic unsuppressed when every named member is handled but the nullable subject's null case is missing. |

## Category B: Invocation and diagnostic-span controls

### should_fail

| File Name | Description |
|-----------|-------------|
| `DirectInvocation_AllHairRoutesCovered` | Suppress the same exhaustive `HairRoute` switch when passed to a non-conditional `on_adjustment_selected.Invoke(...)`; this isolates conditional access from the underlying invocation argument. |
| `ConditionalInvocation_AllHairRoutesCovered_InLambda` | Suppress the exhaustive switch when the conditional invocation is inside a lambda or callback body, ensuring operation lookup works when the diagnostic is nested in a lambda. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ConditionalInvocation_UnrelatedSwitchDiagnostic` | Leave an incomplete switch's compiler diagnostic unsuppressed when another exhaustive switch in the same method is correctly suppressible; suppression must be scoped to the reported switch and must not hide the incomplete one. |
| `NoReport_ConditionalInvocation_IncompleteInnerSwitch` | Suppress an exhaustive outer switch's diagnostic while preserving the incomplete inner switch's diagnostic, matched by source span. |
| `NoReport_ConditionalInvocation_IncompleteOuterSwitch` | Preserve an incomplete outer switch's diagnostic while suppressing the exhaustive inner switch's diagnostic, matched by source span. |
| `NoReport_ConditionalInvocation_NonEnumSubject` | Leave a conditional invocation switch on an unrelated non-enum subject unsuppressed when the checker cannot prove exhaustiveness; the regression must not broaden suppression to indeterminate patterns. |

## Scope decisions

- The exact `?.Invoke` argument shape is the acceptance case; direct invocation,
  local assignment, and lambda nesting are controls for the same operation-span
  boundary.
- No additional tests are required for tuples, generated union types, or ordinary
  switch statements here; those surfaces already have dedicated suppressor coverage.
- Cases that are already exhaustive in the compiler must still assert that a
  compiler exhaustiveness diagnostic exists before checking suppression. This
  prevents a test from passing merely because the compiler stopped reporting a
  warning in a future Roslyn version.
