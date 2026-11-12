# Publish a NuGet release

`.github/workflows/publish.yml` publishes all four Spire packages from a release
tag after version validation, the CoreCLR/NuGet tests, and packing. Authentication
uses NuGet Trusted Publishing for the `houtamelo` profile. No stored publishing
API key is used.

## NuGet policy

Create the policy under the NuGet owner of the four packages:

| Setting | Value |
|---|---|
| GitHub repository owner | `Houtamelo` |
| Repository | `spire_csharp_analyzers` |
| Workflow filename | `publish.yml` |
| Environment | Empty; the workflow uses no GitHub environment |
| Package scope | `Houtamelo.Spire`, `Houtamelo.Spire.Analyzers`, `Houtamelo.Spire.CodeFixes`, `Houtamelo.Spire.Functional` |
| Permission | Publish new versions; allow creation of `Houtamelo.Spire.Functional` for its first release |

The job grants `id-token: write`. The official `NuGet/login` action exchanges
GitHub's OIDC identity for a temporary key immediately before uploading.
The action is pinned to the v1.2.0 commit. See
[NuGet's policy and workflow documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

Before the first Functional release, extend the existing policy scope to its
package ID and allow new-package publication. A policy limited to new versions
of the original three packages cannot create the new package. This account-side
setting is separate from the checked-in workflow.

## New release

Keep the four package project versions synchronized, commit the release notes,
and require the CoreCLR workflow to pass. Create and push a signed tag matching
those versions, such as `v6.0.0`. The tag push starts publication automatically.
The workflow checks out `refs/tags/<tag>` explicitly and records its commit in
the run summary.

## Retry an existing release with the current workflow

When publishing configuration has changed since the release tag was created,
dispatch the updated workflow from `main` with the existing tag:

```sh
gh workflow run publish.yml --repo Houtamelo/spire_csharp_analyzers \
  --ref main -f tag=v5.0.0
```

This uses the publishing workflow from `main` and builds the tagged source.
The release tag stays unchanged. Manual requests from other workflow refs and
malformed tag names fail before checkout or authentication. A missing tag fails
checkout; mismatched package versions fail before build, packing, or login.
Runs targeting the same release are serialized.

Tags created before Functional was added still pack their original three
packages. Current tags must contain all four package projects and matching
versions; the tagged version-check script enforces that before packing.

Re-running the original failed run retains its original workflow revision;
use the manual route to pick up a publishing configuration change. See
[GitHub's rerun behavior](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs).

Before announcing the release, require the publishing run to succeed and verify
that all four public package versions can be downloaded. Build and execute an
ordinary consumer using the public feed; also confirm an incomplete union match
still fails with the expected Spire diagnostic. Keep the GitHub release as a
draft until publication is verified. The Unity Asset Store submission is
independent of this process.
