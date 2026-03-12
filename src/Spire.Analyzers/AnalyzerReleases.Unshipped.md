### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SPIRE001 | Correctness | Error | Non-empty array of [MustBeInit] struct produces default instances
SPIRE002 | Correctness | Warning | [MustBeInit] on fieldless type has no effect
SPIRE003 | Correctness | Error | default(T) where T is a [MustBeInit] struct produces an uninitialized instance
SPIRE004 | Correctness | Error | new T() on [MustBeInit] struct without parameterless constructor is equivalent to default(T)
