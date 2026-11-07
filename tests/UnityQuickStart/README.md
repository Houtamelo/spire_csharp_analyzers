# Quick Start sample inputs

`Assets/QuickStart` contains the canonical non-URP sample. `sample-files.txt`
explicitly lists every exported asset and its metadata. Package preparation
fails before output mutation when a listed input is missing.

The actual Unity tarball acceptance imports these bytes from the signed package
through Package Manager. On Unity 2022, 2023, and 6000 it exercises all three
factories, generated accessors, the wrong-variant assertion, and a native Unity
JSON round trip of the imported component. No sample inputs come from examples.
