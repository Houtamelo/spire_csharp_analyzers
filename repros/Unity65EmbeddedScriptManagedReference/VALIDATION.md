# Validation

Tested on 2026-10-11 UTC with Unity 6000.5.6f1 (`0e0577a1a2ac`) and 6000.6.5f1
(`3ff58d469c8a`), Linux x86_64, under Xvfb. IL2CPP probes use High stripping and CleanBuildCache. Each comparison
starts from an independent project copy without Library or previous builds.

| Configuration | Player result |
| --- | --- |
| This project's saved scene, 6.5 IL2CPP | Build succeeds with one missing-script warning; player exit 133; `level0 is corrupted` and `Position out of bounds`; no managed startup marker |
| Same archive imported in 6.6, identical C# and scene inputs | Build exit 0, zero errors, one missing-script warning; player exit 133 with the same corruption and position error; no startup marker |
| Same runtime source and generated scene, IL2CPP | Same failure |
| Same runtime source, Mono | Exit 0; startup marker confirms backend Mono |
| Component moved to `ProbeHost.cs`, scene recreated, IL2CPP | Exit 0; startup marker confirms backend IL2CPP |
| Embedded component with its field removed, IL2CPP | Exit 0 despite the missing-script warning |

During the 6.6 import, Unity upgraded the project version and added
`com.unity.modules.tetgen` 1.0.0 to the manifest. The two Linux SDK/toolchain
packages stayed at 1.1.0. Both C# files, the runtime script metadata, and the
saved scene and its metadata remained byte-identical to this project.

The controls below the 6.6 row were run on 6000.5.6f1. The original Spire stability fixture reproduced the same corruption in a fresh
project. Moving its component into `StabilityPlayerHost.cs` and replacing only
the frozen scene's embedded script reference with that asset GUID made it pass.
Every serialized value and all four package DLLs stayed unchanged; every required
stability and managed-reference assertion passed. A fresh complete fixture also
passes all main, stability, and managed-reference assertions in one IL2CPP/High
player. The test fixture now checks script registration before building.

Repeated builds in one Editor session can pass despite the bad registration.
Some batch-mode Editors exited 139 after recording a successful build; player
launches and build result files were checked separately. The reported defect is
the player's exit 133 during scene loading.

This repro does not cover other operating systems or Unity versions beyond those
listed above. It is
separate from the generic-struct serialization-callback Editor crash found on
Unity 6000.6.5f1.
