# Struct field-collision coverage

This matrix covers the agreed struct-union field and generated-property policy.
Record and class unions are control cases only; their existing surface and
diagnostics remain outside this change.

| Area | Case | Expected result |
| --- | --- | --- |
| Compatible reference fields | `Effect` and `Effect?` use the same field name in either declaration order | One shared property with the conservative nullable contract; both factories preserve their parameter annotations and runtime values. |
| Nested nullability | `List<Effect?>?` and `List<Effect>?` use the same field name | One shared property whose outer and nested reference annotations are nullable. |
| Tuple identity | Tuple element names differ while element types and nullability are equivalent | One shared property; generated tuple element names remain deterministic and the nested reference element remains nullable. |
| Nested value-type identity | `Outer<Inner<Effect?>>` and `Outer<Inner<Effect>>` use the same field name | One shared property with the nested `Effect` argument conservatively nullable; runtime values survive the recursive wrappers. |
| Nullable array identity | `int?[]` and `int?[]?` use the same field name | One shared property with a nullable array container and `Nullable<int>` elements preserved. |
| Nullable value types | `int` and `int?` use the same field name | The fields remain incompatible and receive separate qualified properties. |
| Two-way collision | `Count(int value)` and `Text(string value)` | All incompatible participants receive `count_value` and `text_value`; the original `value` property is absent. |
| Three-way collision | `HTTPResponse(int value)`, `URL(string value)`, and another distinct value type | Every participant is qualified; noncolliding fields retain their original names. Acronyms become `httpResponse_value` and `url_value`. |
| Digit/underscore variants | `HTTP2Response` and `URL_V1` collide on `value` | The same uppercase-prefix algorithm produces `http2Response_value` and `url_V1_value`. |
| Keyword variants | `@class` and `@event` collide on `value` | Generated identifiers escape the variant prefix in source and expose metadata names `class_value` and `event_value`. |
| Keyword fields without collisions | Fields named `@event` and `@class` across Auto and every explicit layout | Generated properties and runtime accessors preserve the keyword metadata names and compile with escaped source identifiers. |
| Deconstruct | Colliding one-field variants | Existing shared-arity deconstruction remains available and returns the original runtime values. |
| Analyzer mapping | Guarded and unguarded access to qualified properties | Field-access and type-safety analyzers resolve the qualified names to their original variants. |
| Exhaustiveness | Property-pattern switch over every colliding variant | Exhaustiveness recognizes the generated discriminator and qualified fields. A missing variant remains SPIRE009. |
| Unity payload schema | Serializable `Count(int value)` and `Text(string value)` | Factory parameters and payload fields retain the original `value` name and declared types; only runtime properties are qualified. |
| Unity nullable payload schema | Serializable `Effect` and `Effect?` fields | Factory and per-variant payload field nullability annotations remain distinct while payload fields retain `value` and `[SerializeField]`. |
| Unity keyword fields | Serializable fields named `@event` and `@class` with Auto layout | Runtime properties and payload fields retain the escaped keyword names and `[SerializeField]`. |
| JSON | Incompatible `value` fields plus `@event`, using both JSON libraries | Real System.Text.Json and Newtonsoft.Json converters emit original `value` names and round-trip each qualified property. |
| ToString | Colliding `displayName` fields | ToString reads each resolved property and preserves the original factory parameter name in the generated model. |
| Layouts | Additive, BoxedFields, BoxedTuple, Overlap, UnsafeOverlap | The runtime and deconstruct cases pass for each applicable struct layout. |
| Controls | Existing record and class union sources | Existing generated APIs compile and no new field-collision diagnostic is introduced. |
| Record diagnostic control | Record variants with differing same-named field types | Existing `SPIRE_DU010` remains reported; struct qualification does not alter record behavior. |
| Invalid-source control | Overloaded `[Variant]` methods with the same name | Generator returns diagnostics or compiler errors without throwing from duplicate-name bookkeeping. |
