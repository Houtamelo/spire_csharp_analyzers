# Legacy code-fix case migration

The old `*Deconstruct*` cases and the NullablePattern cases exercised
union-level flat field properties and union-level `Deconstruct` overloads.
Those APIs were removed by the variant-payload redesign, so those files were
removed. Their supported nullable, naming, and nested-pattern behavior is
covered by the real before/after cases in `VariantPayloadConsumers`.

The remaining `AddMissingArms_Property_*` and `FixFieldType_Property_*` cases
are active and use nested payload accessors.
