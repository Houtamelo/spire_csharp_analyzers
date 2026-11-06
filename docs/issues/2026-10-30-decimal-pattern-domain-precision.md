# Decimal pattern-domain precision

`NumericDomain` represents decimal bounds through `double`, losing decimal
digits before exhaustiveness witnesses reach diagnostic metadata. For example,
`0.1234567890123456789012345678m` cannot round-trip through that representation.
Rendering the witness as a new decimal literal could change the missing region.

Relevant code: `src/Houtamelo.Spire.PatternAnalysis/Domains/NumericDomain.cs`,
`TryCreateRelationalRange` and `GetBounds`.

The missing-arm fix declines constrained decimal witnesses. Whole-variant fixes
with unrestricted decimal payloads still use the existing capture templates.
`ExactMissingShapeCodeFixTests` covers the no-action boundary.

A separate checker change should retain exact decimal endpoints throughout
interval subtraction, partitioning, and witness construction. Cover adjacent
28-digit values, decimal extrema, nullable decimals, correlated payloads, and
compile/emit/reanalysis of generated range patterns. Do not reconstruct exact
endpoints from formatted doubles.
