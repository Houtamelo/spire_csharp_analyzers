#nullable enable
#pragma warning disable CS8602, CS8604

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Houtamelo.Spire;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Spire.UnityVariantPayloadCore
{

    /// <summary>
    /// Native Editor coverage for the generated serialized-union drawer.
    ///
    /// The runner is intentionally update-driven. Unity's binding callbacks,
    /// native undo stack, and visual-tree rebuilds all occur between editor
    /// updates; a synchronous probe can therefore report a value before the
    /// actual host object has received the replacement union.
    /// </summary>
    public static class SerializedUnionInspectorTests
    {
        private const string ReportName = "unity-union-inspector-results.json";
        private const string AssetFolder = "Assets/InspectorFixture";
        private const string AssetPath = AssetFolder + "/InspectorCommand.asset";
        private const string SecondAssetPath = AssetFolder + "/InspectorCommandSecond.asset";
        private const string GenericAssetPath = AssetFolder + "/InspectorGeneric.asset";
        private const string TexturePath = AssetFolder + "/InspectorTexture.asset";
        private const string PrefabPath = AssetFolder + "/InspectorCommand.prefab";
        private const double WatchdogSeconds = 75.0;
        private const int UiWaitFrames = 240;
        private const int NativeEditCommitWaitFrames = 60;

        private static InspectorReport? _report;
        private static InspectorCommandAsset? _asset;
        private static InspectorCommandAsset? _secondAsset;
        private static InspectorGenericAsset? _genericAsset;
        private static Texture2D? _texture;
        private static GameObject? _prefabAsset;
        private static GameObject? _prefabInstance;
        private static InspectorCommandComponent? _prefabComponent;
        private static InspectorProbeWindow? _window;
        private static InspectorProbeWindow? _imguiWindow;
        private static double _startedAt;
        private static int _phase;
        private static int _lastLoggedPhase = -1;
        private static int _waitFrames;
        private static bool _finishing;

        private static TextField? _keyboardField;
        private static string _keyboardExpected = string.Empty;
        private static int _keyboardEventIndex;
        private static int _keyboardWaitFrames;
        private static string? _keyboardError;
        private static VisualElement? _keyboardTree;
        private static TextField? _copiedField;
        private static string _copiedExpected = string.Empty;
        private static int _copiedWaitFrames;
        private static bool _copiedEditSent;
        private static string? _copiedError;
        private static string _collectionEditDiagnostic = string.Empty;
        private static FloatField? _numericField;
        private static float _numericExpected;
        private static int _numericEditIndex;
        private static int _numericWaitFrames;
        private static string? _numericError;
        private static bool _imguiEditSent;
        private static int _imguiKeyIndex;
        private static int _imguiEditWaitFrames;
        private static string _imguiInitialSpeech = string.Empty;
        private static int _undoWaitFrames;
        private static bool _undoEditSent;
        private static int _undoCommitWaitFrames;
        private static TextField? _undoField;
        private static string _undoBefore = string.Empty;
        private static Exception? _undoError;
        private static SerializedObject? _undoExternalSerialized;
        private static int _undoExternalWaitFrames;
        private static int _managedRefWaitFrames;
        private static bool _managedRefEditSent;
        private static int _managedRefCommitWaitFrames;
        private static TextField? _managedRefField;
        private static string _managedRefBefore = string.Empty;
        private static int _collectionEditWaitFrames;

        private static readonly KeyStroke[] KeyboardEdits =
        {
            new KeyStroke('\0', KeyCode.Backspace),
            new KeyStroke('X', KeyCode.X),
            new KeyStroke('Y', KeyCode.Y),
        };

        private enum Phase
        {
            Setup,
            WaitUiToolkit,
            UiReadOnly,
            GenericStart,
            GenericWait,
            GenericCheck,
            GenericRestoreWait,
            UiKeyboardStart,
            UiKeyboardPump,
            UiSelectMove,
            UiWaitMove,
            UiSelectIdle,
            UiWaitIdle,
            UiWaitMoveAfterIdle,
            UiNumericStart,
            UiNumericPump,
            UiCopiedStart,
            UiCopiedWait,
            UiIdentityAndCollections,
            UiManagedReferenceStart,
            UiManagedReferenceWait,
            UiCollectionsWait,
            UiCollectionsEditWait,
            UiUndoAndExternal,
            UiUndoFieldWait,
            UiUndoExternalWait,
            PrefabStart,
            PrefabWait,
            PrefabEdit,
            MultiObjectStart,
            MultiObjectWait,
            MultiObjectCheck,
            MultiObjectFieldsWait,
            ImguiStart,
            ImguiWait,
            ImguiEdit,
            ImguiNativeRelease,
            ImguiNativeKey,
            ImguiVerify,
            Finish,
        }

        public static void Run()
        {
            if (_report != null && !_finishing)
                return;

            _report = new InspectorReport
            {
                probe = "SerializedUnionInspectorTests",
                unityVersion = Application.unityVersion,
                backend = "EditorMono",
                graphicsBackend = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                status = "running",
                cases = new List<InspectorCase>(),
                assertions = new List<string>(),
                failures = new List<string>(),
                diagnostics = new List<string>(),
                genericUnionSerializationSupported =
                    SerializedUnionNativeCapabilities.GenericUnionSerializationSupported,
                genericUnionSerializationApplicability =
                    SerializedUnionNativeCapabilities.GenericUnionSerializationApplicability,
            };
            _startedAt = EditorApplication.timeSinceStartup;
            _phase = (int)Phase.Setup;
            _lastLoggedPhase = -1;
            _waitFrames = 0;
            _finishing = false;
            RunCase("NativeInput_OptOutAndInvalidInput", CheckNativeInputSafety);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_BEGIN");
        }

        private static void Tick()
        {
            if (_report == null || _finishing)
                return;

            if (_lastLoggedPhase != _phase)
            {
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_PHASE phase=" + ((Phase)_phase));
                _lastLoggedPhase = _phase;
            }

            try
            {
                if (EditorApplication.timeSinceStartup - _startedAt > WatchdogSeconds)
                {
                    RecordFailure(
                        "Runner_Watchdog",
                        new TimeoutException(
                            "The Inspector update-driven runner exceeded " +
                            WatchdogSeconds.ToString("F0", CultureInfo.InvariantCulture) + " seconds."));
                    _phase = (int)Phase.Finish;
                }

                switch ((Phase)_phase)
                {
                    case Phase.Setup:
                        SetupFixture();
                        _phase = (int)Phase.WaitUiToolkit;
                        _waitFrames = 0;
                        break;
                    case Phase.WaitUiToolkit:
                        WaitForUiToolkit();
                        break;
                    case Phase.UiReadOnly:
                        RunCase("OpenDoesNotMutateActual", CheckUiOpenDoesNotMutateActual);
                        if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
                        {
                            _phase = (int)Phase.GenericStart;
                        }
                        else
                        {
                            RecordNotApplicable(
                                "ClosedGenericKindControl",
                                "Closed-generic Inspector serialization is not applicable on Unity 2022.");
                            _phase = (int)Phase.UiKeyboardStart;
                        }
                        _waitFrames = 0;
                        break;
                    case Phase.GenericStart:
                        StartGenericBinding();
                        _phase = (int)Phase.GenericWait;
                        _waitFrames = 0;
                        break;
                    case Phase.GenericWait:
                        WaitForGenericBinding();
                        break;
                    case Phase.GenericCheck:
                        RunCase("ClosedGenericKindControl", CheckClosedGenericKindControl);
                        RestoreMainBindingAfterGeneric();
                        _phase = (int)Phase.GenericRestoreWait;
                        _waitFrames = 0;
                        break;
                    case Phase.GenericRestoreWait:
                        WaitForMainBindingAfterGeneric();
                        break;
                    case Phase.UiKeyboardStart:
                        StartKeyboardEdit();
                        _phase = (int)Phase.UiKeyboardPump;
                        break;
                    case Phase.UiKeyboardPump:
                        PumpKeyboardEdit();
                        break;
                    case Phase.UiSelectMove:
                        StartMoveSelection();
                        _phase = (int)Phase.UiWaitMove;
                        _waitFrames = 0;
                        break;
                    case Phase.UiWaitMove:
                        WaitForMoveSelection();
                        break;
                    case Phase.UiSelectIdle:
                        StartIdleSelection();
                        _phase = (int)Phase.UiWaitIdle;
                        _waitFrames = 0;
                        break;
                    case Phase.UiWaitIdle:
                        WaitForIdleSelection();
                        break;
                    case Phase.UiWaitMoveAfterIdle:
                        WaitForMoveAfterIdle();
                        break;
                    case Phase.UiNumericStart:
                        StartNumericEdit();
                        _phase = (int)Phase.UiNumericPump;
                        break;
                    case Phase.UiNumericPump:
                        PumpNumericEdit();
                        break;
                    case Phase.UiCopiedStart:
                        StartCopiedEdit();
                        _phase = (int)Phase.UiCopiedWait;
                        break;
                    case Phase.UiCopiedWait:
                        PumpCopiedEdit();
                        break;
                    case Phase.UiIdentityAndCollections:
                        RunCase("ManagedReferenceAndUnityObjectIdentityInPreview", CheckManagedReferenceAndObjectIdentity);
                        StartManagedReferenceUndoCase();
                        _phase = (int)Phase.UiManagedReferenceWait;
                        _managedRefWaitFrames = 0;
                        break;
                    case Phase.UiManagedReferenceStart:
                        StartManagedReferenceUndoCase();
                        _phase = (int)Phase.UiManagedReferenceWait;
                        _managedRefWaitFrames = 0;
                        break;
                    case Phase.UiManagedReferenceWait:
                        WaitForManagedReferenceUndoCase();
                        _waitFrames = 0;
                        break;
                    case Phase.UiCollectionsWait:
                        WaitForCollections();
                        break;
                    case Phase.UiCollectionsEditWait:
                        WaitForCollectionEdits();
                        break;
                    case Phase.UiUndoAndExternal:
                        StartUndoRedoAndExternalChange();
                        break;
                    case Phase.UiUndoFieldWait:
                        PumpUndoRedoAndExternalChange();
                        break;
                    case Phase.UiUndoExternalWait:
                        WaitForExternalUndoRefresh();
                        break;
                    case Phase.PrefabStart:
                        StartPrefabBinding();
                        _phase = (int)Phase.PrefabWait;
                        _waitFrames = 0;
                        break;
                    case Phase.PrefabWait:
                        WaitForPrefabBinding();
                        break;
                    case Phase.PrefabEdit:
                        RunCase("PrefabOverrideIsRecorded", CheckPrefabOverrideAndEdit);
                        _phase = (int)Phase.MultiObjectStart;
                        break;
                    case Phase.MultiObjectStart:
                        StartMultiObjectEditing();
                        _phase = (int)Phase.MultiObjectWait;
                        _waitFrames = 0;
                        break;
                    case Phase.MultiObjectWait:
                        WaitForMultiObjectBinding();
                        break;
                    case Phase.MultiObjectCheck:
                        CheckMultiObjectMixedAndPrepareFields();
                        _phase = (int)Phase.MultiObjectFieldsWait;
                        _waitFrames = 0;
                        break;
                    case Phase.MultiObjectFieldsWait:
                        WaitForMultiObjectFields();
                        break;
                    case Phase.ImguiStart:
                        StartImguiProbe();
                        _phase = (int)Phase.ImguiWait;
                        _waitFrames = 0;
                        break;
                    case Phase.ImguiWait:
                        WaitForImguiProbe();
                        break;
                    case Phase.ImguiEdit:
                        StartImguiEdit();
                        break;
                    case Phase.ImguiNativeRelease:
                        QueueImguiNativeRelease();
                        break;
                    case Phase.ImguiNativeKey:
                        QueueImguiNativeKey();
                        break;
                    case Phase.ImguiVerify:
                        VerifyImguiEdit();
                        break;
                    case Phase.Finish:
                        Finish();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown Inspector runner phase " + _phase + ".");
                }
            }
            catch (Exception exception)
            {
                RecordFailure("Runner_Phase_" + ((Phase)_phase), exception);
                _phase = (int)Phase.Finish;
            }
        }

        private static void SetupFixture()
        {
            RunCase("FixtureSetup", () =>
            {
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP DeleteFixtureAssets start");
                DeleteFixtureAssets();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP DeleteFixtureAssets done");
                EnsureFolder(AssetFolder);

                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "InspectorFixtureTexture",
                };
                _texture.SetPixel(0, 0, Color.magenta);
                _texture.SetPixel(1, 0, Color.magenta);
                _texture.SetPixel(0, 1, Color.magenta);
                _texture.SetPixel(1, 1, Color.magenta);
                _texture.Apply();
                AssetDatabase.CreateAsset(_texture, TexturePath);
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP texture asset created");

                var shared = new InspectorNode { label = "shared", value = 7 };
                shared.next = shared;
                _asset = CreateAsset(
                    AssetPath,
                    InspectorCommand.Speak("hello"),
                    InspectorCommand.Speak("left"),
                    InspectorCommand.Speak("right"),
                    shared);
                _asset.sharedTexture = _texture;
                _asset.ordinaryField = "ordinary";
                _asset.commands = new[]
                {
                    InspectorCommand.Move(new Vector2(1f, 2f), 3f),
                    InspectorCommand.Idle(),
                };
                _asset.commandList = new List<InspectorCommand>
                {
                    InspectorCommand.Move(new Vector2(4f, 5f), 6f),
                    InspectorCommand.Idle(),
                };
                _asset.sharedNode = shared;
                _asset.secondSharedNode = shared;
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP native asset created path=" + AssetPath);

                _secondAsset = CreateAsset(
                    SecondAssetPath,
                    InspectorCommand.Move(new Vector2(10f, 11f), 12f),
                    InspectorCommand.Move(new Vector2(20f, 21f), 22f),
                    InspectorCommand.Move(new Vector2(30f, 31f), 32f),
                    new InspectorNode { label = "second", value = 9 });
                _secondAsset.sharedTexture = _texture;
                _secondAsset.ordinaryField = "second";
                _secondAsset.commands = new[] { InspectorCommand.Move(new Vector2(8f, 9f), 10f) };
                _secondAsset.commandList = new List<InspectorCommand> { InspectorCommand.Move(new Vector2(6f, 7f), 8f) };
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP native asset created path=" + SecondAssetPath);

                if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
                {
                    _genericAsset = ScriptableObject.CreateInstance<InspectorGenericAsset>();
                    _genericAsset.generic = InspectorGeneric<int>.Empty();
                    AssetDatabase.CreateAsset(_genericAsset, GenericAssetPath);
                    EditorUtility.SetDirty(_genericAsset);
                }

                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP CreatePrefab start");
                CreatePrefab();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP CreatePrefab done");
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP AssetDatabase.SaveAssets start");
                AssetDatabase.SaveAssets();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP AssetDatabase.SaveAssets done");
                AssetDatabase.Refresh();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP AssetDatabase.Refresh done");
                Require(File.Exists(ProjectPath(AssetPath)), "The Inspector ScriptableObject asset was not persisted.");
                if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
                    Require(File.Exists(ProjectPath(GenericAssetPath)), "The closed-generic Inspector asset was not persisted.");
                Require(File.Exists(ProjectPath(PrefabPath)), "The Inspector prefab asset was not persisted.");

                var beforeYaml = ReadAssetText(AssetPath);
                var beforeId = SerializedPayloadId(new SerializedObject(_asset), "command");
                var beforeUndoGroup = Undo.GetCurrentGroup();
                _window = ScriptableObject.CreateInstance<InspectorProbeWindow>();
                _window.InitialUndoGroup = beforeUndoGroup;
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP window BindUi start");
                _window.BindUi(_asset);
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP window BindUi done");
                _window.Show();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP window Show done");
                _window.Focus();
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP window Focus done");
                _window.InitialYaml = beforeYaml;
                _window.InitialPayloadId = beforeId;
            });
        }

        private static void WaitForUiToolkit()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_window != null && _window.UiReady)
            {
                _phase = (int)Phase.UiReadOnly;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                var hierarchy = _window?.DescribeHierarchy() ?? "window=null";
                var drawers = DescribeMatchingDrawerTypes<InspectorCommand>();
                var diagnostic = "UIToolkit_RenderedDrawer hierarchy=" + hierarchy +
                    ";matching-drawers=" + drawers;
                _report?.diagnostics.Add(diagnostic);
                RecordFailure(
                    "UIToolkit_RenderedDrawer",
                    new UnverifiedException(
                        "The real InspectorElement did not expose the generated InspectorCommand controls within " +
                        UiWaitFrames + " editor updates. " + (_window?.DescribeUi() ?? "window=null") +
                        "; matching-drawers=" + drawers));
                _phase = (int)Phase.Finish;
            }
        }

        private static void CheckUiOpenDoesNotMutateActual()
        {
            Require(_window != null && _asset != null, "The UI Toolkit window or asset was unavailable.");
            var hasInspectorElement = false;
            _window!.Root.Query<InspectorElement>().ForEach(_ => hasInspectorElement = true);
            Require(hasInspectorElement, "The probe did not use a native InspectorElement.");
            RunCase("KindAndPayloadColumnsAlign_Speak", () => _window.RequireAlignedFieldColumns("command", "speech"));
            var commandRoot = _window.FindUnionRootFoldout("command");
            var copiedLeftRoot = _window.FindUnionRootFoldout("copiedLeft");
            Require(commandRoot != null && copiedLeftRoot != null,
                "Named union root Foldouts were not registered for sibling union fields.");
            Require(commandRoot!.text == _window.Serialized.FindProperty("command")!.displayName,
                "The command union root Foldout did not use the actual SerializedProperty display name.");
            Require(copiedLeftRoot!.text == _window.Serialized.FindProperty("copiedLeft")!.displayName,
                "The copiedLeft union root Foldout did not use the actual SerializedProperty display name.");
            Require(string.IsNullOrEmpty((commandRoot as IBindable)?.bindingPath),
                "The union root Foldout must not bind its expansion boolean directly to the union property.");
            Require(HasGeneratedDrawerFor<InspectorCommand>(),
                "TypeCache did not discover a CustomPropertyDrawer for InspectorCommand.");
            Require(_window.FindTextField("command", "speech") != null,
                "The generated drawer did not expose the public payload.value.speech control.");
            Require(_window.FindKindSelector("command") != null,
                "The generated drawer did not expose the native serialized kind selector.");
            Require(_window.KindSelectorLabel("command") == "Kind",
                "The generated union selector must be labelled Kind.");
            var kindChoices = _window.KindChoices("command");
            Require(kindChoices.Length == 4 &&
                kindChoices.All(choice => new[] { "Idle", "Speak", "Move", "ManagedRefNode" }.Contains(choice, StringComparer.Ordinal)),
                "The native kind selector contained a non-variant or missing choice: " + string.Join(",", kindChoices));
            Require(_window.IsPayloadValuePropertyAvailable("command", "speech"),
                "_serializedPayload.value.speech was not a native relative SerializedProperty path.");
            Require(!_window.HasSyntheticValueControl("command"),
                "The generated Inspector still rendered a synthetic Payload.value control.");
            Require(_window.ImmediatePayloadFieldCount("command") == 1,
                "The Speak payload field was not rendered as a direct sibling of Kind.");
            _window.RequireBoundControlsEditable("command");

            if (_asset.command.kind != InspectorCommand.Kind.Speak)
                throw new InvalidOperationException("The initial command kind changed while opening the Inspector.");
            Require(_asset.command.speak.speech == "hello",
                "The initial speech changed while opening the Inspector.");
            Require(ReadAssetText(AssetPath) == _window.InitialYaml,
                "Opening/layout/repaint changed the serialized YAML before an edit.");
            Require(SerializedPayloadId(_window.Serialized, "command") == _window.InitialPayloadId,
                "Opening/layout/repaint changed the active managed-reference ID.");
            Require(!EditorUtility.IsDirty(_asset),
                "Opening/layout/repaint marked the actual asset dirty.");
            Require(Undo.GetCurrentGroup() == _window.InitialUndoGroup,
                "Opening/layout/repaint created an Undo group.");
            Require(_window.NewPreviewControlsAreEnabled(),
                "A generated preview parent disabled one or more native payload controls.");
            _window.RequirePreviewHideFlagsDoNotDisableEditing();
            Observe("native InspectorElement + PropertyField;payload.value=speech;kind=Speak;yaml/id/undo=stable");
            CheckNamedRootFoldoutState(commandRoot);
        }

        private static void CheckNamedRootFoldoutState(Foldout commandRoot)
        {
            Require(_window != null && _asset != null, "The Inspector window or asset was unavailable.");
            var commandProperty = _window!.Serialized.FindProperty("command");
            Require(commandProperty != null, "The command SerializedProperty was unavailable.");
            var beforeYaml = ReadAssetText(AssetPath);
            var beforeCommand = _asset!.command;

            commandRoot.value = false;
            _window.Serialized.Update();
            Require(!commandProperty!.isExpanded, "Collapsing the union root Foldout did not persist isExpanded on the actual SerializedProperty.");
            Require(commandRoot.contentContainer.style.display == DisplayStyle.None,
                "Collapsing the union root Foldout did not hide its Variant/value controls.");
            Require(_asset.command.kind == beforeCommand.kind && ReadAssetText(AssetPath) == beforeYaml,
                "Collapsing the union root Foldout mutated union data.");

            commandRoot.value = true;
            _window.Serialized.Update();
            Require(commandProperty.isExpanded, "Reopening the union root Foldout did not restore isExpanded on the actual SerializedProperty.");
            Require(commandRoot.contentContainer.style.display != DisplayStyle.None,
                "Reopening the union root Foldout did not restore its Variant/value controls.");

            Require(_window.SetKind("command", "Move"), "The command union could not select Move for root Foldout rebuild coverage.");
            var rebuiltRoot = _window.FindUnionRootFoldout("command");
            Require(rebuiltRoot != null && rebuiltRoot.value,
                "The named root Foldout did not retain expansion through a variant rebuild.");
            Require(_asset.command.kind == InspectorCommand.Kind.Move,
                "The variant rebuild did not update the actual command union.");

            _asset.command = beforeCommand;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            _window.RebindUi(_asset);
            var reboundProperty = _window.Serialized.FindProperty("command");
            Require(reboundProperty != null && reboundProperty.isExpanded,
                "The command union expansion state was not retained through SerializedObject rebind.");
        }

        private static void StartGenericBinding()
        {
            Require(_genericAsset != null, "The closed-generic Inspector asset was unavailable.");
            _window?.Close();
            _window = ScriptableObject.CreateInstance<InspectorProbeWindow>();
            _window.BindGenericUi(_genericAsset);
            _window.Show();
            _window.Focus();
        }

        private static void WaitForGenericBinding()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            var selector = _window?.FindKindSelector("generic");
            if (_window != null && _window.Root.panel != null && selector is EnumField)
            {
                _phase = (int)Phase.GenericCheck;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordFailure("ClosedGenericKindControl", new UnverifiedException(
                    "The closed-generic Inspector did not construct its native EnumField."));
                RestoreMainBindingAfterGeneric();
                _phase = (int)Phase.GenericRestoreWait;
                _waitFrames = 0;
            }
        }

        private static void CheckClosedGenericKindControl()
        {
            Require(_genericAsset != null && _window != null,
                "The closed-generic Inspector state was unavailable.");
            var selector = _window.FindKindSelector("generic");
            Require(selector is EnumField,
                "The closed-generic union kind selector was not a native EnumField.");
            Require(_window.KindSelectorLabel("generic") == "Kind",
                "The closed-generic union selector must be labelled Kind.");
            var enumField = (EnumField)selector!;
            Require(enumField.value != null,
                "The closed-generic EnumField did not have a runtime enum value.");
            Require(enumField.value.GetType() == typeof(InspectorGeneric<int>.Kind),
                "The native EnumField used an open or unrelated Kind type: " +
                enumField.value.GetType().AssemblyQualifiedName);
            var choices = _window.KindChoices("generic");
            Require(choices.Length == 2 &&
                choices.All(choice => new[] { "Value", "Empty" }.Contains(choice, StringComparer.Ordinal)),
                "The closed-generic kind choices were not the declared variants: " + string.Join(",", choices));
            Require(_genericAsset.generic.kind == InspectorGeneric<int>.Kind.Empty,
                "The closed-generic fixture did not start in Empty.");
            Require(_window.SetKind("generic", "Value"),
                "The closed-generic EnumField did not accept its Value variant.");
            _window.Serialized.ApplyModifiedProperties();
            if (_genericAsset.generic.kind != InspectorGeneric<int>.Kind.Value)
                throw new InvalidOperationException("Selecting Value did not edit the closed-generic union.");
            var payload = _genericAsset.generic.value;
            Require(payload.value == 0,
                "The closed-generic Value payload did not initialize its unmanaged field to default.");
            Observe("closed-generic-kind=" + enumField.value.GetType().FullName + ";selection=Value;exception=false");
        }

        private static void RestoreMainBindingAfterGeneric()
        {
            Require(_asset != null && _window != null, "The main Inspector asset was unavailable after generic coverage.");
            _window.RebindUi(_asset);
        }

        private static void WaitForMainBindingAfterGeneric()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_window != null && _window.UiReady)
            {
                _phase = (int)Phase.UiKeyboardStart;
                _waitFrames = 0;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordFailure("ClosedGenericKindControl", new UnverifiedException(
                    "The main Inspector did not restore after closed-generic coverage."));
                _phase = (int)Phase.Finish;
            }
        }

        private static void StartKeyboardEdit()
        {
            _keyboardField = _window?.FindTextField("command", "speech");
            _keyboardExpected = _keyboardField?.value ?? string.Empty;
            _keyboardEventIndex = 0;
            _keyboardWaitFrames = 0;
            _keyboardError = null;
            _keyboardTree = _window?.FindDrawerTree("command");
            if (_keyboardField == null || !_window!.FocusTextField(_keyboardField))
                _keyboardError = "The rendered speech TextField could not receive native keyboard focus.";
        }

        private static void PumpKeyboardEdit()
        {
            if (_keyboardField == null || _window == null || _asset == null)
            {
                RecordFailure("TextFieldKeepsFocusAcrossEachCharacter", new UnverifiedException("Keyboard field setup was unavailable."));
                _phase = (int)Phase.UiSelectMove;
                return;
            }

            if (_keyboardEventIndex < KeyboardEdits.Length)
            {
                if (_keyboardError == null)
                {
                    Require(ReferenceEquals(_keyboardField, _window.FindTextField("command", "speech")),
                        "The focused TextField was rebuilt during an ordinary character edit.");
                    Require(_keyboardField.enabledInHierarchy && !_keyboardField.isReadOnly,
                        "The focused speech TextField was not editable in the rendered preview.");
                    Require(_window.IsTextFieldFocused(_keyboardField),
                        "The speech TextField lost focus before the next native keyboard event.");
                    var stroke = KeyboardEdits[_keyboardEventIndex];
                    _window.SendKey(stroke.character, stroke.keyCode);
                    _keyboardEventIndex++;
                    if (stroke.keyCode == KeyCode.Backspace)
                    {
                        if (_keyboardExpected.Length > 0)
                            _keyboardExpected = _keyboardExpected.Substring(0, _keyboardExpected.Length - 1);
                    }
                    else
                    {
                        _keyboardExpected += stroke.character;
                    }
                    _window.Serialized.ApplyModifiedProperties();
                    return;
                }

                _keyboardEventIndex = KeyboardEdits.Length;
            }

            _keyboardWaitFrames++;
            if (_keyboardWaitFrames < 2)
                return;

            RunCase("TextFieldKeepsFocusAcrossEachCharacter", () =>
            {
                if (_keyboardError != null)
                    throw new UnverifiedException(_keyboardError);
                var currentField = _window.FindTextField("command", "speech");
                if (!ReferenceEquals(_keyboardField, currentField))
                    RecordUiDiagnostic(
                        "TextFieldKeepsFocusAcrossEachCharacter",
                        _window,
                        "expected=" + _window.DescribeTextField(_keyboardField) +
                        ";actual=" + _window.DescribeTextField(currentField) +
                        ";expectedTree=" + _window.DescribeElement(_keyboardTree) +
                        ";actualTree=" + _window.DescribeElement(_window.FindDrawerTree("command")));
                Require(ReferenceEquals(_keyboardField, currentField),
                    "The ordinary field edit replaced the focused TextField control.");
                Require(_window.IsTextFieldFocused(_keyboardField),
                    "The focused speech control did not retain focus after keyboard edits.");
                Require(_keyboardField.cursorIndex >= 0 && _keyboardField.selectIndex >= 0,
                    "The native TextField caret/selection was not available after keyboard edits.");
                if (_asset.command.kind != InspectorCommand.Kind.Speak)
                    throw new InvalidOperationException("The text edit changed the active variant.");
                Require(_asset.command.speak.speech == _keyboardExpected,
                    "The native keyboard edits did not commit the expected speech value.");
                Require(ReferenceEquals(_keyboardTree, _window.FindDrawerTree("command")),
                    "An ordinary payload edit rebuilt the generated visual tree.");
                Observe("keyboard=Backspace/X/Y;focus=true;caret=true;tree-identity=true;value=" + _keyboardExpected);
            });
            _phase = (int)Phase.UiSelectMove;
        }

        private static void StartMoveSelection()
        {
            Require(_window != null && _asset != null, "The move selector window was unavailable.");
            Require(_window.SetKind("command", "Move"),
                "The native kind selector did not offer the real Move variant.");
            _window.Serialized.ApplyModifiedProperties();
        }

        private static void WaitForMoveSelection()
        {
            _waitFrames++;
            if (_window == null || _asset == null)
                throw new InvalidOperationException("The move selector state was lost.");

            _window.Serialized.Update();
            if (_asset.command.kind == InspectorCommand.Kind.Move &&
                _window.FindFloatField("command", "speed") != null &&
                _window.FindVector2Field("command", "destination") != null &&
                _window.AreFieldBoundsReady("command", "speed", "destination"))
            {
                RunCase("KindSelectorSwitchesVariant", () =>
                {
                    Require(_asset.command.kind == InspectorCommand.Kind.Move,
                        "Selecting Move did not replace the actual host union.");
                    Require(_window.FindFloatField("command", "speed") != null &&
                        _window.FindVector2Field("command", "destination") != null,
                        "Selecting Move did not bind its public payload fields.");
                    Require(!_window.HasSyntheticValueControl("command"),
                        "The Move Inspector still rendered a synthetic Payload.value control.");
                    Require(_window.ImmediatePayloadFieldCount("command") == 2,
                        "The Move payload fields were not rendered as direct siblings of Kind.");
                    _window.RequireAlignedFieldColumns("command", "speed", "destination");
                });
                _phase = (int)Phase.UiSelectIdle;
                _waitFrames = 0;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RunCase("KindSelectorSwitchesVariant", () =>
                {
                    Require(_asset.command.kind == InspectorCommand.Kind.Move,
                        "Selecting Move did not replace the actual host union.");
                });
                _phase = (int)Phase.UiNumericStart;
            }
        }

        private static void StartIdleSelection()
        {
            Require(_window != null, "The fieldless selector window was unavailable.");
            Require(_window.SetKind("command", "Idle"),
                "The native kind selector did not offer the real fieldless Idle variant.");
            _window.Serialized.ApplyModifiedProperties();
        }

        private static void WaitForIdleSelection()
        {
            _waitFrames++;
            if (_window == null || _asset == null)
                throw new InvalidOperationException("The fieldless selector state was lost.");
            _window.Serialized.Update();
            if (_asset.command.kind == InspectorCommand.Kind.Idle &&
                _window.FindTextField("command", "speech") == null &&
                _window.FindFloatField("command", "speed") == null &&
                _window.FindVector2Field("command", "destination") == null)
            {
                RunCase("FieldlessVariantHasNoSyntheticFields", () =>
                {
                    Require(_asset.command.kind == InspectorCommand.Kind.Idle,
                        "Selecting Idle did not replace the actual host union.");
                    Require(_window.FindTextField("command", "speech") == null &&
                        _window.FindFloatField("command", "speed") == null,
                        "The fieldless Idle variant kept stale payload controls.");
                    Require(!_window.HasSyntheticValueControl("command"),
                        "The fieldless Idle Inspector rendered a synthetic Payload.value control.");
                    Require(_window.ImmediatePayloadFieldCount("command") == 0,
                        "The fieldless Idle Inspector rendered a synthetic payload row.");
                    Observe("fieldless=Idle;synthetic-fields=false;structural-rebuild=true");
                });
                Require(_window.SetKind("command", "Move"),
                    "The native kind selector could not restore Move after fieldless coverage.");
                _window.Serialized.ApplyModifiedProperties();
                _phase = (int)Phase.UiWaitMoveAfterIdle;
                _waitFrames = 0;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordUiDiagnostic(
                    "FieldlessVariantHasNoSyntheticFields",
                    _window,
                    "kind=" + (_asset?.command.kind.ToString() ?? "<asset-null>") +
                    ";speech=" + (_window.FindTextField("command", "speech") != null) +
                    ";speed=" + (_window.FindFloatField("command", "speed") != null) +
                    ";destination=" + (_window.FindVector2Field("command", "destination") != null));
                RecordFailure("FieldlessVariantHasNoSyntheticFields", new UnverifiedException(
                    "The native selector did not rebuild to the fieldless Idle payload."));
                _phase = (int)Phase.UiWaitMoveAfterIdle;
                _waitFrames = 0;
            }
        }

        private static void WaitForMoveAfterIdle()
        {
            _waitFrames++;
            if (_window == null || _asset == null)
                throw new InvalidOperationException("The Move-after-Idle state was lost.");
            _window.Serialized.Update();
            if (_asset.command.kind == InspectorCommand.Kind.Move &&
                _window.FindFloatField("command", "speed") != null)
            {
                _phase = (int)Phase.UiNumericStart;
                _waitFrames = 0;
                return;
            }
            if (_waitFrames >= UiWaitFrames)
            {
                RecordUiDiagnostic(
                    "KindSelectorRestoresMove",
                    _window,
                    "kind=" + (_asset?.command.kind.ToString() ?? "<asset-null>"));
                RecordFailure("KindSelectorRestoresMove", new UnverifiedException(
                    "The native selector did not restore Move after fieldless coverage."));
                _phase = (int)Phase.UiNumericStart;
            }
        }

        private static void StartNumericEdit()
        {
            _numericField = _window?.FindFloatField("command", "speed");
            _numericExpected = 0f;
            _numericEditIndex = 0;
            _numericWaitFrames = 0;
            _numericError = null;
            if (_numericField == null)
                _numericError = "The rendered Move.speed FloatField was unavailable.";
            else if (!_numericField.enabledInHierarchy || _numericField.isReadOnly)
                _numericError = "The rendered Move.speed FloatField was disabled or read-only.";
            else
                _numericField.Focus();
        }

        private static void PumpNumericEdit()
        {
            if (_numericField == null || _window == null || _asset == null)
            {
                RecordUiDiagnostic("NumericFieldKeepsFocusAcrossEachEdit", _window, "numeric-field=null");
                RecordFailure("NumericFieldKeepsFocusAcrossEachEdit", new UnverifiedException("Numeric field setup was unavailable."));
                _phase = (int)Phase.UiCopiedStart;
                return;
            }

            var values = new[] { 1.25f, 2.5f, 4.75f };
            if (_numericEditIndex < values.Length)
            {
                if (_numericError == null)
                {
                    Require(ReferenceEquals(_numericField, _window.FindFloatField("command", "speed")),
                        "The numeric control was rebuilt during an ordinary value edit.");
                    Require(_numericField.enabledInHierarchy && !_numericField.isReadOnly,
                        "The numeric control became disabled while editing.");
                    _numericField.Focus();
                    _numericField.value = values[_numericEditIndex];
                    _numericExpected = values[_numericEditIndex];
                    _numericEditIndex++;
                    _window.Serialized.ApplyModifiedProperties();
                    return;
                }

                _numericEditIndex = values.Length;
            }

            _numericWaitFrames++;
            if (_numericWaitFrames < 2)
                return;

            RunCase("NumericFieldKeepsFocusAcrossEachEdit", () =>
            {
                if (_numericError != null)
                    throw new UnverifiedException(_numericError);
                Require(ReferenceEquals(_numericField, _window.FindFloatField("command", "speed")),
                    "The native numeric control was replaced during ordinary edits.");
                if (_asset.command.kind != InspectorCommand.Kind.Move)
                    throw new InvalidOperationException("Numeric editing changed the active variant.");
                Require(Math.Abs(_asset.command.move.speed - _numericExpected) < 0.0001f,
                    "The repeated native numeric edits did not reach the actual union.");
                Observe("FloatField=1.25/2.5/4.75;control-identity=true;active=Move");
            });
            _phase = (int)Phase.UiCopiedStart;
        }

        private static void StartCopiedEdit()
        {
            Require(_asset != null && _window != null, "The copied-value fixture was unavailable.");
            _asset.copiedLeft = InspectorCommand.Speak("left");
            _asset.copiedRight = _asset.copiedLeft;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            _window.RebindUi(_asset);
            _copiedField = null;
            _copiedExpected = "left";
            _copiedWaitFrames = 0;
            _copiedEditSent = false;
            _copiedError = null;
        }

        private static void PumpCopiedEdit()
        {
            _copiedWaitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (!_copiedEditSent && _copiedError == null && _window != null)
            {
                _copiedField = _window.FindTextField("copiedLeft", "speech");
                if (_copiedField != null && _window.PrepareTextFieldForEdit(_copiedField))
                {
                    _copiedExpected = _copiedField.value ?? "left";
                    Debug.Log(
                        "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT copied-before-send field=" +
                        _window.DescribeTextField(_copiedField) + ";value=" + _copiedField.value +
                        ";cursor=" + _copiedField.cursorIndex + ";select=" + _copiedField.selectIndex);
                    _window.SendKey('\0', KeyCode.End);
                    _window.SendKey('!', KeyCode.Alpha1);
                    _copiedExpected += "!";
                    _window.Serialized.ApplyModifiedProperties();
                    _copiedEditSent = true;
                    Debug.Log(
                        "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT copied-after-send field=" +
                        _window.DescribeTextField(_copiedField) + ";readOnly=" + _copiedField.isReadOnly +
                        ";host=" + (_asset.copiedLeft.kind == InspectorCommand.Kind.Speak
                            ? _asset.copiedLeft.speak.speech : "<non-speak>"));
                }
                else if (_copiedWaitFrames >= UiWaitFrames)
                {
                    _copiedError = "The copied-left native speech field was unavailable or not focusable after " +
                        UiWaitFrames.ToString(CultureInfo.InvariantCulture) + " editor updates.";
                }
                return;
            }
            if (!_copiedEditSent && _copiedError == null)
                return;
            if (_copiedEditSent && _copiedError == null && _window != null && _asset != null)
            {
                _window.Serialized.Update();
                var committed = _asset.copiedLeft.kind == InspectorCommand.Kind.Speak &&
                    _asset.copiedLeft.speak.speech == _copiedExpected;
                if (!committed && _copiedWaitFrames < UiWaitFrames)
                    return;
            }
            if (_copiedEditSent && _copiedError == null && _copiedWaitFrames >= UiWaitFrames)
            {
                Debug.Log(
                    "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT copied-timeout field=" +
                    (_window == null ? "<window-null>" : _window.DescribeTextField(_copiedField)) +
                    ";readOnly=" + (_copiedField == null ? "<null>" : _copiedField.isReadOnly.ToString()) +
                    ";host=" + (_asset == null || _asset.copiedLeft.kind != InspectorCommand.Kind.Speak
                        ? "<non-speak>" : _asset.copiedLeft.speak.speech));
            }
            if (_copiedWaitFrames < 3)
                return;

            RunCase("CopiedPreviewEditsAreIsolated", () =>
            {
                Require(_asset != null && _window != null, "The copied-value state was lost.");
                if (_copiedError != null)
                    RecordUiDiagnostic("CopiedPreviewEditsAreIsolated", _window, _copiedError);
                if (_copiedError != null)
                    throw new UnverifiedException(_copiedError);
                if (_asset!.copiedLeft.kind != InspectorCommand.Kind.Speak)
                    throw new InvalidOperationException("Editing the copied-left value changed its variant unexpectedly.");
                if (_asset.copiedRight.kind != InspectorCommand.Kind.Speak)
                    throw new InvalidOperationException("Editing one copied value changed the sibling variant.");
                Require(_asset.copiedLeft.speak.speech == _copiedExpected,
                    "The copied-left native text edit did not commit.");
                Require(_asset.copiedRight.speak.speech == "left",
                    "Editing copiedLeft changed copiedRight through a shared wrapper/cache.");
                Require(_window.FindTextField("copiedLeft", "speech") != null,
                    "The copied-left control disappeared after editing.");
                Observe("copied-left=" + _asset.copiedLeft.speak.speech + ";copied-right=" + _asset.copiedRight.speak.speech + ";isolated=true");
            });
            _phase = (int)Phase.UiIdentityAndCollections;
        }

        private static void CheckManagedReferenceAndObjectIdentity()
        {
            Require(_asset != null, "The identity fixture asset was unavailable.");
            var shared = _asset.sharedNode;
            var expectedTexture = _asset.sharedTexture;
            Require(shared != null, "The intentional shared managed-reference graph was not initialized.");
            Require(expectedTexture != null, "The intentional shared UnityEngine.Object was not initialized.");
            Require(ReferenceEquals(shared, _asset.secondSharedNode),
                "The two intentional host SerializeReference fields were not initially shared.");
            Require(shared!.next != null && ReferenceEquals(shared, shared.next),
                "The intentional managed-reference cycle was not initialized.");

            _asset.command = InspectorCommand.ManagedRefNode(shared, expectedTexture);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            if (_asset.command.kind != InspectorCommand.Kind.ManagedRefNode)
                throw new InvalidOperationException("The managed-reference identity case did not select its variant.");
            Require(ReferenceEquals(shared, _asset.command.managedRefNode.node),
                "Constructing the payload did not preserve the user managed-reference identity.");
            Require(ReferenceEquals(expectedTexture, _asset.command.managedRefNode.icon),
                "Constructing the payload did not preserve the UnityEngine.Object identity.");
            _window?.RebindUi(_asset);
            if (_window != null)
            {
                var command = _window.Serialized.FindProperty("command");
                var payload = command?.FindPropertyRelative("_serializedPayload")?.FindPropertyRelative("value");
                var node = payload?.FindPropertyRelative("_node");
                var icon = payload?.FindPropertyRelative("_icon");
                Require(node != null && ReferenceEquals(node.managedReferenceValue, shared),
                    "The native preview did not preserve the intentional SerializeReference node identity.");
                Require(icon != null && ReferenceEquals(icon.objectReferenceValue, expectedTexture),
                    "The native preview did not preserve the intentional UnityEngine.Object identity.");
            }
            Observe("SerializeReference=cycle/shared;UnityObject=sharedTexture;payload-cache=generated-only");
        }

        private static void StartManagedReferenceUndoCase()
        {
            Require(_asset != null && _window != null, "The managed-reference Undo fixture was unavailable.");
            var node = _asset.sharedNode;
            Require(node != null && _asset.sharedTexture != null,
                "The managed-reference Undo graph was not initialized.");
            node!.label = "node-before";
            node.next = node;
            _asset.secondSharedNode = node;
            _asset.command = InspectorCommand.ManagedRefNode(node, _asset.sharedTexture);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            _window.RebindUi(_asset);
            _window.Show();
            _window.Focus();
            _window.ExpandGeneratedPayloadFoldouts();
            _window.ExpandFoldout("node");
            _managedRefField = null;
            _managedRefEditSent = false;
            _managedRefCommitWaitFrames = 0;
            _managedRefBefore = node.label;
        }

        private static void WaitForManagedReferenceUndoCase()
        {
            _managedRefWaitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            _window?.ExpandFoldout("node");
            if (_window != null && _asset != null)
            {
                _managedRefField = _window.FindTextField("command", "label");
                if (_managedRefField != null && _window.PrepareTextFieldForEdit(_managedRefField))
                {
                    if (!_managedRefEditSent)
                    {
                        Undo.ClearAll();
                        Debug.Log(
                            "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT managed-reference-before-send field=" +
                            _window.DescribeTextField(_managedRefField) + ";value=" + _managedRefField.value +
                            ";cursor=" + _managedRefField.cursorIndex + ";select=" + _managedRefField.selectIndex);
                        _window.SendKey('\0', KeyCode.End);
                        _window.SendKey('!', KeyCode.Alpha1);
                        _window.Serialized.ApplyModifiedProperties();
                        _managedRefEditSent = true;
                        Debug.Log(
                            "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT managed-reference-after-send field=" +
                            _window.DescribeTextField(_managedRefField) + ";readOnly=" + _managedRefField.isReadOnly +
                            ";host=" + (_asset.sharedNode == null ? "<null>" : _asset.sharedNode.label));
                        return;
                    }

                    _window.Serialized.Update();
                    if (_asset.sharedNode == null || _asset.sharedNode.label != _managedRefBefore + "!")
                    {
                        _managedRefCommitWaitFrames++;
                        if (_managedRefCommitWaitFrames >= NativeEditCommitWaitFrames)
                        {
                            Debug.Log(
                                "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT managed-reference-timeout field=" +
                                _window.DescribeTextField(_managedRefField) + ";readOnly=" + _managedRefField.isReadOnly +
                                ";host=" + (_asset.sharedNode == null ? "<null>" : _asset.sharedNode.label));
                            RunCase("ManagedReferenceFieldUndoPreservesAliases", () =>
                                throw new InvalidOperationException(
                                    "The native managed-reference scalar edit did not commit after " +
                                    NativeEditCommitWaitFrames + " update frames."));
                            _phase = (int)Phase.UiCollectionsWait;
                            _waitFrames = 0;
                        }
                        return;
                    }

                    var field = _managedRefField;
                    RunCase("ManagedReferenceFieldUndoPreservesAliases", () =>
                    {
                        Require(_asset.sharedNode != null &&
                            ReferenceEquals(_asset.sharedNode, _asset.secondSharedNode) &&
                            ReferenceEquals(_asset.sharedNode, _asset.sharedNode.next),
                            "The managed-reference aliases/cycle were not present before the scalar edit.");
                        var editedNode = _asset.sharedNode;
                        Require(editedNode != null && editedNode.label == _managedRefBefore + "!",
                            "The native managed-reference scalar edit did not reach the host.");
                        Require(ReferenceEquals(editedNode, _asset.secondSharedNode) &&
                            ReferenceEquals(editedNode, editedNode.next),
                            "The scalar edit broke the host managed-reference aliases/cycle.");
                        Undo.FlushUndoRecordObjects();
                        Undo.PerformUndo();
                        _window.Serialized.Update();
                        var undoNode = _asset.sharedNode;
                        Require(undoNode != null && undoNode.label == _managedRefBefore,
                            "Undo did not restore the managed-reference scalar value.");
                        if (_asset.command.kind != InspectorCommand.Kind.ManagedRefNode)
                            throw new InvalidOperationException("Undo changed the managed-reference variant unexpectedly.");
                        Require(ReferenceEquals(undoNode, _asset.secondSharedNode) &&
                            ReferenceEquals(undoNode, undoNode.next) &&
                            ReferenceEquals(undoNode, _asset.command.managedRefNode.node),
                            "Undo did not preserve managed-reference aliases/cycle within the host.");
                        Undo.PerformRedo();
                        _window.Serialized.Update();
                        var redoNode = _asset.sharedNode;
                        Require(redoNode != null && redoNode.label == _managedRefBefore + "!",
                            "Redo did not restore the managed-reference scalar value.");
                        if (_asset.command.kind != InspectorCommand.Kind.ManagedRefNode)
                            throw new InvalidOperationException("Redo changed the managed-reference variant unexpectedly.");
                        Require(ReferenceEquals(redoNode, _asset.secondSharedNode) &&
                            ReferenceEquals(redoNode, redoNode.next) &&
                            ReferenceEquals(redoNode, _asset.command.managedRefNode.node),
                            "Redo did not preserve managed-reference aliases/cycle within the host.");
                        Require(ReferenceEquals(field, _window.FindTextField("command", "label")),
                            "Undo/Redo replaced the managed-reference scalar control unexpectedly.");
                        Observe("SerializeReference=scalar-label;Undo/Redo=restored;aliases=true;cycle=true;external-pointer-not-required=true");
                    });
                    _phase = (int)Phase.UiCollectionsWait;
                    _waitFrames = 0;
                    return;
                }
            }
            if (_managedRefEditSent &&
                (_managedRefField == null || _window == null || !_window.IsTextFieldFocused(_managedRefField)))
            {
                _managedRefCommitWaitFrames++;
                if (_managedRefCommitWaitFrames >= NativeEditCommitWaitFrames)
                {
                    Debug.Log(
                        "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT managed-reference-timeout field=" +
                        (_window == null ? "<window-null>" : _window.DescribeTextField(_managedRefField)) +
                        ";host=" + (_asset == null || _asset.sharedNode == null ? "<null>" : _asset.sharedNode.label));
                    RunCase("ManagedReferenceFieldUndoPreservesAliases", () =>
                        throw new InvalidOperationException(
                            "The native managed-reference scalar edit did not commit after " +
                            NativeEditCommitWaitFrames + " update frames."));
                    _phase = (int)Phase.UiCollectionsWait;
                    _waitFrames = 0;
                    return;
                }
            }
            if (_managedRefWaitFrames < UiWaitFrames)
                return;
            if (_window != null)
                RecordUiDiagnostic("ManagedReferenceFieldUndoPreservesAliases", _window,
                    "label-field=" + (_window.FindTextField("command", "label") != null));
            RunCase("ManagedReferenceFieldUndoPreservesAliases", () => throw new UnverifiedException(
                "The managed-reference scalar TextField did not become focusable after " +
                UiWaitFrames.ToString(CultureInfo.InvariantCulture) + " editor updates."));
            _phase = (int)Phase.UiCollectionsWait;
            _waitFrames = 0;
        }

        private static void CheckNestedAndCollectionPaths()
        {
            Require(_window != null, "The collection window was unavailable.");
            _window.ExpandFoldout("commands");
            _window.ExpandFoldout("commandList");
            if (!_window.HasBindingPath("commands.Array.data[0].*", "speed") ||
                !_window.HasBindingPath("commandList.Array.data[0].*", "speed"))
                RecordUiDiagnostic(
                    "NestedAndCollectionPathsBind",
                    _window,
                    "array/list-speed-path-missing;lists=" + _window.DescribeListViews());
            Require(_window.HasBindingPath("commands.Array.data[0].*", "speed"),
                "The native UI tree did not bind the first union array element payload path.");
            Require(_window.HasBindingPath("commandList.Array.data[0].*", "speed"),
                "The native UI tree did not bind the first union list element payload path.");
            var payload = _window.Serialized.FindProperty("command").FindPropertyRelative("_serializedPayload");
            Require(payload != null && payload.FindPropertyRelative("value") != null,
                "The active wrapper did not expose its public payload value through SerializedProperty.");
            var arraySpeed = _window.FindFloatField("commands.Array.data[0]", "speed");
            var listSpeed = _window.FindFloatField("commandList.Array.data[0]", "speed");
            Require(arraySpeed != null && arraySpeed.enabledInHierarchy && !arraySpeed.isReadOnly,
                "The first union array element speed control was not editable.");
            Require(listSpeed != null && listSpeed.enabledInHierarchy && !listSpeed.isReadOnly,
                "The first union list element speed control was not editable.");
            _collectionEditDiagnostic =
                "before-array=" + _window.DescribeFloatField(arraySpeed) +
                ";before-list=" + _window.DescribeFloatField(listSpeed) +
                ";array-prop=" + _window.DescribeSerializedProperty("commands.Array.data[0]._serializedPayload.value._speed") +
                ";list-prop=" + _window.DescribeSerializedProperty("commandList.Array.data[0]._serializedPayload.value._speed");
            arraySpeed!.value = 13f;
            listSpeed!.value = 14f;
            _collectionEditDiagnostic +=
                ";assigned-array=" + _window.DescribeFloatField(arraySpeed) +
                ";assigned-list=" + _window.DescribeFloatField(listSpeed);
            _window.Serialized.ApplyModifiedProperties();
            _collectionEditDiagnostic +=
                ";applied-array-prop=" + _window.DescribeSerializedProperty("commands.Array.data[0]._serializedPayload.value._speed") +
                ";applied-list-prop=" + _window.DescribeSerializedProperty("commandList.Array.data[0]._serializedPayload.value._speed");
            _collectionEditWaitFrames = 0;
            _phase = (int)Phase.UiCollectionsEditWait;
            throw new PendingCaseException();
        }

        private static void VerifyNestedAndCollectionPaths()
        {
            Require(_asset != null && _window != null, "The collection edit fixture was unavailable.");
            var arrayValue = _asset!.commands[0];
            var listValue = _asset.commandList[0];
            if (arrayValue.kind != InspectorCommand.Kind.Move)
                throw new InvalidOperationException("Editing the union array element changed its variant.");
            if (listValue.kind != InspectorCommand.Kind.Move)
                throw new InvalidOperationException("Editing the union list element changed its variant.");
            var arrayPayload = arrayValue.move;
            var listPayload = listValue.move;
            var arraySpeed = _window.FindFloatField("commands.Array.data[0]", "speed");
            var listSpeed = _window.FindFloatField("commandList.Array.data[0]", "speed");
            if (Math.Abs(arrayPayload.speed - 13f) >= 0.0001f ||
                Math.Abs(listPayload.speed - 14f) >= 0.0001f)
            {
                RecordUiDiagnostic(
                    "NestedAndCollectionPathsBind",
                    _window,
                    _collectionEditDiagnostic +
                    ";verify-array-runtime=" + arrayPayload.speed.ToString(CultureInfo.InvariantCulture) +
                    ";verify-list-runtime=" + listPayload.speed.ToString(CultureInfo.InvariantCulture) +
                    ";verify-array=" + _window.DescribeFloatField(arraySpeed) +
                    ";verify-list=" + _window.DescribeFloatField(listSpeed) +
                    ";verify-array-prop=" + _window.DescribeSerializedProperty("commands.Array.data[0]._serializedPayload.value._speed") +
                    ";verify-list-prop=" + _window.DescribeSerializedProperty("commandList.Array.data[0]._serializedPayload.value._speed"));
            }
            Require(Math.Abs(arrayPayload.speed - 13f) < 0.0001f,
                "The native array-element payload edit did not commit.");
            Require(Math.Abs(listPayload.speed - 14f) < 0.0001f,
                "The native list-element payload edit did not commit.");
            Observe("array-path=commands.Array.data[0];list-path=commandList.Array.data[0];relative=value;committed=true");
        }

        private static void WaitForCollectionEdits()
        {
            _collectionEditWaitFrames++;
            _window?.Serialized.Update();
            if (_collectionEditWaitFrames < 3)
                return;
            RunCase("NestedAndCollectionPathsBind", VerifyNestedAndCollectionPaths);
            _phase = (int)Phase.UiUndoAndExternal;
        }

        private static void WaitForCollections()
        {
            _waitFrames++;
            _window?.RefreshNativeListViews();
            if (_window != null &&
                _window.HasBindingPath("commands.Array.data[0].*", "speed") &&
                _window.HasBindingPath("commandList.Array.data[0].*", "speed") &&
                _window.AreEditableFieldBoundsReady("commands.Array.data[0]", "speed") &&
                _window.AreEditableFieldBoundsReady("commandList.Array.data[0]", "speed"))
            {
                RunCase("NestedAndCollectionPathsBind", CheckNestedAndCollectionPaths);
                if (_phase != (int)Phase.UiCollectionsEditWait)
                    _phase = (int)Phase.UiUndoAndExternal;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RunCase("NestedAndCollectionPathsBind", CheckNestedAndCollectionPaths);
                if (_phase != (int)Phase.UiCollectionsEditWait)
                    _phase = (int)Phase.UiUndoAndExternal;
            }
        }

        private static void StartUndoRedoAndExternalChange()
        {
            _undoError = null;
            _undoField = null;
            _undoEditSent = false;
            _undoCommitWaitFrames = 0;
            _undoBefore = string.Empty;
            _undoWaitFrames = 0;
            try
            {
                Require(_asset != null && _window != null, "The undo fixture was unavailable.");
                _asset.command = InspectorCommand.Speak("variant-before");
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssets();
                _window.RebindUi(_asset);
                _window.Show();
                _window.Focus();
                Undo.ClearAll();
                Require(_window.SetKind("command", "Move"),
                    "The Undo variant selector did not expose Move.");
                _window.Serialized.ApplyModifiedProperties();
                if (_asset.command.kind != InspectorCommand.Kind.Move)
                    throw new InvalidOperationException("The variant switch did not reach the actual host.");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                _window.Serialized.Update();
                if (_asset.command.kind != InspectorCommand.Kind.Speak)
                    throw new InvalidOperationException("Undo did not restore the previous variant.");
                Undo.PerformRedo();
                _window.Serialized.Update();
                if (_asset.command.kind != InspectorCommand.Kind.Move)
                    throw new InvalidOperationException("Redo did not restore the replacement variant.");

                _asset.command = InspectorCommand.Speak("undo-before");
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssets();
                _window.RebindUi(_asset);
                _window.Show();
                _window.Focus();
                _window.ExpandGeneratedPayloadFoldouts();
            }
            catch (Exception exception)
            {
                _undoError = exception;
            }
            _phase = (int)Phase.UiUndoFieldWait;
        }

        private static void PumpUndoRedoAndExternalChange()
        {
            _undoWaitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_undoError == null && _window != null && _asset != null)
            {
                _undoField = _window.FindTextField("command", "speech");
                if (_undoField != null && _window.PrepareTextFieldForEdit(_undoField))
                {
                    if (!_undoEditSent)
                    {
                        Undo.ClearAll();
                        _undoBefore = _asset.command.speak.speech;
                        Debug.Log(
                            "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT undo-before-send field=" +
                            _window.DescribeTextField(_undoField) + ";value=" + _undoField.value +
                            ";cursor=" + _undoField.cursorIndex + ";select=" + _undoField.selectIndex);
                        _window.SendKey('\0', KeyCode.End);
                        _window.SendKey('!', KeyCode.Alpha1);
                        _window.Serialized.ApplyModifiedProperties();
                        _undoEditSent = true;
                        Debug.Log(
                            "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT undo-after-send field=" +
                            _window.DescribeTextField(_undoField) + ";readOnly=" + _undoField.isReadOnly +
                            ";host=" + (_asset.command.kind == InspectorCommand.Kind.Speak
                                ? _asset.command.speak.speech : "<non-speak>"));
                        return;
                    }

                    _window.Serialized.Update();
                    if (_asset.command.kind != InspectorCommand.Kind.Speak ||
                        _asset.command.speak.speech != _undoBefore + "!")
                    {
                        _undoCommitWaitFrames++;
                        if (_undoCommitWaitFrames >= NativeEditCommitWaitFrames)
                        {
                            Debug.Log(
                                "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT undo-timeout field=" +
                                _window.DescribeTextField(_undoField) + ";readOnly=" + _undoField.isReadOnly +
                                ";host=" + (_asset.command.kind == InspectorCommand.Kind.Speak
                                    ? _asset.command.speak.speech : "<non-speak>"));
                            RunCase("UndoRedoAndExternalSerializedChange", () =>
                                throw new InvalidOperationException(
                                    "The rendered Undo field edit did not commit after " +
                                    NativeEditCommitWaitFrames + " update frames."));
                            _phase = (int)Phase.PrefabStart;
                        }
                        return;
                    }

                    var field = _undoField;
                    RunCase("UndoRedoAndExternalSerializedChange", () =>
                    {
                        Require(_asset.command.kind == InspectorCommand.Kind.Speak,
                            "The undo fixture did not retain its Speak variant.");
                        if (_asset.command.kind != InspectorCommand.Kind.Speak)
                            throw new InvalidOperationException("The Undo field edit changed the active variant.");
                        Require(_asset.command.speak.speech == _undoBefore + "!",
                            "The rendered Undo field edit did not reach the actual host.");
                        Undo.FlushUndoRecordObjects();
                        Undo.PerformUndo();
                        _window.Serialized.Update();
                        if (_asset.command.kind != InspectorCommand.Kind.Speak)
                            throw new InvalidOperationException("Undo changed the active variant unexpectedly.");
                        Require(_asset.command.speak.speech == _undoBefore,
                            "Undo did not restore the actual host payload.");
                        Undo.PerformRedo();
                        _window.Serialized.Update();
                        Require(_asset.command.speak.speech == _undoBefore + "!",
                            "Redo did not restore the actual host payload edit.");
                        Require(ReferenceEquals(field, _window.FindTextField("command", "speech")),
                            "Undo/Redo replaced the ordinary focused TextField control.");
                        Require(_window.IsTextFieldFocused(field),
                            "Undo/Redo did not preserve focus on the ordinary payload control.");

                        var external = new SerializedObject(_asset);
                        external.Update();
                        var ordinary = external.FindProperty("ordinaryField");
                        Require(ordinary != null, "The ordinary external SerializedObject field was unavailable.");
                        ordinary.stringValue = "external-change";
                        Require(external.ApplyModifiedProperties(), "The external SerializedObject change was not applied.");
                        _window.Serialized.Update();
                        Require(_asset.ordinaryField == "external-change",
                            "The external SerializedObject change did not reach the host.");
                        Require(_asset.command.speak.speech == _undoBefore + "!",
                            "Refreshing after an external change restored stale preview payload data.");

                        var externalUnion = new SerializedObject(_asset);
                        externalUnion.Update();
                        var externalSpeech = externalUnion.FindProperty("command")
                            ?.FindPropertyRelative("_serializedPayload")
                            ?.FindPropertyRelative("value")
                            ?.FindPropertyRelative("_speech");
                        Require(externalSpeech != null,
                            "The external SerializedObject could not reach the active union payload field.");
                        externalSpeech!.stringValue = "external-union";
                        Require(externalUnion.ApplyModifiedProperties(),
                            "The external union payload change was not applied.");
                        _undoExternalSerialized = externalUnion;
                        _undoExternalWaitFrames = 0;
                        _phase = (int)Phase.UiUndoExternalWait;
                        throw new PendingCaseException();
                    });
                    if (_phase != (int)Phase.UiUndoExternalWait)
                        _phase = (int)Phase.PrefabStart;
                    return;
                }
            }
            if (_undoEditSent &&
                (_undoField == null || _window == null || !_window.IsTextFieldFocused(_undoField)))
            {
                _undoCommitWaitFrames++;
                if (_undoCommitWaitFrames >= NativeEditCommitWaitFrames)
                {
                    Debug.Log(
                        "SPIRE_UNITY_VARIANT_INSPECTOR_EDIT undo-timeout field=" +
                        (_window == null ? "<window-null>" : _window.DescribeTextField(_undoField)) +
                        ";host=" + (_asset == null || _asset.command.kind != InspectorCommand.Kind.Speak
                            ? "<non-speak>" : _asset.command.speak.speech));
                    RunCase("UndoRedoAndExternalSerializedChange", () =>
                        throw new InvalidOperationException(
                            "The rendered Undo field edit did not commit after " +
                            NativeEditCommitWaitFrames + " update frames."));
                    _phase = (int)Phase.PrefabStart;
                    return;
                }
            }

            if (_undoError == null && _undoWaitFrames < UiWaitFrames)
                return;
            var error = _undoError ?? new UnverifiedException(
                "The undo speech field did not become focusable after " +
                UiWaitFrames.ToString(CultureInfo.InvariantCulture) + " editor updates.");
            RunCase("UndoRedoAndExternalSerializedChange", () => throw error);
            _phase = (int)Phase.PrefabStart;
        }

        private static void WaitForExternalUndoRefresh()
        {
            _undoExternalWaitFrames++;
            _window?.Serialized.Update();
            if (_window != null && _asset != null && _undoExternalSerialized != null)
            {
                var refreshedField = _window.FindTextField("command", "speech");
                if (_asset.command.kind == InspectorCommand.Kind.Speak &&
                    _asset.command.speak.speech == "external-union" &&
                    refreshedField != null && refreshedField.value == "external-union")
                {
                    RunCase("UndoRedoAndExternalSerializedChange", () =>
                    {
                        Require(_asset.command.kind == InspectorCommand.Kind.Speak,
                            "The external union payload edit changed the active variant.");
                        Require(_asset.command.speak.speech == "external-union",
                            "The external union payload edit did not reach the host.");
                        Require(refreshedField.value == "external-union",
                            "The open Inspector retained stale preview data after an external union edit.");
                        Undo.PerformUndo();
                        _window.Serialized.Update();
                        Require(_asset.command.speak.speech == _undoBefore + "!",
                            "Undo after an external union edit did not restore the actual prior payload.");
                        Undo.PerformRedo();
                        _window.Serialized.Update();
                        Require(_asset.command.speak.speech == "external-union",
                            "Redo after an external union edit did not restore the external payload.");
                        Observe("field-edit=Undo/Redo;external-ordinary=refreshed;external-union=refreshed;stale-preview=false");
                    });
                    _phase = (int)Phase.PrefabStart;
                    return;
                }
            }
            if (_undoExternalWaitFrames < UiWaitFrames)
                return;
            var fieldValue = _window?.FindTextField("command", "speech")?.value ?? "<null>";
            var actualValue = _asset == null || _asset.command.kind != InspectorCommand.Kind.Speak
                ? "<non-speak>"
                : _asset.command.speak.speech;
            RunCase("UndoRedoAndExternalSerializedChange", () => throw new UnverifiedException(
                "The external union payload did not refresh the open preview after " +
                UiWaitFrames.ToString(CultureInfo.InvariantCulture) + " updates; field=" + fieldValue + ";actual=" + actualValue));
            _phase = (int)Phase.PrefabStart;
        }

        private static void StartPrefabBinding()
        {
            Require(_prefabInstance != null && _prefabComponent != null,
                "The prefab instance was unavailable.");
            _window?.Close();
            _window = ScriptableObject.CreateInstance<InspectorProbeWindow>();
            _window.BindUi(_prefabComponent);
            _window.Show();
            _window.Focus();
            _waitFrames = 0;
        }

        private static void WaitForPrefabBinding()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_window != null && _window.UiReady)
            {
                _phase = (int)Phase.PrefabEdit;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordUiDiagnostic("PrefabOverrideIsRecorded", _window, "prefab-controls-unavailable");
                RecordFailure("PrefabOverrideIsRecorded", new UnverifiedException("The prefab Inspector did not render the generated union controls."));
                _phase = (int)Phase.MultiObjectStart;
            }
        }

        private static void CheckPrefabOverrideAndEdit()
        {
            Require(_prefabInstance != null && _prefabComponent != null && _window != null,
                "The prefab editing fixture was unavailable.");
            Require(!PrefabUtility.HasPrefabInstanceAnyOverrides(_prefabInstance, false),
                "Opening/repainting the prefab Inspector recorded an override.");
            var field = _window.FindTextField("command", "speech");
            Require(field != null && _window.FocusTextField(field),
                "The prefab command speech control was unavailable.");
            _window.SendKey('!', KeyCode.Alpha1);
            _window.Serialized.ApplyModifiedProperties();
            if (_prefabComponent.command.kind != InspectorCommand.Kind.Speak)
                throw new InvalidOperationException("The prefab value edit changed the active variant.");
            Require(_prefabComponent.command.speak.speech.EndsWith("!", StringComparison.Ordinal),
                "The prefab rendered field edit did not reach the instance.");
            Require(PrefabUtility.HasPrefabInstanceAnyOverrides(_prefabInstance, false),
                "The rendered prefab edit did not record an instance override.");
            var modifications = PrefabUtility.GetPropertyModifications(_prefabInstance);
            Require(modifications != null && modifications.Any(modification => modification.propertyPath.Contains("command", StringComparison.Ordinal)),
                "The prefab override list did not contain the edited union field.");
            Observe("open/repaint-overrides=false;field-edit-overrides=true;path=command");
        }

        private static void StartMultiObjectEditing()
        {
            Require(_asset != null && _secondAsset != null, "The multi-object assets were unavailable.");
            _asset.command = InspectorCommand.Speak("mixed");
            _secondAsset.command = InspectorCommand.Move(new Vector2(200f, 201f), 2f);
            EditorUtility.SetDirty(_asset);
            EditorUtility.SetDirty(_secondAsset);
            AssetDatabase.SaveAssets();

            _window?.Close();
            _window = ScriptableObject.CreateInstance<InspectorProbeWindow>();
            _window.BindUi(new UnityEngine.Object[] { _asset, _secondAsset });
            _window.Show();
            _window.Focus();
            _waitFrames = 0;
        }

        private static void WaitForMultiObjectBinding()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_window != null && _window.Root.panel != null && _window.FindKindSelector("command") != null)
            {
                _phase = (int)Phase.MultiObjectCheck;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordUiDiagnostic("MultiObjectMixedKindsAndPerObjectFields", _window, "multi-object-kind-selector-unavailable");
                RecordFailure("MultiObjectMixedKindsAndPerObjectFields", new UnverifiedException(
                    "The multi-object Inspector did not render its native kind selector."));
                _phase = (int)Phase.ImguiStart;
            }
        }

        private static void CheckMultiObjectMixedAndPrepareFields()
        {
            Require(_asset != null && _secondAsset != null && _window != null,
                "The multi-object assets were unavailable.");
            if (_window.FindKindSelector("command") == null)
                RecordUiDiagnostic("MultiObjectMixedKindsAndPerObjectFields", _window, "check-phase-kind-selector-null");
            Require(_window.FindKindSelector("command") != null,
                "The multi-object Inspector did not expose a kind selector.");
            Require(_window.KindSelectorShowsMixed("command"),
                "The multi-object Inspector did not show a mixed kind before a selection.");
            Require(_window.SetKind("command", "Move"),
                "The multi-object kind selector did not expose the real Move choice.");
            _window.Serialized.ApplyModifiedProperties();

            // The mixed-kind choice deliberately initializes the first target.
            // Seed distinct Move payloads again before the per-object field edit
            // so a speed edit can prove that destination data is not copied from
            // the first selected object.
            _asset.command = InspectorCommand.Move(new Vector2(100f, 101f), 1f);
            _secondAsset.command = InspectorCommand.Move(new Vector2(200f, 201f), 2f);
            EditorUtility.SetDirty(_asset);
            EditorUtility.SetDirty(_secondAsset);
            AssetDatabase.SaveAssets();
            _window.RebindUi(new UnityEngine.Object[] { _asset, _secondAsset });
        }

        private static void WaitForMultiObjectFields()
        {
            _waitFrames++;
            _window?.ExpandGeneratedPayloadFoldouts();
            if (_window == null || _asset == null || _secondAsset == null)
                throw new InvalidOperationException("The multi-object field-edit state was lost.");
            var speed = _window.FindFloatField("command", "speed");
            if (speed == null)
            {
                if (_waitFrames >= UiWaitFrames)
                {
                    RecordFailure("MultiObjectMixedKindsAndPerObjectFields", new UnverifiedException(
                        "The multi-object Move.speed field did not render after selecting Move."));
                    _phase = (int)Phase.ImguiStart;
                }
                return;
            }

            RunCase("MultiObjectMixedKindsAndPerObjectFields", () =>
            {
                Require(speed != null && speed.enabledInHierarchy && !speed.isReadOnly,
                    "The multi-object Move.speed control was unavailable or disabled.");
                Require(_asset.command.kind == InspectorCommand.Kind.Move &&
                    _secondAsset.command.kind == InspectorCommand.Kind.Move,
                    "The multi-object Move payloads were not active before the speed edit.");
                var beforeFirst = _asset.command.move;
                var beforeSecond = _secondAsset.command.move;
                Debug.Log(
                    "SPIRE_UNITY_VARIANT_INSPECTOR_MULTI before-speed-edit first-destination=" +
                    beforeFirst.destination + ";first-speed=" + beforeFirst.speed +
                    ";second-destination=" + beforeSecond.destination + ";second-speed=" + beforeSecond.speed);
                Require(beforeFirst.destination == new Vector2(100f, 101f) &&
                    beforeSecond.destination == new Vector2(200f, 201f),
                    "The multi-object payloads lost their distinct destinations before the speed edit.");
                speed!.value = 9f;
                _window.Serialized.ApplyModifiedProperties();
                var firstValue = _asset.command;
                var secondValue = _secondAsset.command;
                Debug.Log(
                    "SPIRE_UNITY_VARIANT_INSPECTOR_MULTI after-speed-edit first-kind=" +
                    firstValue.kind + ";first-destination=" + firstValue.move.destination +
                    ";first-speed=" + firstValue.move.speed + ";second-kind=" + secondValue.kind +
                    ";second-destination=" + secondValue.move.destination +
                    ";second-speed=" + secondValue.move.speed);
                if (firstValue.kind != InspectorCommand.Kind.Move)
                    throw new InvalidOperationException("Choosing a valid multi-object variant did not affect the first selected target.");
                if (secondValue.kind != InspectorCommand.Kind.Move)
                    throw new InvalidOperationException("Choosing a valid multi-object variant did not affect the second selected target.");
                var firstPayload = firstValue.move;
                var secondPayload = secondValue.move;
                Require(firstPayload.destination == new Vector2(100f, 101f),
                    "Editing speed on the first selected object copied its destination from another target.");
                Require(secondPayload.destination == new Vector2(200f, 201f),
                    "Editing speed on multi-selected objects copied the first target's destination.");
                Require(Math.Abs(firstPayload.speed - 9f) < 0.0001f &&
                    Math.Abs(secondPayload.speed - 9f) < 0.0001f,
                    "The multi-object speed edit did not affect both selected targets.");
                Observe("mixed-kind=true;choice=Move;speed=9;destinations=per-object;targets=2");
            });
            _phase = (int)Phase.ImguiStart;
        }

        private static void StartImguiProbe()
        {
            Require(_asset != null, "The IMGUI asset was unavailable.");
            _asset.command = InspectorCommand.Speak("imgui");
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            if (_asset.command.kind != InspectorCommand.Kind.Speak)
                throw new InvalidOperationException("The IMGUI fixture did not retain its Speak variant.");
            _imguiInitialSpeech = _asset.command.speak.speech;
            _window?.Close();
            _imguiWindow = ScriptableObject.CreateInstance<InspectorProbeWindow>();
            _imguiWindow.BindImgui(_asset, "command");
            _imguiWindow.InitialYaml = ReadAssetText(AssetPath);
            _imguiWindow.InitialPayloadId = SerializedPayloadId(_imguiWindow.Serialized, "command");
            _imguiWindow.Show();
            _imguiWindow.Focus();
            _waitFrames = 0;
            _imguiEditSent = false;
            _imguiKeyIndex = 0;
            _imguiEditWaitFrames = 0;
        }

        private static void WaitForImguiProbe()
        {
            _waitFrames++;
            if (_imguiWindow != null && _imguiWindow.ImguiRepainted)
            {
                RunCase("IMGUI_OpenAndRepaintDoesNotMutateActual", CheckImguiReadOnly);
                _phase = (int)Phase.ImguiEdit;
                return;
            }

            if (_waitFrames >= UiWaitFrames)
            {
                RecordFailure("IMGUI_OpenAndRepaintDoesNotMutateActual", new UnverifiedException("The native IMGUI editor window did not repaint."));
                _phase = (int)Phase.Finish;
            }
        }

        private static void CheckImguiReadOnly()
        {
            Require(_imguiWindow != null && _asset != null, "The IMGUI probe window was unavailable.");
            Require(_imguiWindow.LastPropertyHeight > 0f && _imguiWindow.LastPropertyRect.width > 0f,
                "The generated IMGUI drawer did not report a rendered property rect.");
            var imguiProperty = _imguiWindow.Serialized.FindProperty("command");
            Require(imguiProperty != null, "The IMGUI command SerializedProperty was unavailable.");
            var originalExpanded = imguiProperty!.isExpanded;
            imguiProperty.isExpanded = false;
            var collapsedHeight = EditorGUI.GetPropertyHeight(imguiProperty, true);
            imguiProperty.isExpanded = true;
            var expandedHeight = EditorGUI.GetPropertyHeight(imguiProperty, true);
            imguiProperty.isExpanded = originalExpanded;
            Require(collapsedHeight <= EditorGUIUtility.singleLineHeight + 0.1f,
                "The collapsed IMGUI union did not reduce to its root foldout line.");
            Require(expandedHeight > collapsedHeight,
                "The expanded IMGUI union did not include its Kind and payload rows.");
            CheckImguiHeightContract();
            Require(HasGeneratedDrawerFor<InspectorCommand>(),
                "TypeCache did not discover the generated InspectorCommand drawer for IMGUI.");
            if (_asset.command.kind != InspectorCommand.Kind.Speak)
                throw new InvalidOperationException("The IMGUI read-only repaint corrupted the union kind.");
            Require(_asset.command.speak.speech == _imguiInitialSpeech,
                "The IMGUI read-only repaint changed the active speech value.");
            Require(ReadAssetText(AssetPath) == _imguiWindow.InitialYaml,
                "The IMGUI read-only repaint changed the serialized YAML.");
            Require(SerializedPayloadId(_imguiWindow.Serialized, "command") == _imguiWindow.InitialPayloadId,
                "The IMGUI read-only repaint changed the active managed-reference ID.");
            Observe("actual EditorGUI.PropertyField/OnGUI repaint;drawer-discovered=true;read-only host stable");
        }

        private static void CheckImguiHeightContract()
        {
            Require(_imguiWindow != null && _asset != null, "The IMGUI height fixture was unavailable.");
            try
            {
                var heightPath = AssetFolder + "/InspectorHeightProbe.asset";
                var heightProbe = ScriptableObject.CreateInstance<InspectorCommandAsset>();
                heightProbe.command = InspectorCommand.Idle();
                AssetDatabase.CreateAsset(heightProbe, heightPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceUpdate);
                var persistedProbe = AssetDatabase.LoadAssetAtPath<InspectorCommandAsset>(heightPath);
                var heightSerialized = new SerializedObject(persistedProbe);
                heightSerialized.Update();
                var idle = heightSerialized.FindProperty("command");
                Require(idle != null, "The fieldless IMGUI command property was unavailable.");
                idle!.isExpanded = true;
                var expectedFieldless = EditorGUIUtility.singleLineHeight +
                    EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
                Require(Math.Abs(EditorGUI.GetPropertyHeight(idle, true) - expectedFieldless) < 0.1f,
                    "The fieldless IMGUI union height included a synthetic payload row.");

                persistedProbe!.command = InspectorCommand.Speak("height");
                EditorUtility.SetDirty(persistedProbe);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceUpdate);
                heightSerialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<InspectorCommandAsset>(heightPath));
                heightSerialized.Update();
                var speak = heightSerialized.FindProperty("command");
                var speech = speak?.FindPropertyRelative("_serializedPayload")?.FindPropertyRelative("value")?.FindPropertyRelative("_speech");
                Require(speak != null && speech != null, "The simple IMGUI payload field was unavailable.");
                speak!.isExpanded = true;
                var expectedSimple = EditorGUIUtility.singleLineHeight +
                    EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight +
                    EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(speech!, GUIContent.none, true);
                Require(Math.Abs(EditorGUI.GetPropertyHeight(speak, true) - expectedSimple) < 0.1f,
                    "The simple IMGUI union height did not contain exactly Kind and its direct payload field.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetFolder + "/InspectorHeightProbe.asset");
            }
        }

        private static void StartImguiEdit()
        {
            if (_imguiWindow == null)
            {
                RecordFailure("IMGUI_RenderedFieldEdit", new UnverifiedException("The IMGUI window was unavailable."));
                _phase = (int)Phase.Finish;
                return;
            }
            _imguiWindow.Focus();
            if (!NativeInspectorInput.IsAvailable)
            {
                RecordFailure(
                    "IMGUI_RenderedFieldEdit",
                    new UnverifiedException(
                        "Native XTest input was unavailable for the isolated Unity display; " +
                        "the direct EditorWindow.SendEvent path is intentionally not used."));
                _phase = (int)Phase.Finish;
                return;
            }
            if (!_imguiWindow.QueueNativeMouseClick())
            {
                RecordFailure("IMGUI_RenderedFieldEdit", new UnverifiedException(
                    "Native XTest could not queue the IMGUI field click."));
                _phase = (int)Phase.Finish;
                return;
            }
            _phase = (int)Phase.ImguiNativeKey;
        }

        private static void QueueImguiNativeRelease()
        {
            _phase = (int)Phase.ImguiNativeKey;
        }

        private static void QueueImguiNativeKey()
        {
            var keys = new[] { "x", "y", "z" };
            if (_imguiKeyIndex >= keys.Length || !NativeInspectorInput.QueueKeystroke(keys[_imguiKeyIndex]))
            {
                RecordFailure("IMGUI_RenderedFieldEdit", new UnverifiedException(
                    "Native desktop input could not queue the IMGUI keyboard event."));
                _phase = (int)Phase.Finish;
                return;
            }
            _imguiKeyIndex++;
            if (_imguiKeyIndex < keys.Length)
            {
                _phase = (int)Phase.ImguiNativeKey;
                return;
            }
            _imguiEditSent = true;
            _imguiEditWaitFrames = 0;
            _phase = (int)Phase.ImguiVerify;
        }

        private static void VerifyImguiEdit()
        {
            _imguiEditWaitFrames++;
            if (_imguiWindow == null || _asset == null)
            {
                RecordFailure("IMGUI_RenderedFieldEdit", new UnverifiedException("The IMGUI state disappeared during the real event sequence."));
                _phase = (int)Phase.Finish;
                return;
            }

            if (_imguiEditWaitFrames < 4)
                return;

            RunCase("IMGUI_RenderedFieldEdit", () =>
            {
                if (!_imguiEditSent)
                    throw new UnverifiedException(
                        "The native IMGUI input sequence was not completed at the generated field rect; " +
                        _imguiWindow.DescribeImgui() + ";native=" + NativeInspectorInput.IsAvailable +
                        ";speech=" + (_asset.command.kind == InspectorCommand.Kind.Speak ? _asset.command.speak.speech : "<non-speak>"));
                if (_asset.command.kind != InspectorCommand.Kind.Speak)
                    throw new InvalidOperationException("The IMGUI event changed the union to an invalid kind.");
                Require(_asset.command.speak.speech != _imguiInitialSpeech &&
                    _asset.command.speak.speech.EndsWith("xyz", StringComparison.Ordinal),
                    "The three real IMGUI text events did not commit the replacement union; speech=" +
                    _asset.command.speak.speech + ";" + _imguiWindow.DescribeImgui());
                Require(_imguiWindow.KeyboardControl != 0,
                    "The IMGUI keyboard control was not retained after the queued keystrokes.");
                Observe("actual EditorGUI mouse/key events;keys=xyz;replacement-union=true;keyboard-control=" + _imguiWindow.KeyboardControl + ";reflection-mutation=false");
            });
            _phase = (int)Phase.Finish;
        }

        private static void Finish()
        {
            if (_finishing || _report == null)
                return;

            _finishing = true;
            EditorApplication.update -= Tick;
            try
            {
                _window?.Close();
                _imguiWindow?.Close();
            }
            catch (Exception exception)
            {
                RecordFailure("Runner_WindowCleanup", exception);
            }

            var allCasesPassed = _report.cases.Count > 0 &&
                _report.cases.All(item => item.status == "pass" ||
                    (item.status == "not-applicable" && item.name == "ClosedGenericKindControl"));
            _report.status = _report.failures.Count == 0 && _report.unverified == 0 && allCasesPassed
                ? "pass"
                : "fail";
            _report.failed = _report.failures.Count;
            _report.completedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var reportPath = ProjectPath(ReportName);
            try
            {
                File.WriteAllText(reportPath, JsonUtility.ToJson(_report, true));
            }
            catch (Exception exception)
            {
                _report.status = "fail";
                _report.failures.Add("Could not write Inspector report: " + exception);
            }

            // The report is intentionally written before disposable assets are
            // removed, so a failed native run leaves its exact evidence intact.
            try
            {
                CleanupFixture();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogError("Inspector fixture cleanup failed: " + exception);
            }

            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_END status=" + _report.status + " checks=" + _report.checks + " failures=" + _report.failures.Count);
            EditorApplication.Exit(_report.status == "pass" ? 0 : 1);
        }

        private static void CheckNativeInputSafety()
        {
            var previous = Environment.GetEnvironmentVariable("SPIRE_UNITY_NATIVE_INPUT");
            try
            {
                Environment.SetEnvironmentVariable("SPIRE_UNITY_NATIVE_INPUT", null);
                NativeInspectorInput.Dispose();
                Require(!NativeInspectorInput.IsAvailable, "Native input was enabled without opt-in.");
                Require(!NativeInspectorInput.QueueMove(0, 0), "Pointer input bypassed opt-in.");
                Require(!NativeInspectorInput.QueueButton(1, true), "Button input bypassed opt-in.");
                Require(!NativeInspectorInput.QueueKeystroke("x"), "Keyboard input bypassed opt-in.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SPIRE_UNITY_NATIVE_INPUT", previous);
                NativeInspectorInput.Dispose();
            }
            Require(!NativeInspectorInput.QueueButton(0, true), "An invalid button was accepted.");
            Require(!NativeInspectorInput.QueueKeystroke(string.Empty), "An empty key was accepted.");
        }

        private static void RunCase(string name, Action action)
        {
            if (_report == null)
                return;

            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_CASE_START name=" + name);

            var result = new InspectorCase
            {
                name = name,
                status = "pass",
                checks = 0,
                observations = new List<string>(),
                failure = string.Empty,
            };
            var beforeChecks = _report.checks;
            try
            {
                action();
                result.checks = _report.checks - beforeChecks;
            }
            catch (PendingCaseException)
            {
                Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_CASE_COMPLETE name=" + name + " status=pending");
                WriteProgressReport();
                return;
            }
            catch (UnverifiedException exception)
            {
                result.status = "unverified";
                result.failure = exception.ToString();
                _report.unverified++;
                _report.failures.Add(name + " [unverified]: " + exception.Message);
                result.checks = _report.checks - beforeChecks;
            }
            catch (Exception exception)
            {
                result.status = "fail";
                result.failure = exception.ToString();
                _report.failures.Add(name + ": " + exception.Message);
                result.checks = _report.checks - beforeChecks;
            }

            _report.cases.Add(result);
            Debug.Log(
                "SPIRE_UNITY_VARIANT_INSPECTOR_CASE_COMPLETE name=" + name +
                " status=" + result.status + " checks=" + result.checks +
                (string.IsNullOrEmpty(result.failure) ? string.Empty : " failure=" + result.failure));
            if (result.status == "pass")
                _report.passed++;
            else
                _report.failed++;
            WriteProgressReport();
        }

        private static void RecordFailure(string name, Exception exception)
        {
            if (_report == null)
                return;
            _report.failures.Add(name + ": " + exception.Message);
            _report.failed++;
            _report.cases.Add(new InspectorCase
            {
                name = name,
                status = exception is UnverifiedException ? "unverified" : "fail",
                checks = 0,
                observations = new List<string>(),
                failure = exception.ToString(),
            });
            if (exception is UnverifiedException)
                _report.unverified++;
            WriteProgressReport();
        }

        private static void RecordNotApplicable(string name, string reason)
        {
            if (_report == null)
                return;

            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_CASE_COMPLETE name=" + name + " status=not-applicable");

            _report.cases.Add(new InspectorCase
            {
                name = name,
                status = "not-applicable",
                checks = 0,
                observations = new List<string> { reason },
                failure = reason,
            });
            _report.assertions.Add("not-applicable: " + reason);
            WriteProgressReport();
        }

        private static void WriteProgressReport()
        {
            if (_report == null)
                return;

            var status = _report.status;
            try
            {
                _report.status = "running";
                File.WriteAllText(ProjectPath(ReportName), JsonUtility.ToJson(_report, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("Inspector progress report write failed: " + exception.Message);
            }
            finally
            {
                _report.status = status;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (_report != null)
                _report.checks++;
            if (!condition)
                throw new InvalidOperationException(message);
            if (_report != null)
                _report.assertions.Add("pass: " + message);
        }

        private static void Observe(string message)
        {
            _report?.assertions.Add("observed: " + message);
        }

        private static void RecordUiDiagnostic(string label, InspectorProbeWindow? window, string details = "")
        {
            if (_report == null)
                return;
            var hierarchy = window?.DescribeHierarchy() ?? "window=null";
            _report.diagnostics.Add(label + " " + details + " hierarchy=" + hierarchy);
        }

        private static bool HasGeneratedDrawerFor<T>()
        {
            var drawerTypes = TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>();
            foreach (var type in drawerTypes)
            {
                foreach (var attribute in type.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(CustomPropertyDrawer) || attribute.ConstructorArguments.Count == 0)
                        continue;
                    if (attribute.ConstructorArguments[0].Value is Type target && target == typeof(T))
                        return true;
                }
            }

            return false;
        }

        private static string DescribeMatchingDrawerTypes<T>()
        {
            var requested = typeof(T);
            var requestedDefinition = requested.IsGenericType
                ? requested.GetGenericTypeDefinition()
                : requested;
            var matches = new List<string>();
            foreach (var type in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
            {
                foreach (var attribute in type.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(CustomPropertyDrawer) || attribute.ConstructorArguments.Count == 0)
                        continue;
                    if (attribute.ConstructorArguments[0].Value is not Type target)
                        continue;
                    if (target != requested && target != requestedDefinition)
                        continue;
                    matches.Add(type.AssemblyQualifiedName ?? type.FullName ?? type.Name);
                }
            }

            return matches.Count == 0 ? "<none>" : string.Join("|", matches.Distinct(StringComparer.Ordinal));
        }

        private static InspectorCommandAsset CreateAsset(
            string path,
            InspectorCommand command,
            InspectorCommand copiedLeft,
            InspectorCommand copiedRight,
            InspectorNode sharedNode)
        {
            var value = ScriptableObject.CreateInstance<InspectorCommandAsset>();
            value.command = command;
            value.copiedLeft = copiedLeft;
            value.copiedRight = copiedRight;
            value.sharedNode = sharedNode;
            value.secondSharedNode = sharedNode;
            AssetDatabase.CreateAsset(value, path);
            EditorUtility.SetDirty(value);
            return value;
        }

        private static void CreatePrefab()
        {
            var source = new GameObject("InspectorCommandPrefabSource");
            var component = source.AddComponent<InspectorCommandComponent>();
            component.command = InspectorCommand.Speak("prefab");
            component.copiedLeft = InspectorCommand.Speak("prefab-left");
            component.copiedRight = component.copiedLeft;
            component.commands = new[] { InspectorCommand.Move(new Vector2(1f, 1f), 2f) };
            component.commandList = new List<InspectorCommand> { InspectorCommand.Idle() };
            component.ordinaryField = "prefab-ordinary";
            component.sharedTexture = _texture;
            var shared = new InspectorNode { label = "prefab-shared", value = 11 };
            component.sharedNode = shared;
            component.secondSharedNode = shared;
            bool success;
            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP PrefabUtility.SaveAsPrefabAsset start path=" + PrefabPath);
            _prefabAsset = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath, out success);
            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP PrefabUtility.SaveAsPrefabAsset done success=" + success);
            Require(success && _prefabAsset != null, "The Inspector fixture prefab could not be saved.");
            UnityEngine.Object.DestroyImmediate(source);
            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP PrefabUtility.InstantiatePrefab start");
            _prefabInstance = PrefabUtility.InstantiatePrefab(_prefabAsset) as GameObject;
            Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP PrefabUtility.InstantiatePrefab done");
            Require(_prefabInstance != null, "The Inspector fixture prefab could not be instantiated.");
            _prefabComponent = _prefabInstance!.GetComponent<InspectorCommandComponent>();
            Require(_prefabComponent != null, "The Inspector fixture prefab component was unavailable.");
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private static void DeleteFixtureAssets()
        {
            foreach (var path in new[] { AssetPath, SecondAssetPath, GenericAssetPath, TexturePath, PrefabPath })
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
            if (_prefabInstance != null)
                UnityEngine.Object.DestroyImmediate(_prefabInstance);
            _prefabInstance = null;
            _prefabComponent = null;
        }

        private static void CleanupFixture()
        {
            _window = null;
            _imguiWindow = null;
            if (_prefabInstance != null)
                UnityEngine.Object.DestroyImmediate(_prefabInstance);
            _prefabInstance = null;
            _prefabComponent = null;
            DeleteFixtureAssets();
            if (AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.DeleteAsset(AssetFolder);
            _asset = null;
            _secondAsset = null;
            _genericAsset = null;
            _texture = null;
        }

        private static long SerializedPayloadId(SerializedObject serialized, string propertyPath)
        {
            var property = serialized.FindProperty(propertyPath);
            Require(property != null, "Serialized union property was unavailable: " + propertyPath);
            var payload = property!.FindPropertyRelative("_serializedPayload");
            Require(payload != null, "Serialized union did not expose _serializedPayload: " + propertyPath);
            return payload!.managedReferenceId;
        }

        private static string ReadAssetText(string path)
        {
            var absolute = ProjectPath(path);
            return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
        }

        private static string ProjectPath(string path)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot!.FullName, path));
        }

        [Serializable]
        private sealed class InspectorReport
        {
            public string probe = string.Empty;
            public string unityVersion = string.Empty;
            public string backend = string.Empty;
            public string graphicsBackend = string.Empty;
            public string graphicsDevice = string.Empty;
            public string status = string.Empty;
            public int checks;
            public int passed;
            public int failed;
            public int unverified;
            public string completedAt = string.Empty;
            public bool genericUnionSerializationSupported;
            public string genericUnionSerializationApplicability = string.Empty;
            public List<string> assertions = new List<string>();
            public List<string> failures = new List<string>();
            public List<string> diagnostics = new List<string>();
            public List<InspectorCase> cases = new List<InspectorCase>();
        }

        [Serializable]
        private sealed class InspectorCase
        {
            public string name = string.Empty;
            public string status = string.Empty;
            public int checks;
            public string failure = string.Empty;
            public List<string> observations = new List<string>();
        }

        private sealed class UnverifiedException : Exception
        {
            public UnverifiedException(string message) : base(message)
            {
            }
        }

        private sealed class PendingCaseException : Exception
        {
        }

        private readonly struct KeyStroke
        {
            public KeyStroke(char character, KeyCode keyCode)
            {
                this.character = character;
                this.keyCode = keyCode;
            }

            public readonly char character;
            public readonly KeyCode keyCode;
        }

        private sealed class InspectorProbeWindow : EditorWindow
        {
            private SerializedObject? _serialized;
            private SerializedProperty? _imguiProperty;
            private bool _imgui;
            private bool _imguiRepainted;
            private float _lastPropertyHeight;
            private Rect _lastPropertyRect;
            private Vector2 _imguiFieldScreenPoint;
            private int _imguiKeyboardControl;
            private bool _receivedKeyboardEvent;
            private InspectorElement? _inspector;
            private bool _nativeListViewsRefreshed;
            private TextField? _pendingFocusedField;

            public string InitialYaml { get; set; } = string.Empty;
            public long InitialPayloadId { get; set; }
            public int InitialUndoGroup { get; set; }
            public VisualElement Root => rootVisualElement;
            public SerializedObject Serialized => _serialized ?? throw new InvalidOperationException("SerializedObject is unavailable.");
            public bool UiReady => !_imgui && rootVisualElement.panel != null &&
                FindTextField("command", "speech") != null && FindKindSelector("command") != null &&
                AreFieldBoundsReady("command", "speech");
            public bool ImguiRepainted => _imguiRepainted;
            public int KeyboardControl => _imguiKeyboardControl;
            public float LastPropertyHeight => _lastPropertyHeight;
            public Rect LastPropertyRect => _lastPropertyRect;
            public bool QueueNativeMouseClick()
            {
                var x = Mathf.RoundToInt(_imguiFieldScreenPoint.x);
                var y = Mathf.RoundToInt(_imguiFieldScreenPoint.y);
                return NativeInspectorInput.QueueMove(x, y) && NativeInspectorInput.QueueClick(1);
            }

            public void BindUi(UnityEngine.Object target)
            {
                _imgui = false;
                _imguiRepainted = false;
                _receivedKeyboardEvent = false;
                _nativeListViewsRefreshed = false;
                _pendingFocusedField = null;
                _serialized = new SerializedObject(target);
                _serialized.Update();
                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _inspector = new InspectorElement
                {
                    name = "spire-native-inspector",
                };
                rootVisualElement.Add(_inspector);
                AddNativePropertyFields(_inspector);
                minSize = new Vector2(780f, 640f);
                titleContent = new GUIContent("Spire Inspector Fixture");
            }

            public void BindGenericUi(UnityEngine.Object target)
            {
                _imgui = false;
                _imguiRepainted = false;
                _receivedKeyboardEvent = false;
                _nativeListViewsRefreshed = false;
                _pendingFocusedField = null;
                _serialized = new SerializedObject(target);
                _serialized.Update();
                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _inspector = new InspectorElement
                {
                    name = "spire-closed-generic-inspector",
                };
                var property = _serialized.FindProperty("generic");
                if (property == null)
                    throw new InvalidOperationException("The closed-generic SerializedProperty was unavailable.");
                rootVisualElement.Add(_inspector);
                var directField = new PropertyField(property, "generic")
                {
                    name = "spire-direct-property-generic",
                };
                _inspector.Add(directField);
                directField.Bind(_serialized);
                minSize = new Vector2(560f, 220f);
                titleContent = new GUIContent("Spire Closed Generic Inspector Fixture");
            }

            public void BindUi(UnityEngine.Object[] targets)
            {
                _imgui = false;
                _imguiRepainted = false;
                _receivedKeyboardEvent = false;
                _nativeListViewsRefreshed = false;
                _pendingFocusedField = null;
                _serialized = new SerializedObject(targets);
                _serialized.Update();
                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _inspector = new InspectorElement
                {
                    name = "spire-native-multi-object-inspector",
                };
                rootVisualElement.Add(_inspector);
                AddNativePropertyFields(_inspector);
                minSize = new Vector2(780f, 640f);
                titleContent = new GUIContent("Spire Inspector Multi-Object Fixture");
            }

            public void RebindUi(UnityEngine.Object target)
            {
                BindUi(target);
            }

            public void RebindUi(UnityEngine.Object[] targets)
            {
                BindUi(targets);
            }

            public void BindImgui(UnityEngine.Object target, string propertyPath)
            {
                _imgui = true;
                _imguiRepainted = false;
                _receivedKeyboardEvent = false;
                _pendingFocusedField = null;
                _serialized = new SerializedObject(target);
                _serialized.Update();
                _imguiProperty = _serialized.FindProperty(propertyPath);
                if (_imguiProperty == null)
                    throw new InvalidOperationException("IMGUI property was unavailable: " + propertyPath);
                rootVisualElement.Clear();
                minSize = new Vector2(560f, 240f);
                titleContent = new GUIContent("Spire IMGUI Inspector Fixture");
            }

            public TextField? FindTextField(string owner, string fieldName)
            {
                TextField? found = null;
                rootVisualElement.Query<TextField>().ForEach(field =>
                {
                    if (found == null && IsBinding(field, owner, fieldName))
                        found = field;
                });
                return found;
            }

            public string DescribeTextField(TextField? field)
            {
                if (field == null)
                    return "<null>";
                return DescribeElement(field) + ";value=" + (field.value ?? "<null>") +
                    ";focused=" + IsTextFieldFocused(field);
            }

            public string DescribeElement(VisualElement? element)
            {
                if (element == null)
                    return "<null>";
                return element.GetType().FullName +
                    ";hash=" + element.GetHashCode().ToString(CultureInfo.InvariantCulture) +
                    ";name=" + element.name +
                    ";binding=" + ((element as IBindable)?.bindingPath ?? "<none>") +
                    ";panel=" + (element.panel != null) +
                    ";enabled=" + element.enabledInHierarchy +
                    ";visible=" + element.visible +
                    ";worldBound=" + element.worldBound +
                    ";classes=" + string.Join(",", element.GetClasses());
            }

            public string DescribeFloatField(FloatField? field)
            {
                if (field == null)
                    return "<null>";
                return DescribeElement(field) +
                    ";value=" + field.value.ToString(CultureInfo.InvariantCulture);
            }

            public string DescribeSerializedProperty(string path)
            {
                if (_serialized == null)
                    return "so=<null>;path=" + path;
                var targets = _serialized.targetObjects == null
                    ? string.Empty
                    : string.Join(",", _serialized.targetObjects
                        .Where(target => target != null)
                        .Select(target => target.GetEntityId().ToString()));
                var property = _serialized.FindProperty(path);
                if (property == null)
                    return "soTargets=" + targets + ";modified=" + _serialized.hasModifiedProperties +
                        ";path=" + path + ";property=<null>";
                var value = "<n/a>";
                if (property.propertyType == SerializedPropertyType.Float)
                    value = property.floatValue.ToString(CultureInfo.InvariantCulture);
                return "soTargets=" + targets + ";modified=" + _serialized.hasModifiedProperties +
                    ";path=" + path + ";propertyPath=" + property.propertyPath +
                    ";type=" + property.propertyType + ";value=" + value;
            }

            public FloatField? FindFloatField(string owner, string fieldName)
            {
                FloatField? found = null;
                rootVisualElement.Query<FloatField>().ForEach(field =>
                {
                    if (found == null && IsBinding(field, owner, fieldName))
                        found = field;
                });
                return found;
            }

            public Vector2Field? FindVector2Field(string owner, string fieldName)
            {
                Vector2Field? found = null;
                rootVisualElement.Query<Vector2Field>().ForEach(field =>
                {
                    if (found == null && IsBinding(field, owner, fieldName))
                        found = field;
                });
                return found;
            }

            public bool AreFieldBoundsReady(string owner, params string[] fieldNames)
            {
                var selector = FindKindSelector(owner);
                if (selector == null || !HasFinitePositiveBounds(selector.worldBound))
                    return false;
                foreach (var fieldName in fieldNames)
                {
                    var field = FindTextField(owner, fieldName) as VisualElement ??
                        FindFloatField(owner, fieldName) as VisualElement ??
                        FindVector2Field(owner, fieldName) as VisualElement;
                    if (field == null || !HasFinitePositiveBounds(field.worldBound))
                        return false;
                }
                return true;
            }

            public bool AreEditableFieldBoundsReady(string owner, params string[] fieldNames)
            {
                foreach (var fieldName in fieldNames)
                {
                    var field = FindFloatField(owner, fieldName) as VisualElement ??
                        FindTextField(owner, fieldName) as VisualElement ??
                        FindVector2Field(owner, fieldName) as VisualElement;
                    if (field == null || !field.enabledInHierarchy ||
                        (field is TextField textField && textField.isReadOnly) ||
                        (field is FloatField floatField && floatField.isReadOnly) ||
                        (field is Vector2Field vectorField && !IsEditableVector2Field(vectorField)) ||
                        !HasFinitePositiveBounds(field.worldBound))
                        return false;
                }

                return true;
            }

            private static bool IsEditableVector2Field(Vector2Field field)
            {
                var foundComponent = false;
                var editable = true;
                field.Query<FloatField>().ForEach(component =>
                {
                    foundComponent = true;
                    if (!component.enabledInHierarchy || component.isReadOnly ||
                        !HasFinitePositiveBounds(component.worldBound))
                        editable = false;
                });
                return foundComponent && editable;
            }

            private static bool HasFinitePositiveBounds(Rect bounds)
                => IsFinite(bounds.x) && IsFinite(bounds.y) &&
                    IsFinite(bounds.width) && IsFinite(bounds.height) &&
                    bounds.width > 0f && bounds.height > 0f;

            private static bool IsFinite(float value)
                => !float.IsNaN(value) && !float.IsInfinity(value);

            public void RequireAlignedFieldColumns(string owner, params string[] fieldNames)
            {
                var selector = FindKindSelector(owner);
                Require(selector != null, "Kind selector was unavailable for layout alignment: " + owner);
                Require(selector!.GetFirstAncestorOfType<InspectorElement>() != null,
                    "Kind selector was not attached under a native InspectorElement: " + DescribeElement(selector));
                var kindLabel = selector!.Q<Label>();
                var kindInput = selector.Q<VisualElement>(className: "unity-base-field__input");
                Require(kindLabel != null && kindInput != null && kindLabel.worldBound.width > 0f && kindInput.worldBound.width > 0f,
                    "Kind selector layout was unavailable: " + DescribeElement(selector));
                foreach (var fieldName in fieldNames)
                {
                    var field = FindTextField(owner, fieldName) as VisualElement ??
                        FindFloatField(owner, fieldName) as VisualElement ??
                        FindVector2Field(owner, fieldName) as VisualElement;
                    Require(field != null, "Payload field was unavailable for layout alignment: " + fieldName);
                    Require(field!.GetFirstAncestorOfType<InspectorElement>() != null,
                        "Payload field was not attached under a native InspectorElement: " + DescribeElement(field));
                    var label = field.Q<Label>();
                    var input = field.Q<VisualElement>(className: "unity-base-field__input");
                    var details = "kind=" + DescribeElement(selector) + ";kindLabel=" + DescribeElement(kindLabel) +
                        ";kindInput=" + DescribeElement(kindInput) + ";field=" + DescribeElement(field) +
                        ";fieldLabel=" + DescribeElement(label) + ";fieldInput=" + DescribeElement(input);
                    Require(label != null && input != null && label.worldBound.width > 0f && input.worldBound.width > 0f,
                        "Payload field layout was unavailable: " + details);
                    Require(Math.Abs(kindLabel!.worldBound.x - label!.worldBound.x) <= 1f &&
                        Math.Abs(kindInput!.worldBound.x - input!.worldBound.x) <= 1f,
                        "Kind and payload field columns were not aligned within 1px: " + details);
                }
            }

            public VisualElement? FindKindSelector(string owner)
            {
                VisualElement? found = null;
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (found != null)
                        return;
                    var binding = (element as IBindable)?.bindingPath ?? string.Empty;
                    var isKindBinding = binding.Contains(owner + ".", StringComparison.Ordinal) &&
                        binding.EndsWith("_kind", StringComparison.Ordinal);
                    var isNativeSelector = element is EnumField ||
                        element.GetType().Name.StartsWith("PopupField", StringComparison.Ordinal);
                    if (isNativeSelector && (isKindBinding || IsInOwnerProperty(element, owner)))
                        found = element;
                });
                return found;
            }

            public string KindSelectorLabel(string owner)
            {
                var selector = FindKindSelector(owner);
                if (selector == null)
                    return string.Empty;
                var labelProperty = selector.GetType().GetProperty("label", BindingFlags.Instance | BindingFlags.Public);
                if (labelProperty?.GetValue(selector, null) is string label)
                    return label;
                return selector.Q<Label>()?.text ?? string.Empty;
            }

            public Foldout? FindUnionRootFoldout(string owner)
            {
                Foldout? found = null;
                rootVisualElement.Query<Foldout>().ForEach(foldout =>
                {
                    if (found != null || !string.Equals(foldout.name, "__SpireUnionProperty", StringComparison.Ordinal))
                        return;
                    var ownerField = foldout.GetFirstAncestorOfType<PropertyField>();
                    var ownerBinding = (ownerField as IBindable)?.bindingPath;
                    var matchesOwner = string.Equals(ownerBinding, owner, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(ownerBinding) && ownerBinding.EndsWith("." + owner, StringComparison.Ordinal));
                    foldout.contentContainer.Query<VisualElement>().ForEach(child =>
                    {
                        if (matchesOwner)
                            return;
                        var binding = (child as IBindable)?.bindingPath;
                        if (!string.IsNullOrEmpty(binding) &&
                            (binding.Contains(owner + ".", StringComparison.Ordinal) ||
                                binding.Contains(owner + "_", StringComparison.Ordinal)))
                            matchesOwner = true;
                    });
                    if (matchesOwner)
                        found = foldout;
                });
                return found;
            }

            public bool KindSelectorShowsMixed(string owner)
            {
                var selector = FindKindSelector(owner);
                if (selector == null)
                    return false;
                var property = selector.GetType().GetProperty("showMixedValue", BindingFlags.Instance | BindingFlags.Public);
                return property?.GetValue(selector, null) as bool? == true;
            }

            public bool SetKind(string owner, string desired)
            {
                var selector = FindKindSelector(owner);
                if (selector == null)
                    return false;
                if (selector is EnumField enumField)
                {
                    var enumType = enumField.value?.GetType() ?? ResolveKindType(owner);
                    if (enumType == null || !enumType.IsEnum)
                        return false;
                    enumField.value = (Enum)Enum.Parse(enumType, desired, ignoreCase: false);
                    return true;
                }

                var choicesProperty = selector.GetType().GetProperty("choices", BindingFlags.Instance | BindingFlags.Public);
                var valueProperty = selector.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                var choices = choicesProperty?.GetValue(selector, null) as IEnumerable;
                if (valueProperty == null || !valueProperty.CanWrite || choices == null)
                    return false;
                foreach (var choice in choices)
                {
                    if (choice != null && string.Equals(choice.ToString(), desired, StringComparison.Ordinal))
                    {
                        valueProperty.SetValue(selector, choice, null);
                        return true;
                    }
                }
                return false;
            }

            public string[] KindChoices(string owner)
            {
                var selector = FindKindSelector(owner);
                if (selector == null)
                    return Array.Empty<string>();
                if (selector is EnumField enumField)
                {
                    var enumType = enumField.value?.GetType() ?? ResolveKindType(owner);
                    return enumType == null || !enumType.IsEnum
                        ? Array.Empty<string>()
                        : Enum.GetNames(enumType);
                }
                var choicesProperty = selector.GetType().GetProperty("choices", BindingFlags.Instance | BindingFlags.Public);
                var choices = choicesProperty?.GetValue(selector, null) as IEnumerable;
                return choices == null
                    ? Array.Empty<string>()
                    : choices.Cast<object>().Select(choice => choice?.ToString() ?? string.Empty).ToArray();
            }

            private Type? ResolveKindType(string owner)
            {
                var target = _serialized?.targetObject;
                if (target == null)
                    return null;
                var field = target.GetType().GetField(
                    owner,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var unionType = field?.FieldType;
                var kindType = unionType?.GetNestedType(
                    "Kind",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (kindType == null || !kindType.IsGenericTypeDefinition || unionType == null || !unionType.IsGenericType)
                    return kindType;
                var arguments = unionType.GetGenericArguments();
                return kindType.GetGenericArguments().Length == arguments.Length
                    ? kindType.MakeGenericType(arguments)
                    : kindType;
            }

            public bool IsPayloadValuePropertyAvailable(string owner, string fieldName)
            {
                var property = Serialized.FindProperty(owner);
                var payload = property?.FindPropertyRelative("_serializedPayload");
                var value = payload?.FindPropertyRelative("value");
                return value?.FindPropertyRelative("_" + fieldName) != null;
            }

            public bool HasSyntheticValueControl(string owner)
            {
                var root = FindUnionRootFoldout(owner);
                if (root == null)
                    return false;
                var found = false;
                root.contentContainer.Query<VisualElement>().ForEach(element =>
                {
                    if (found || element is not PropertyField && element is not Foldout)
                        return;
                    var path = (element as IBindable)?.bindingPath;
                    if (string.Equals(path, owner + "._serializedPayload.value", StringComparison.Ordinal) ||
                        string.Equals(path, owner + ".Payload.value", StringComparison.Ordinal) ||
                        string.Equals(path, "Payload.value", StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(path) &&
                            (path.EndsWith("._serializedPayload.value", StringComparison.Ordinal) ||
                                path.EndsWith(".Payload.value", StringComparison.Ordinal))))
                        found = true;
                });
                return found;
            }

            public int ImmediatePayloadFieldCount(string owner)
            {
                var root = FindUnionRootFoldout(owner);
                if (root == null)
                    return 0;
                return root.contentContainer.Children()
                    .OfType<PropertyField>()
                    .Count(field =>
                    {
                        var path = (field as IBindable)?.bindingPath;
                        return !string.IsNullOrEmpty(path) &&
                            (path.Contains("._serializedPayload.value.", StringComparison.Ordinal) ||
                                path.Contains(".Payload.value.", StringComparison.Ordinal) ||
                                path.StartsWith("Payload.value.", StringComparison.Ordinal));
                    });
            }

            public VisualElement? FindDrawerTree(string owner)
            {
                var field = FindTextField(owner, "speech") as VisualElement;
                while (field?.parent != null)
                {
                    if (field.parent is PropertyField)
                        return field.parent;
                    field = field.parent;
                }
                return FindKindSelector(owner);
            }

            public void RequireBoundControlsEditable(string owner)
            {
                var controls = new List<VisualElement>();
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    var path = (element as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(path) &&
                        (path.Contains(owner + ".", StringComparison.Ordinal) ||
                            path.Contains("Payload.value", StringComparison.Ordinal) && IsInOwnerProperty(element, owner)))
                        controls.Add(element);
                });
                Require(controls.Count > 0, "No bound controls were found for " + owner + ".");
                foreach (var control in controls)
                {
                    if (control is PropertyField || control is Foldout || control is Label)
                        continue;
                    Require(control.enabledInHierarchy,
                        "Bound generated control was disabled in hierarchy: " + control.GetType().Name + " " +
                        ((IBindable)control).bindingPath);
                }
            }

            public bool NewPreviewControlsAreEnabled()
            {
                var disabled = false;
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    var path = (element as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(path) &&
                        (path.Contains("command._serializedPayload", StringComparison.Ordinal) ||
                            path.Contains("Payload.value", StringComparison.Ordinal)) &&
                        !element.enabledInHierarchy)
                        disabled = true;
                });
                return !disabled;
            }

            public bool ExpandGeneratedPayloadFoldouts()
            {
                var expanded = false;
                rootVisualElement.Query<Foldout>().ForEach(foldout =>
                {
                    if (!string.Equals(foldout.name, "__SpireUnionProperty", StringComparison.Ordinal))
                        return;
                    if (!foldout.value)
                    {
                        foldout.value = true;
                        expanded = true;
                    }
                });
                rootVisualElement.Query<Foldout>().ForEach(foldout =>
                {
                    var path = (foldout as IBindable)?.bindingPath;
                    if (string.IsNullOrEmpty(path) || !path.EndsWith("Payload.value", StringComparison.Ordinal))
                        return;
                    if (!foldout.value)
                    {
                        foldout.value = true;
                        expanded = true;
                    }
                });
                return expanded;
            }

            public void RequirePreviewHideFlagsDoNotDisableEditing()
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
                {
                    if (candidate == null)
                        continue;
                    var typeName = candidate.GetType().Name;
                    if ((typeName.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0) &&
                        (candidate.hideFlags & HideFlags.NotEditable) != 0)
                    {
                        throw new InvalidOperationException(
                            "Generated preview object used HideFlags.NotEditable: " + typeName + ".");
                    }
                }
            }

            public bool HasBindingPath(string prefix, string fieldName)
            {
                var expectedPrefix = prefix.Replace(".*", string.Empty, StringComparison.Ordinal);
                var owner = expectedPrefix.Split('.')[0];
                var found = false;
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (found)
                        return;
                    var path = (element as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(path) && path.StartsWith(expectedPrefix, StringComparison.Ordinal) &&
                        (path.EndsWith("." + fieldName, StringComparison.Ordinal) ||
                            path.EndsWith("._" + fieldName, StringComparison.Ordinal)))
                        found = true;
                    if (!found && !string.IsNullOrEmpty(path) && path.Contains("Payload.value", StringComparison.Ordinal) &&
                        (path.EndsWith("." + fieldName, StringComparison.Ordinal) ||
                            path.EndsWith("._" + fieldName, StringComparison.Ordinal)) &&
                        IsInOwnerProperty(element, owner))
                        found = true;
                });
                return found;
            }

            public bool ExpandFoldout(string owner)
            {
                Foldout? found = null;
                rootVisualElement.Query<Foldout>().ForEach(foldout =>
                {
                    if (found != null)
                        return;
                    var path = (foldout as IBindable)?.bindingPath;
                    if (string.Equals(path, owner, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(path) &&
                            (path.EndsWith("." + owner, StringComparison.Ordinal) ||
                                path.EndsWith("._" + owner, StringComparison.Ordinal))))
                        found = foldout;
                });
                if (found == null)
                    return false;
                found.value = true;
                return true;
            }

            public void RefreshNativeListViews()
            {
                if (_nativeListViewsRefreshed)
                    return;
                _nativeListViewsRefreshed = true;
                rootVisualElement.Query<ListView>().ForEach(listView =>
                {
                    var bindingPath = (listView as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(bindingPath))
                    {
                        var property = _serialized?.FindProperty(bindingPath);
                        if (property != null)
                            property.isExpanded = true;
                    }
                    listView.RefreshItems();
                    listView.Rebuild();
                    if (listView.itemsSource != null && listView.itemsSource.Count > 0)
                    {
                        listView.ScrollToItem(0);
                        listView.GetRootElementForIndex(0);
                    }
                    var scrollView = listView.Q<ScrollView>();
                    if (scrollView != null)
                        scrollView.scrollOffset = Vector2.zero;
                    listView.schedule.Execute(() =>
                    {
                        if (listView.panel == null)
                            return;
                        listView.RefreshItems();
                        listView.Rebuild();
                        if (listView.itemsSource != null && listView.itemsSource.Count > 0)
                            listView.ScrollToItem(0);
                    }).StartingIn(0);
                });
            }

            public string DescribeListViews()
            {
                var descriptions = new List<string>();
                rootVisualElement.Query<ListView>().ForEach(listView =>
                {
                    var path = (listView as IBindable)?.bindingPath ?? string.Empty;
                    var count = listView.itemsSource == null ? -1 : listView.itemsSource.Count;
                    descriptions.Add(path + ":source=" + count.ToString(CultureInfo.InvariantCulture) +
                        ":children=" + listView.childCount.ToString(CultureInfo.InvariantCulture));
                });
                return string.Join(",", descriptions);
            }

            public bool PrepareTextFieldForEdit(TextField field)
            {
                var firstRequest = !ReferenceEquals(_pendingFocusedField, field);
                if (firstRequest)
                    _pendingFocusedField = field;

                var input = field.Q(TextInputBaseField<string>.textInputUssName) ?? field;
                if (!field.enabledInHierarchy || field.isReadOnly || input.panel == null)
                    return false;

                if (firstRequest)
                {
                    input.Focus();
                    return false;
                }

                if (!IsTextFieldFocused(field))
                {
                    input.Focus();
                    return false;
                }

                var end = field.value?.Length ?? 0;
                field.cursorIndex = end;
                field.selectIndex = end;
                _pendingFocusedField = null;
                return true;
            }

            public bool FocusTextField(TextField field)
            {
                var input = field.Q(TextInputBaseField<string>.textInputUssName) ?? field;
                if (!field.enabledInHierarchy || field.isReadOnly || input.panel == null)
                    return false;
                input.Focus();
                var end = field.value?.Length ?? 0;
                field.cursorIndex = end;
                field.selectIndex = end;
                return IsTextFieldFocused(field);
            }

            public bool IsTextFieldFocused(TextField field)
            {
                var focused = rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
                return focused != null && (ReferenceEquals(focused, field) || field.Contains(focused));
            }

            public void SendKey(char character, KeyCode keyCode)
            {
                SendEvent(new Event
                {
                    type = EventType.KeyDown,
                    character = keyCode == KeyCode.Backspace ? '\0' : character,
                    keyCode = keyCode,
                    modifiers = keyCode == KeyCode.Backspace
                        ? EventModifiers.FunctionKey
                        : keyCode == KeyCode.Alpha1
                            ? EventModifiers.Shift
                            : EventModifiers.None,
                });
            }

            public string DescribeUi()
            {
                return "panel=" + (rootVisualElement.panel != null) +
                    ";text=" + (FindTextField("command", "speech") != null) +
                    ";kind=" + (FindKindSelector("command") != null) +
                    ";children=" + rootVisualElement.childCount;
            }

            public string DescribeHierarchy()
            {
                var builder = new StringBuilder();
                AppendHierarchy(rootVisualElement, builder, 0);
                return builder.ToString();
            }

            private static void AppendHierarchy(VisualElement element, StringBuilder builder, int depth)
            {
                builder.Append(' ', depth * 2);
                builder.Append(element.GetType().FullName);
                builder.Append(" name=");
                builder.Append(element.name);
                builder.Append(" binding=");
                builder.Append((element as IBindable)?.bindingPath ?? "<none>");
                builder.Append(" children=");
                builder.Append(element.childCount);
                builder.Append(" panel=");
                builder.Append(element.panel != null);
                builder.Append(" enabled=");
                builder.Append(element.enabledInHierarchy);
                builder.Append(" visible=");
                builder.Append(element.visible);
                builder.Append(" bound=");
                builder.Append(element.worldBound);
                builder.AppendLine();
                for (var index = 0; index < element.childCount; index++)
                    AppendHierarchy(element[index], builder, depth + 1);
            }

            public string DescribeImgui()
            {
                return "repaint=" + _imguiRepainted +
                    ";rect=" + _lastPropertyRect +
                    ";screenPoint=" + _imguiFieldScreenPoint +
                    ";height=" + _lastPropertyHeight +
                    ";keyboardControl=" + _imguiKeyboardControl +
                    ";keyboard=" + _receivedKeyboardEvent;
            }

            private void OnGUI()
            {
                if (!_imgui || _serialized == null || _imguiProperty == null)
                    return;
                _serialized.Update();
                var height = EditorGUI.GetPropertyHeight(_imguiProperty, true);
                _lastPropertyHeight = height;
                _lastPropertyRect = GUILayoutUtility.GetRect(520f, height);
                var fieldPoint = new Vector2(
                    _lastPropertyRect.x + _lastPropertyRect.width * 0.6f,
                    _lastPropertyRect.y + Mathf.Max(0f, _lastPropertyRect.height - 8f));
                _imguiFieldScreenPoint = GUIUtility.GUIToScreenPoint(fieldPoint);
                EditorGUI.PropertyField(_lastPropertyRect, _imguiProperty, new GUIContent("command"), true);
                _imguiKeyboardControl = GUIUtility.keyboardControl;
                if (Event.current != null && Event.current.type == EventType.Repaint)
                    _imguiRepainted = true;
                if (Event.current != null && Event.current.type == EventType.KeyDown &&
                    (Event.current.keyCode == KeyCode.Alpha1 || Event.current.character == 'x'))
                    _receivedKeyboardEvent = true;
            }

            private static bool IsBinding(VisualElement element, string owner, string fieldName)
            {
                var path = (element as IBindable)?.bindingPath;
                if (string.IsNullOrEmpty(path))
                    return false;
                var directBinding = path.Contains(owner + ".", StringComparison.Ordinal) &&
                    (string.Equals(path, fieldName, StringComparison.Ordinal) ||
                        string.Equals(path, "_" + fieldName, StringComparison.Ordinal) ||
                        path.EndsWith("." + fieldName, StringComparison.Ordinal) ||
                        path.EndsWith("._" + fieldName, StringComparison.Ordinal));
                var previewBinding = path.Contains("Payload.value", StringComparison.Ordinal) &&
                    (path.EndsWith("." + fieldName, StringComparison.Ordinal) ||
                        path.EndsWith("._" + fieldName, StringComparison.Ordinal));
                return directBinding || previewBinding && IsInOwnerProperty(element, owner);
            }

            private static bool IsInOwnerProperty(VisualElement element, string owner)
            {
                var rootOwner = owner;
                var separator = rootOwner.IndexOf('.', StringComparison.Ordinal);
                if (separator >= 0)
                    rootOwner = rootOwner.Substring(0, separator);
                for (var current = element; current != null; current = current.parent)
                {
                    if (current is not PropertyField)
                        continue;
                    var name = current.name ?? string.Empty;
                    if (name == "spire-property-" + owner ||
                        name == "spire-direct-property-" + owner ||
                        name == "PropertyField:" + owner ||
                        name == "spire-property-" + rootOwner ||
                        name == "spire-direct-property-" + rootOwner ||
                        name == "PropertyField:" + rootOwner)
                        return true;
                }

                return false;
            }

            private void AddNativePropertyFields(VisualElement container)
            {
                foreach (var path in new[]
                {
                    "command",
                    "copiedLeft",
                    "copiedRight",
                    "commands",
                    "commandList",
                    "ordinaryField",
                    "sharedTexture",
                    "sharedNode",
                    "secondSharedNode",
                })
                {
                    Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-start path=" + path);
                    var property = _serialized?.FindProperty(path);
                    if (property == null)
                    {
                        Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-skip path=" + path + " reason=missing-property");
                        continue;
                    }
                    var field = new PropertyField(property, path)
                    {
                        name = "spire-property-" + path,
                    };
                    container.Add(field);
                    field.Bind(_serialized!);
                    Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-done path=" + path);
                }

                foreach (var elementPath in new[]
                {
                    "commands.Array.data[0]",
                    "commandList.Array.data[0]",
                })
                {
                    Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-start path=" + elementPath);
                    var property = _serialized?.FindProperty(elementPath);
                    if (property == null)
                    {
                        Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-skip path=" + elementPath + " reason=missing-property");
                        continue;
                    }
                    var owner = elementPath.StartsWith("commands.", StringComparison.Ordinal)
                        ? "commands"
                        : "commandList";
                    var field = new PropertyField(property, elementPath)
                    {
                        name = "spire-direct-property-" + owner,
                    };
                    container.Add(field);
                    field.Bind(_serialized!);
                    Debug.Log("SPIRE_UNITY_VARIANT_INSPECTOR_SETUP field-bind-done path=" + elementPath);
                }
            }
        }
    }

}
