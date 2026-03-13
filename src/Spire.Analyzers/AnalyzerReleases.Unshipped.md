### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SPIRE001 | Correctness | Error | Non-empty array of [MustBeInit] struct produces default instances
SPIRE002 | Correctness | Warning | [MustBeInit] on fieldless type has no effect
SPIRE003 | Correctness | Error | default(T) where T is a [MustBeInit] struct produces an uninitialized instance
SPIRE004 | Correctness | Error | new T() on [MustBeInit] struct without parameterless constructor is equivalent to default(T)
SPIRE005 | Correctness | Error | Activator.CreateInstance on [MustBeInit] struct produces a default instance
SPIRE006 | Correctness | Error | Clearing array or span of [MustBeInit] struct produces default instances
SPIRE007 | Correctness | Error | Unsafe.SkipInit on [MustBeInit] struct leaves it uninitialized
SPIRE008 | Correctness | Error | RuntimeHelpers.GetUninitializedObject on [MustBeInit] struct bypasses all constructors
