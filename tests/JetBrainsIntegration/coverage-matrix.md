# JetBrainsIntegration Test Coverage Matrix

This matrix is for a real JetBrains Rider run against the Unity-generated
solution. The target installation is Rider `2026.2.3.1`, build
`RD-262.10968.170`, at `/home/houtamelo/.local/share/JetBrains/Toolbox/apps/rider-2`.
The test must use an isolated Rider configuration and a bounded Xvfb/IDE
process. It must not replace Rider with an AdhocWorkspace, a fake UI, or a
second Roslyn code-fix runner.

The deterministic code-fix fixture is SPIRE016, because the shipped
`Houtamelo.Spire.CodeFixes` assembly contains `FromEnumCodeFix` for SPIRE016.
SPIRE002 has no provider in `src/Houtamelo.Spire.CodeFixes`; the existing
fieldless `_rider.cs` fixture therefore proves analyzer discovery only and
must not be described as having a SPIRE002 fix. SPIRE001 is also unsuitable
for this integration path because no matching code-fix provider is shipped.

After Unity has generated the solution and project files, replace or refresh
`tests/UnityAsmdefIntegration/Assets/RuntimeAuto/_rider.cs` with this source
so the already-generated project contains the file while the post-bootstrap
source change is analyzed by Rider:

```csharp
using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.RuntimeAuto
{
    [EnforceInitialization]
    public enum RiderQuickFixKind
    {
        Active = 1,
        Inactive = 2,
    }

    public static class RiderQuickFixProbe
    {
        public static RiderQuickFixKind Convert(int value)
        {
            return (RiderQuickFixKind)value;
        }
    }
}
```

The exact expected replacement is
`SpireEnum<RiderQuickFixKind>.From(value)`, and the exact provider title is
`Replace with SpireEnum<T>.From(value)`. The diagnostic must be SPIRE016 at
the cast. The constant-cast variant is a separate negative case because
`FromEnumCodeFix.RegisterCodeFixesAsync` intentionally does not register a
fix when the diagnostic property `IsConstant` is `true`.

The preferred installed automation route for bootstrap, editor targeting, and
save is the Rider performance-testing plugin's `LocalPlaybackRunner` and its
built-in commands. Reflection over the installed plugin confirms `%openFile`,
`%goto`, `%moveCaret`, `%assertCaretPosition`,
`%waitForFinishedCodeAnalysis`, `%altEnter`, `%executeEditorAction`,
`%saveDocumentsAndSettings`, `%selectText`, `%replaceText`,
`%takeScreenshot`, and `%exitApp`. `%altEnter` is useful as a bounded smoke
control, but its installed implementation calls IntelliJ's cached
`ShowIntentionActionsHandler.calcCachedIntentions` path. It is not sufficient
evidence for a Rider C# backend quick-fix: the cache may be empty while the
backend menu has the SPIRE016 action.

The build-262 quick-fix path must call the installed Rider presenter on the
EDT: construct `RiderShowIntentionActionsHandler`, then call
`createAndShowPopup(project, editor, psiFile, null, onPopupReady, false)`.
`onPopupReady` receives a `ListPopup`; cast it to `ListPopupImpl`, obtain its
Swing `ListPopupModel` from `getList().getModel()`, and attach a
`ListDataListener` before scheduling an initial scan with
`ApplicationManager.getApplication().invokeLater`. Scan `getElementAt(i)`
values as `PopupItem`, match `item.getDisplayName().getText()` to the exact
provider title, and call `ListPopupImpl.selectAndExecuteValue(item)` on the
EDT. The listener must handle `contentsChanged` as well as added/removed
events because backend items arrive reactively. The callback is installed
before `PopupMenus.show` displays the popup, so the deferred initial scan and
listener cover both already-present and late-arriving items. Store the returned
`RiderPopupMenuController.PopupMenu`; on a bounded timeout call its `close()`
via `invokeLater` (the method asserts the EDT and terminates the popup
lifetime). Wait for source persistence and Unity recompilation off the EDT.
This route exercises Rider's actual C# menu and action dispatch.

The IntelliJ Starter/Driver API is a valid fallback only after a bounded smoke
test against this Rider build; the installed Driver client uses JMX and the
official SDK exposes `invokeAction`, editor/caret components, and
`waitForCodeAnalysis`. RemoteRobot is deprecated in JetBrains' own repository
and is not a substitute without a proven build-262 run.

The first smoke script should be assembled with the absolute generated-file
path substituted by the harness (the placeholder below is not sent literally):

```text
%openFile {{RIDER_FILE}}
%waitForFinishedCodeAnalysis
%goto 17 20
%assertCaretPosition 17 20
%altEnter Replace with SpireEnum<T>.From(value)|false
%saveDocumentsAndSettings
%waitForFinishedCodeAnalysis
```

`%goto` and `%assertCaretPosition` use one-based line/column pairs. The
`|false` suffix makes `%altEnter` expose the cached-intentions smoke menu; it
must not be treated as proof that the Rider C# backend action was found. The
actual action is selected by the presenter callback above. The script must be passed to
`LocalPlaybackRunner.runScript(project, script)` by the test plugin and
wrapped by a harness timeout. Setting `idea.is.integration.test=true` alone
does not run a `.ijperf` file. In the per-project process, P3 support injects
`-Dtestscript.filename=<systemDir>/test_script.txt` for integration-test
bookkeeping, but the installed performance plugin does not automatically
execute that file.

The installed Rider build exposes a selection prerequisite for the playback
route. A bounded run reached `%goto 17 20`, then logged an `AssertionError` at
`GoToCommand.java:63` because
`FileEditorManager.getSelectedTextEditor()` was null; the adjacent log entry
reported that `_rider.cs` could not be focused while the current composite was
the `What's New` editor. Opening a text editor and receiving a non-null
`Editor` is therefore insufficient in Rider's remote frontend. The driver must
call `FileEditorManager.setSelectedEditor(file, "text-editor")` on the EDT
after opening the file, then verify `getSelectedTextEditor()` before running
`%goto`, `%altEnter`, or `createAndShowPopup`. The selected editor must be an
`EditorEx`, and `PsiManager.findFile(file)` must return the C# PSI file from
the loaded Rider project. If selection or PSI membership remains unavailable,
the driver must fail within its timeout rather than retrying unbounded
playback. A direct in-process `calcCachedIntentions` fallback can record the
cache control, but it must not be used to claim that the backend quick-fix was
invoked.

The harness must also gate the quick-fix assertions on a stable Rider project
model. In the installed run, the solution initially loaded all nine generated
projects and enabled Roslyn, but the Unity solution refresh selected the Unity
8.0.318 CLI while Rider had no usable MSBuild tool (`BuildToolContainer:
Discover found 0 build tools`). Rider then logged `MSBuild is required to load
project ... but it was not found`, removed all nine projects, removed the
assembly references (including `Houtamelo.Spire`), and opened `_rider.cs` from
`Miscellaneous Files`; its daemon state was `HIDDEN` because the file was not
included in compilation. A test must record the selected project, compilation
membership, daemon visibility, and analyzer reference before inspecting a
popup. This is an environment failure, not evidence that the analyzer or code
fix is absent. The bounded failure artifact should include the relevant
toolset/MSBuild diagnostics and must not retry an Alt+Enter route against a
Miscellaneous Files document.

The immediate reproduction is deterministic: running the Unity bundled
`dotnet` from `tmp/unity-asmdefs` exits with SDK-resolution error 145 because
the repository `global.json` requests SDK `10.0.100`, while that Unity install
contains only SDK `8.0.318`. Running the bundled `MSBuild.dll` directly works,
but Rider's normal CLI discovery does not bypass `global.json`. The harness
must therefore either launch the disposable solution with a compatible
project-local SDK selection or explicitly select a compatible Rider/user
MSBuild tool. The chosen route and the resolved SDK/MSBuild path belong in the
run artifact; silently falling back to a miscellaneous file is a failed
bootstrap.

The installed Rider settings use these ReSharper-host keys to select the
toolset in an isolated profile:

```xml
<s:String x:Key="/Default/Environment/Hierarchy/Build/BuildTool/CustomBuildToolPath/@EntryValue">{MSBUILD_DLL}</s:String>
<s:String x:Key="/Default/Environment/Hierarchy/Build/BuildTool/DotNetCliExePath/@EntryValue">{DOTNET_CLI}</s:String>
```

The user profile proves the pair
`/home/houtamelo/.dotnet/sdk/11.0.100-preview.3.26207.106/MSBuild.dll` and
`/home/houtamelo/.dotnet/dotnet`; Rider logs then show `Discover found 1` and
`DotNetCoreMsBuildProvider Found MSBuild in SDK`. The integration harness may
seed only these two keys into its disposable
`resharper-host/GlobalSettingsStorage.DotSettings`; it must not reuse the
production profile. The test result should report the selected paths and the
`Discover found 1`/project-membership evidence.

The Unity plugin can overwrite these settings after startup: an observed run
first discovered the configured user MSBuild, then
`RiderDotNetActiveRuntimeHost` switched the active CLI to Unity's `dotnet`,
cleared the custom path, and removed the projects when SDK resolution failed.
The durable fixture control is a disposable project-local `global.json` that
requests the installed Unity SDK `8.0.318` (or a solution copy outside the
repository's root `global.json`). This keeps Unity's selected CLI valid while
leaving production repository settings untouched. A test using the settings
keys alone must assert that they remain effective after the Unity runtime
handshake; otherwise it is only a startup smoke control.

A bounded rerun with the disposable `8.0.318` global.json reached the
required gate: all nine projects were added to the model, Roslyn became
enabled, `_rider.cs` was associated with `Spire.UnityAsmdefRuntimeAuto`, and
the daemon reached `UP_TO_DATE`. This is the minimum evidence that a later
empty popup is an analyzer/code-fix result rather than a failed project load.

One additional Rider-specific boundary is now verified from the installed
classes: the performance plugin's `%altEnter` implementation calls the static
IntelliJ `ShowIntentionActionsHandler.calcCachedIntentions` path. Rider's
remote C# menu is implemented separately by
`RiderShowIntentionActionsHandler` and `AltEnterActionsExtension`.

The supported build-262 sequence is
`RiderShowIntentionActionsHandler.createAndShowPopup(project, editor, psiFile,
null, onPopupReady, false)` on the EDT. Its presenter owns the factory
promise, creates a `PopupViewModel`, attaches the backend models, and shows the
popup. The callback must cast its `ListPopup` to `ListPopupImpl`, obtain the
Swing `com.intellij.ui.popup.list.ListPopupModel` from
`getList().getModel()`, and attach a `ListDataListener`. It scans each
`PopupItem` from `getElementAt(i)`, matches
`getDisplayName().getText()` to `Replace with SpireEnum<T>.From(value)`, and
calls `selectAndExecuteValue(item)` on the EDT. The initial scan is posted
with `ApplicationManager.getApplication().invokeLater` after the listener is
attached; later `contentsChanged`/added/removed events cover backend items
that arrive after popup construction. The returned
`RiderPopupMenuController.PopupMenu` must be closed on the EDT after a bounded
timeout or missing-title result. The test waits off the EDT for the persisted
source and Unity recompile, and records the selected title and exact source
diff.

A direct `BulbMenuModelFactory.create(...)` probe is only a lower-level RD
control. Its `ReSharperPopupMenuModelFactory` sets the promise when
`RdPopupMenuModel.isPrepared` becomes true, then leaves the EDT in
`IRdDispatcherKt.pumpProtocolQueue` until the popup lifetime ends. The promise
can therefore wake a pooled waiter while the EDT is still pumping; a following
`Application.invokeAndWait` deadlocks. If the presenter cannot be used, all
model inspection and `PopupModel.executeItem` calls must be queued through the
project protocol's `IScheduler.queue(...)`, with the lifetime retained until
execution and terminated only after the bounded action result. The freeze dump
`tmp/unity-asmdefs/RiderDriver/logs/threadDumps-freeze-20261007-212330-RD-262.10968.170/report.txt`
records this exact EDT pump/background `invokeAndWait` deadlock. Neither route
may edit source directly or substitute a cached IntelliJ action list.

## Category A: Real Rider and Unity solution bootstrap

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SPIRE016_RiderLoadsUnitySolution` | Ensure that the real Rider process opens the Unity-generated solution and reports SPIRE016 on the cast in `_rider.cs`. |
| `Detect_SPIRE016_AnalyzerDiscoveredFromUnityPackage` | Ensure that Rider discovers the analyzer DLL through the generated Unity project/package references and shows the SPIRE016 diagnostic without a manually-created Roslyn workspace. |
| `Detect_SPIRE016_PostBootstrapSourceRefresh` | Ensure that changing the already-generated `_rider.cs` after Unity solution generation refreshes the Rider document/project and produces SPIRE016. |
| `Detect_SPIRE016_ExactDiagnosticCaret` | Ensure that the highlighted source span and the caret target identify the `(RiderQuickFixKind)value` cast, not the method name, enum declaration, or return keyword. |
| `Detect_SPIRE016_IsolatedRiderConfig` | Ensure that Rider reports SPIRE016 while running with a disposable config/system directory, proving the test does not depend on a preconfigured production IDE. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_UnitySolutionOpenDoesNotMutateSource` | Ensure that opening and indexing the Unity solution leaves the pre-fix source text byte-for-byte unchanged. |
| `NoReport_UnitySolutionOpenDoesNotMutateProductionConfig` | Ensure that the test's isolated config contains the Rider state and production Rider settings remain unchanged. |
| `NoReport_UnrelatedUnityAsmdefs` | Ensure that indexing the other generated runtime/editor asmdefs does not create a false SPIRE016 finding in unrelated files. |
| `NoReport_RiderStartupLeavesNoStaleProcess` | Ensure that the bounded startup/teardown path closes Rider and its helper process when bootstrap or analysis fails. |
| `NoReport_SPIRE002FixtureHasNoCustomFix` | Ensure that the original fieldless `[EnforceInitialization]` struct fixture may report SPIRE002 but does not claim a SPIRE002 code fix, because no such provider exists. |

## Category B: Installed Rider editor targeting and action invocation

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_Playback_OpenFileTargetsRiderFixture` | Ensure that `%openFile` opens the actual `_rider.cs` document in Rider rather than a copied file or a test-side text buffer. |
| `Detect_Playback_GotoAndAssertCaret` | Ensure that `%goto` or `%moveCaret` places the caret on the SPIRE016 cast and `%assertCaretPosition` verifies the expected line/column. |
| `Detect_Playback_WaitForFinishedCodeAnalysis` | Ensure that the test waits for Rider's real code-analysis completion before inspecting the diagnostic or intention list. |
| `Detect_RiderBackendPopupFindsExactFromEnumFix` | Ensure that Rider's real `createAndShowPopup` presenter populates `ListPopupModel` from the C# backend and exposes the exact external-analyzer title at the highlighted cast. |
| `Detect_Playback_SaveDocumentsAndSettingsPersistsFix` | Ensure that `%saveDocumentsAndSettings` completes after the real Rider action and the changed document is visible on disk. |
| `Detect_Driver_RegisteredActionAndCodeEditorFallback` | (?) If the installed Starter/Driver path is selected, ensure that its real `invokeAction`/code-editor/caret calls reach Rider build 262 and can reproduce the same target and intention list. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_PlaybackWrongFileHasNoSpireFix` | Ensure that opening a non-C# project file or unrelated generated file does not expose the SPIRE016 fix. |
| `NoReport_PlaybackCaretOutsideDiagnosticHasNoSpireFix` | Ensure that invoking Alt+Enter with the caret away from the cast does not apply the SPIRE016 provider to an unrelated location. |
| `NoReport_PlaybackTitleMismatchDoesNotGuessFix` | Ensure that a wrong or partial intention title is treated as a missing action and does not invoke another Rider action by position. |
| `NoReport_PlaybackUsesRealMenuOrAction` | Ensure that the harness records the real Rider presenter/action invocation and does not edit the file by sending the expected replacement text directly. |
| `NoReport_DriverSmokeFailureIsBounded` | Ensure that an unsupported Driver API or endpoint fails within the configured timeout, closes Rider, and reports an explicit unsupported-route result rather than hanging. |

## Category C: SPIRE016 `FromEnumCodeFix` successful application

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_FromEnum_NonConstantCast_LocalMethod` | Ensure that a variable integer cast to the `[EnforceInitialization]` enum is reported as SPIRE016 and offers the FromEnum fix. |
| `Detect_FromEnum_ReturnStatement` | Ensure that the same non-constant cast in a return statement is reported and the fix is available at that cast. |
| `Detect_FromEnum_MethodArgument` | Ensure that a non-constant cast passed directly to a method argument is reported and the fix is available without first introducing a local. |
| `Detect_FromEnum_ExpressionBodiedMember` | Ensure that a cast in an expression-bodied member is reported and the Rider action can locate the enclosing cast syntax. |
| `Detect_FromEnum_EnumToEnumOperand` | Ensure that a cast from another enum is reported and the provider's documented behavior inserts the required integer conversion inside `SpireEnum<...>.From((int)source)`. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_FromEnum_ExactExpectedSource` | Ensure that applying the exact title changes only `(RiderQuickFixKind)value` to `SpireEnum<RiderQuickFixKind>.From(value)`, preserving whitespace, comments, and surrounding statements. |
| `NoReport_FromEnum_NoResidualCast` | Ensure that the saved Rider document and on-disk `_rider.cs` contain no remaining cast at the target site. |
| `NoReport_FromEnum_OnlyTargetSiteChanged` | Ensure that the source diff contains exactly the provider's replacement and no edits to other methods, generated files, or Unity project metadata. |
| `NoReport_FromEnum_ReanalysisClearsDiagnostic` | Ensure that a fresh Rider analysis after the fix contains no SPIRE016 diagnostic at the former location. |
| `NoReport_FromEnum_SecondAltEnterHasNoCustomFix` | Ensure that reopening the intention list after the fix does not offer `Replace with SpireEnum<T>.From(value)` again at that site. |

## Category D: Analyzer absence and discovery boundary

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_AnalyzerPresentBeforeRemoval` | Ensure that the control project with the analyzer DLL present reports SPIRE016 before the missing-analyzer variants are exercised. |
| `Detect_AnalyzerReappearsAfterCleanRiderRestart` | Ensure that restoring the analyzer reference and reopening the real Rider project restores SPIRE016 discovery after a clean restart. |
| `Detect_AnalyzerReferenceUsesGeneratedProject` | Ensure that the positive project variant uses the generated Unity project reference, not a hidden harness-only analyzer injection. |
| `Detect_AnalyzerMissingVariantChangesOnlyDiscovery` | Ensure that the missing-analyzer variant reaches the editor and source file successfully so a no-diagnostic result is attributable to analyzer absence. |
| `Detect_AnalyzerReenabledAfterProjectRefresh` | (?) Ensure that re-adding the analyzer and refreshing/reloading the project restores the diagnostic within a bounded wait. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_MissingAnalyzer_DllAbsent` | Ensure that removing `Houtamelo.Spire.Analyzers.dll` from the disposable project/package prevents SPIRE016 from appearing in Rider. |
| `NoReport_MissingAnalyzer_ProjectReferenceAbsent` | Ensure that leaving the DLL on disk but removing its generated analyzer reference also prevents SPIRE016, proving Rider is using project discovery. |
| `NoReport_MissingAnalyzer_NoCustomQuickFix` | Ensure that Alt+Enter at the cast contains no Spire `FromEnum` action when the analyzer is absent. |
| `NoReport_MissingAnalyzer_SourceUnchanged` | Ensure that attempting the missing-analyzer path does not modify the source file. |
| `NoReport_MissingAnalyzer_UnityCompileControl` | Ensure that the missing-analyzer control still opens and compiles through the existing Unity pipeline; a no-diagnostic result must not be confused with a broken project. |

## Category E: Code-fix assembly absence and provider registration

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_MissingCodeFix_AnalyzerStillReports` | Ensure that a disposable project with the analyzer DLL present but `Houtamelo.Spire.CodeFixes.dll` absent still reports SPIRE016. |
| `Detect_MissingCodeFix_RiderLoadsProject` | Ensure that the missing-CodeFix variant opens and reaches the cast so the absence is isolated to provider registration. |
| `Detect_MissingCodeFix_RiderKeepsDiagnosticAfterAltEnter` | Ensure that invoking Alt+Enter in the missing-CodeFix variant leaves SPIRE016 and the original cast intact. |
| `Detect_MissingCodeFix_ReaddingAssemblyRestoresTitle` | (?) Ensure that adding the CodeFixes assembly and reloading the disposable project restores the exact FromEnum title. |
| `Detect_MissingCodeFix_ProviderTitleIsStable` | Ensure that the positive control uses the exact title `Replace with SpireEnum<T>.From(value)` rather than a localized or position-based menu choice. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_MissingCodeFix_NoFromEnumTitle` | Ensure that the missing-CodeFix Alt+Enter list contains no `Replace with SpireEnum<T>.From(value)` action. |
| `NoReport_MissingCodeFix_NoSourceEdit` | Ensure that the missing-CodeFix path cannot silently perform the expected text replacement through the test harness. |
| `NoReport_MissingCodeFix_NoGeneratedFileEdit` | Ensure that neither Unity-generated project files nor generated source are changed while the CodeFix provider is absent. |
| `NoReport_MissingCodeFix_NativeActionsNotMisidentified` | Ensure that a built-in Rider inspection action is not counted as the Spire provider. |
| `NoReport_MissingCodeFix_CompileUsesOriginalSource` | Ensure that any Unity recompile in this variant compiles the original cast source and does not report a false successful fix. |

## Category F: Provider negative cases and no-fix-after-change behavior

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_FromEnum_ConstantInvalidCastDiagnostic` | Ensure that a constant invalid cast still produces the SPIRE016 diagnostic in real Rider. |
| `Detect_FromEnum_NamedMemberControl` | Ensure that a separate valid named-member use remains available as a source control while the constant invalid cast is highlighted. |
| `Detect_FromEnum_FixableAndNonFixableSitesTogether` | Ensure that one file can contain a non-constant fixable cast and a constant non-fixable cast, and Rider associates the provider only with the correct diagnostic site. |
| `Detect_FromEnum_CaretSwitchesBetweenSites` | Ensure that moving the real editor caret between the two sites changes the intention list accordingly. |
| `Detect_FromEnum_NonFixableDiagnosticRemains` | Ensure that applying the fix at the non-constant site does not remove or rewrite the independent constant diagnostic. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_FromEnum_ConstantCastHasNoFromFix` | Ensure that the constant-cast diagnostic does not offer `Replace with SpireEnum<T>.From(value)` because the provider intentionally skips `IsConstant=true`. |
| `NoReport_FromEnum_AfterFixNoDiagnosticAtFormerSite` | Ensure that the former non-constant site has no SPIRE016 diagnostic after applying and reanalyzing the fix. |
| `NoReport_FromEnum_AfterFixNoRepeatedAction` | Ensure that repeated Alt+Enter at the fixed site cannot apply the same FromEnum action again. |
| `NoReport_FromEnum_NamedMemberIsNotFlagged` | Ensure that using `RiderQuickFixKind.Active` or `.Inactive` directly is not treated as an invalid cast. |
| `NoReport_FromEnum_CaretOutsideSitesDoesNotChangeSource` | Ensure that moving the caret away from both diagnostics and invoking actions leaves the source unchanged. |

## Category G: Exact persistence and Unity recompile verification

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_UnityCompileBeforeFixContainsSPIRE016` | Ensure that the pre-fix Unity/Rider control records the expected SPIRE016 diagnostic and the original cast source. |
| `Detect_UnityRecompileStartsAfterRiderSave` | Ensure that saving the real Rider document causes the Unity project to observe the changed timestamp/content and begin its normal script recompilation. |
| `Detect_UnityRecompileUsesChangedSource` | Ensure that the Unity compile/reload evidence corresponds to `SpireEnum<RiderQuickFixKind>.From(value)`, not merely a changed editor buffer. |
| `Detect_RiderReanalysisAfterUnityRefresh` | Ensure that a Unity refresh/recompile does not restore the old cast or stale diagnostic in Rider. |
| `Detect_RestartRiderOpensPersistedFixedSource` | Ensure that closing and reopening the real Rider project opens the persisted fixed source. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_UnityRecompileAfterFix` | Ensure that the Unity recompile/reload completes without SPIRE016 errors after the accepted fix. |
| `NoReport_FixedSourceSurvivesRiderRestart` | Ensure that a clean Rider restart with the same disposable project retains the exact fixed source on disk. |
| `NoReport_FixedSourceSurvivesUnityRefresh` | Ensure that Unity refresh and generated-project regeneration do not reintroduce the original cast. |
| `NoReport_SecondCompileHasNoSPIRE016` | Ensure that a second real Unity/Rider compile has no residual SPIRE016 diagnostic and no repeated custom fix. |
| `NoReport_RecompileDoesNotTouchUnrelatedAssets` | Ensure that the quick-fix/recompile cycle changes only `_rider.cs` and the expected disposable Unity/Rider state. |

## Category H: Bounded failure and artifact evidence

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_MissingProjectPathFailsExplicitly` | Ensure that an invalid solution/project path produces a bounded, actionable harness failure rather than opening a different project. |
| `Detect_UnsupportedDriverRouteFailsExplicitly` | (?) Ensure that an unavailable Driver/endpoint is reported as unsupported for Rider build 262 and does not silently fall back to fake editor operations. |
| `Detect_QuickFixTimeoutCapturesRiderEvidence` | Ensure that a timeout while waiting for the menu or analysis captures Rider logs, the isolated config path, source path, and a screenshot before teardown. |
| `Detect_UnityRecompileTimeoutCapturesUnityEvidence` | Ensure that a Unity recompile timeout captures the Unity log/compile evidence and exact source diff. |
| `Detect_LicenseOrStartupBlockIsActionable` | Ensure that an unavailable valid Rider license or startup dialog is reported as an environment prerequisite failure, with no license bypass or indefinite wait. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SuccessArtifactsContainExactSourceDiff` | Ensure that a successful run stores the before/after source text or diff, the selected title, diagnostic ID, and project path. |
| `NoReport_SuccessArtifactsIdentifyRiderBuild` | Ensure that the result records Rider `2026.2.3.1` / `RD-262.10968.170` and the selected automation route. |
| `NoReport_FailureTeardownIsBounded` | Ensure that all expected Rider/Unity processes are stopped and the test returns within its global timeout after any negative case. |
| `NoReport_IsolatedStateIsRecoverable` | Ensure that disposable config/project copies live under the project-local `tmp/` directory and can be removed without touching production IDE state. |
| `NoReport_NoFakeRoslynProviderPath` | Ensure that the integration result cannot pass by invoking `FromEnumCodeFix` through the existing generic Roslyn provider tests instead of Rider. |

The primary external references for the harness contract are JetBrains'
[Roslyn analyzer documentation](https://www.jetbrains.com/help/rider/Using_NET_Compiler_Analyzers.html),
[quick-fix workflow](https://www.jetbrains.com/help/rider/Code_Analysis__Quick-Fixes.html),
[Starter/Driver integration-test introduction](https://plugins.jetbrains.com/docs/intellij/integration-tests-intro.html),
and [Driver UI testing](https://plugins.jetbrains.com/docs/intellij/integration-tests-ui.html).
