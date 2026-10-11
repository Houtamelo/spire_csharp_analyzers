# Unity IL2CPP release changelog audit

Checked on October 10, 2026. No exact published issue has been confirmed as the
cause of Spire's native scene-loading failure. Later releases contain relevant
serialization fixes, but their applicability needs a test with the fixed fixture.
The subsequent [6000.6.5f1 test run](unity-il2cpp-opaque-managed-reference-slots.md#unity-66-recheck-on-october-10)
encountered an earlier Editor crash during native serialization preparation.

## Versions and scope

The original test Editor was `6000.5.6f1` (`0e0577a1a2ac`), released July 29.
Unity identifies [6.3 as the latest LTS line](https://unity.com/releases/unity-6/support).
Its latest patch is [6000.3.26f1](https://unity.com/releases/editor/whats-new/6000.3.26f1),
released October 8. The latest production Update release is
[6000.6.5f1](https://unity.com/releases/editor/whats-new/6000.6.5f1), also October 8.
Thus the installed Editor is on a numerically later branch than LTS; the
comparison uses release dates rather than treating LTS as a forward version range.

Every production Editor release published from July 29 through October 8 was
included: 22 complete changelogs, including the installed version and the
same-day LTS patch. This covers all intervening production patches on 6.0 LTS,
6.3 LTS, 6.5, and 6.6. Alpha/beta Editors and releases predating July 29 are outside
the coverage window; individual older fixes were checked where necessary to
avoid misclassifying cumulative 6.6 notes as new fixes.

The release list came from Unity's public Editor release API, paginated through
the first release older than the baseline date. Complete official Markdown
notes were downloaded, all entries screened, and relevant known issues, fixes,
API changes, serialization, scripting, IL2CPP, player, and build entries reviewed.
The 6.6.0 notes include cumulative changes, so entry counts are not counts of
distinct new fixes.

## Findings

| Issue | Published fix / branch status | Relevance to our failure |
|---|---|---|
| [UUM-128055](https://issuetracker.unity.com/issues/21719/errors-are-thrown-for-lists-and-arrays-in-build-using-il2cpp-when-data-class-contains-both-if-unityeditor-fields-and-generic-fields) | Fixed on 6.6; included in 6000.6.0f1. Tracker still lists 6.3 and 6.5 open. | Incorrect IL2CPP layouts for generic array/list elements with conditional Editor serialized fields. Related failure family, but that specific field condition has not been established in our fixture. |
| UUM-152214, [6000.6.3f1 notes](https://unity.com/releases/editor/whats-new/6000.6.3f1) | Managed-reference mapping fix in 6000.6.3f1. No public detailed report or LTS backport confirmed. | Null class elements in serialized arrays/lists can misassign managed references. Potentially relevant to our graphs; the note does not identify our startup trap. |
| [UUM-151757](https://issuetracker.unity.com/issues/24651/objectinstantiate-corrupts-a-listt-field-when-another-reference-field-is-declared-before-it-player-build-only) | Introduced in 6000.6.0b6; fixed in 6000.6.3f1. | Player cloning can corrupt a managed-reference list following another reference field. This later 6.6 regression does not explain a 6.5.6 failure; avoid testing an earlier 6.6 patch. |
| UUM-143324, [6000.5.8f1 notes](https://unity.com/releases/editor/whats-new/6000.5.8f1) and [6000.3.22f1 notes](https://unity.com/releases/editor/whats-new/6000.3.22f1) | Fixed in 6000.5.8f1, 6000.3.22f1, 6000.0.82f1; also listed in 6.6.0. | Off-thread WeakReference destruction causes GC crashes. Our captured failure is a preload serialization trap, without GC frames. |
| UUM-152365, [6000.3.25f1 notes](https://unity.com/releases/editor/whats-new/6000.3.25f1) | Development-player assert guard in 6000.3.25f1 and 6000.6.3f1. | Vague public description; no published stack or affected subsystem establishes a match. |
| UUM-149985, [6000.6.2f1 notes](https://unity.com/releases/editor/whats-new/6000.6.2f1) | Linux/macOS IL2CPP segmentation-fault fix in 6000.6.2f1. | Vague public description. Our current signature is SIGTRAP, so this is not a confirmed match. |
| [UUM-137538](https://issuetracker.unity.com/issues/17398/crash-on-cachedreaderoutofboundserror-when-loading-vfx-object-from-an-asset-bundle-with-disabletypetree) | Already fixed in 6000.5.0b5 and 6000.3.14f1. | Similar out-of-bounds wording, but affects VFX AssetBundles with disabled type trees. The fixture uses neither, and our Editor already contains the fix. |
| UUM-144008, [6000.5.3f1 notes](https://unity.com/releases/editor/whats-new/6000.5.3f1) | Already fixed in 6000.5.3f1. | Null method-pointer fix for Faster/Smaller code generation. Already included locally; it does not newly resolve our observed stripping dependency. |
| UUM-135412, [6000.6.0f1 notes](https://unity.com/releases/editor/whats-new/6000.6.0f1) | Cumulative notes list a fix for TypeCache attribute queries omitting Serializable/StructLayout types. Earlier-branch provenance not established. | No demonstrated connection to native serialized-type generation; not evidence that our missing payload metadata is fixed. |

No reviewed production release note specifically identifies the observed
combination of `level0` corruption, the preload SIGTRAP, and omitted stability
payload type metadata. Similar text alone is not a root-cause match. Our emitted
serialization reference field has the same declaration in Editor and player;
excluding entire numeric Inspector fixture types is different from UUM-128055's
conditional serialized-field trigger.

## Coverage

All listed changelogs were fetched in full and checked. Sources and dates are
from Unity's official release API and linked release pages.

| Version | Released | Stream |
|---|---|---|
| [6000.5.6f1](https://unity.com/releases/editor/whats-new/6000.5.6f1) | July 29 | Update, installed baseline |
| [6000.3.21f1](https://unity.com/releases/editor/whats-new/6000.3.21f1) | July 29 | LTS |
| [6000.5.7f1](https://unity.com/releases/editor/whats-new/6000.5.7f1) | August 5 | Update |
| [6000.0.81f1](https://unity.com/releases/editor/whats-new/6000.0.81f1) | August 6 | LTS |
| [6000.5.8f1](https://unity.com/releases/editor/whats-new/6000.5.8f1) | August 12 | Update |
| [6000.3.22f1](https://unity.com/releases/editor/whats-new/6000.3.22f1) | August 13 | LTS |
| [6000.5.9f1](https://unity.com/releases/editor/whats-new/6000.5.9f1) | August 19 | Update |
| [6000.0.82f1](https://unity.com/releases/editor/whats-new/6000.0.82f1) | August 19 | LTS |
| [6000.5.10f1](https://unity.com/releases/editor/whats-new/6000.5.10f1) | August 26 | Update |
| [6000.3.23f1](https://unity.com/releases/editor/whats-new/6000.3.23f1) | August 26 | LTS |
| [6000.6.0f1](https://unity.com/releases/editor/whats-new/6000.6.0f1) | August 31 | Update, cumulative notes |
| [6000.5.11f1](https://unity.com/releases/editor/whats-new/6000.5.11f1) | September 2 | Update |
| [6000.0.83f1](https://unity.com/releases/editor/whats-new/6000.0.83f1) | September 2 | LTS |
| [6000.3.24f1](https://unity.com/releases/editor/whats-new/6000.3.24f1) | September 10 | LTS |
| [6000.0.84f1](https://unity.com/releases/editor/whats-new/6000.0.84f1) | September 16 | LTS |
| [6000.6.1f1](https://unity.com/releases/editor/whats-new/6000.6.1f1) | September 16 | Update |
| [6000.6.2f1](https://unity.com/releases/editor/whats-new/6000.6.2f1) | September 18 | Update |
| [6000.3.25f1](https://unity.com/releases/editor/whats-new/6000.3.25f1) | September 24 | LTS |
| [6000.6.3f1](https://unity.com/releases/editor/whats-new/6000.6.3f1) | September 24 | Update |
| [6000.6.4f1](https://unity.com/releases/editor/whats-new/6000.6.4f1) | October 1 | Update |
| [6000.6.5f1](https://unity.com/releases/editor/whats-new/6000.6.5f1) | October 8 | Latest Update |
| [6000.3.26f1](https://unity.com/releases/editor/whats-new/6000.3.26f1) | October 8 | Latest LTS patch |

Downloaded full notes, official release-list responses, candidate entries, and
SHA-256 hashes are retained under `tmp/unity-changelog-audit`. The manifest has
22 unique versions and records source URLs and audit status for every document.

## Next validation

Use the fixed current fixture and saved failing scene from the
[package comparison](unity-il2cpp-committed-baseline-comparison.md), with identical
package DLLs. Test both 6000.3.26f1 for the LTS target and 6000.6.5f1 for the
newer serialization fixes. Keep the scene, fixture source, GUIDs, Linux target,
IL2CPP, and High stripping fixed in isolated projects, recording imports and any
Editor-driven data migration. A passing newer build would show a version effect;
it would not by itself identify which listed fix caused it.

The audit itself did not upgrade an Editor. After the user installed 6000.6.5f1,
the complete Editor Mono harness passed 2,266 checks and the IL2CPP behavioral
player passed 137. Native Editor integration and every serialization player
preparation profile, including the fixed saved scene, crashed in the Editor.
All 50 fixed fixture source/meta/scene files remained identical after import.
Consequently the newer Editor did not provide a passing native serialization
validation, and the original player loading failure could not be retested.
6000.3.26f1 LTS has not been run.
