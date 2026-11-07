# Quick Start

Import this sample from Spire's Package Manager Samples tab. It requires no
additional packages and works independently of the render pipeline.

Add the QuickStart component to a GameObject. Its Command field demonstrates
the generated union Inspector. Choose a variant and edit its payload, then use
the component's **Describe command** context menu to log the value.

QuickStart.cs declares the union factories and consumes the generated accessors.
The adjacent csc.rsp enables C# 10 and nullable analysis for this sample assembly.
The sample uses a non-generic union; Unity 2022 does not support native
serialization of generic unions.
