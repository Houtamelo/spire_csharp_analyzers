# Unity package sample fixture

This directory owns the sample inputs exported to `Samples~/Showcase` and the
project settings used by customer acceptance. Package and Unity tests consume
this fixture; they do not read or modify the interactive projects in
`examples/`.

`sample-files.txt` lists every exported file under `Assets`, including authored
Unity metadata. Keep that inventory complete when changing the fixture.
Preserve the assembly and asset GUIDs: customer acceptance resolves the imported
sample through those identities.

The saved preset and scene must match `ShowcasePreset.ResetExamples`. Regenerate
them in a disposable project using `ShowcaseAuthoring.CreateExamples`, then
copy the reviewed assets back here. Run customer acceptance on a fresh export
to verify import, serialization, reference sharing, and the sample's own tests.
Do not open this fixture in Unity in place; test projects belong under `tmp/`.
