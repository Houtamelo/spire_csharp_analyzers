# Native record-variant acceptance

This isolated Editor assembly uses C# 10 and is constrained to
`UNITY_6000_4_OR_NEWER`. It does not change the fixture's existing C# baseline.

Run the write phase in a fresh disposable Unity project after the package and
generator implementation are ready:

```sh
dotnet run -c Release --project tools/Spire.Tool -- \
  unity-union-core --method Spire.UnityRecordVariantTests.RecordVariantNativeEditorTests.Write \
  --project tmp/record-variant-native --timeout 900
```

Run the read phase in a fresh Editor process using the same project and asset:

```sh
dotnet run -c Release --project tools/Spire.Tool -- \
  unity-union-core --method Spire.UnityRecordVariantTests.RecordVariantNativeEditorTests.Read \
  --project tmp/record-variant-native --timeout 900
```

The phases write `record-variant-native-{write,read}-results.json` and a YAML
baseline. They exercise actual Unity `SerializedObject` paths, backing-field
names, generated internal wrappers, record equality/hash behavior, prepare and
hydrate callbacks, null/derived/shared managed references, and a former-name
YAML migration that replaces `SliderSerializedPayload` with
`OldSliderSerializedPayload` before a fresh read process restores the canonical
record payload. It also migrates an explicitly declared serialized record field
(`Explicit`/`OldExplicit`) and a legacy method-shaped `_value` key for the
`Slider` alias without renaming unrelated variant data.
