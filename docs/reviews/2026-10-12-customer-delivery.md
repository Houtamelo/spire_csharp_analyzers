# Customer delivery follow-up

On `unity-6000`, the customer bundle now includes offline guides, API XML
documentation, third-party notices, and an optional importable showcase.
Real Package Manager clean installation and prerelease upgrade acceptance both
pass on Unity 6000.5.6f1, Linux x64, under Xvfb.

## Delivered

- `Documentation~` contains setup, usage, serialization, troubleshooting,
  upgrade guidance, and the existing SPIRE rule references. Its local links are
  checked against the exported bundle, without depending on a checkout.
- `Runtime/Houtamelo.Spire.xml` accompanies the runtime DLL. Missing public
  documentation is a build error, and integration tests inspect the exported
  XML against the actual public types/members, including inherited overload docs.
- `Third Party Notices.md` includes the pinned PolySharp 1.15.0 MIT license and
  the compatibility metadata inventory inspected from all four shipped DLLs.
  Compiler-generated definitions are distinguished from PolySharp attribution.
  Compiler-host DLLs remain excluded. Spire's MIT license is unchanged.
- `Samples~/Showcase` includes authored assets, scripts, tests, and preserved
  metadata GUIDs. URP 17.5.0 and Test Framework 1.7.0 are optional sample
  prerequisites rather than dependencies imposed on every Spire consumer.
  Asset paths are resolved from the sample assembly GUID. Import does not change
  render settings; the explicitly invoked setup menu does.
- Documentation and sample inventories make missing inputs fail before output
  writes. Deterministic export and unrelated-file protection remain enforced.

## Acceptance workflow

Build the package, then run clean customer acceptance:

```sh
dotnet run -c Release --project tools/Spire.Tool -- package
dotnet run -c Release --project tools/Spire.Tool -- unity-customer \
  --package tmp/unity-package/com.houtamelo.spire \
  --project tmp/unity-customer-clean --editor /path/to/Editor/Unity
```

Use `--baseline /path/to/prior/com.houtamelo.spire` and a separate disposable
`--project` for upgrade acceptance. `--prepare-only` builds the disposable project
without launching an Editor. Invalid packages, unrelated projects, and attempts
to overwrite the tracked fixture are rejected.

Installation uses Unity's `Client.Add`; clean sample import uses `Sample.Import`.
The workflow verifies the registered package path and waits across domain reloads.
It runs the showcase's actual Unity tests. Upgrade checks compare scene/preset
GUIDs and bytes, factory values, managed-reference sharing/nulls, and unchanged
asset saves both before and after those tests.

The October 12 upgrade baseline is the preserved, previously tested `b1ceada`
bundle. Both inputs identify themselves as prerelease **4.7.1**, with distinct
local package paths. This proves replacement and asset-preservation mechanics;
it does not certify migration from an older publicly released Unity version.
Preserve actual released bundles and rerun the same workflow for future releases.

## Validation

Native clean installation/import passes all 15 showcase tests. Native prerelease
upgrade passes all 15 and the final asset-preservation check. A missing baseline
capture is rejected by the real Editor with a failure report and exit code 1.
The .NET tooling suite passes 91/91; the runtime behavioral suite passes 129/129. The complete Editor Mono
harness passes 2,313/2,313 and matches all 2,174 portable xUnit identities;
every discovered case and original behavioral assertion executes.

Final tooling and complete Editor Mono results are recorded with the machine
reports under `tmp/customer-delivery`. The installable bundle is at
`tmp/unity-package/com.houtamelo.spire`.

The rendered test runner gives Unity a writable per-project GI cache using
`-giCustomCacheLocation`. A known Mono startup descriptor assertion is retried
once only when it prevented a test report from being created; assertion logs
remain available. Failed test results are never treated as passing acceptance.

The real build-mode CLI integration test exposed shared MSBuild nodes hanging
under the .NET test host. A process-tree capture showed orphaned reusable nodes;
changing only `MSBUILDDISABLENODEREUSE=1` changed the same test from a three-minute
timeout to a sub-second pass. `ToolContext.BuildAsync` now scopes that setting to
its build subprocess. CLI diagnostics preserve the command/environment whitelist
and output logs under `tmp/customer-delivery/cli-logs`.

## Remaining release decisions

The maintained baseline is still unresolved. The official
[6000.3.26f1 LTS Editor](https://unity.com/releases/editor/whats-new/6000.3.26f1)
initially rejected the Roslyn 4.10 compiler extensions with CS9057. A subsequent
Roslyn 4.3.1 downgrade resolves that rejection with two enum compatibility checks.
The 120-assertion native Inspector fixture passes when its runtime Unsafe
dependency is supplied separately. See [the follow-up evaluation](../issues/unity-6.3-roslyn-host-compatibility.md).
The shipping minimum and sample dependencies remain unchanged pending the
runtime dependency and complete release-baseline decisions.
The separately reported 6.6 native callback crash remains an engine boundary.

Library source distribution, advertised platforms/version commitments, Unity
store review questions about unsafe code/compiled distribution, publisher contact
details, AI disclosure, and release tag/version policy still need product or
publication decisions. Customer delivery does not change the approved
single-alias schema migration scope.
