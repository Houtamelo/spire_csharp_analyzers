using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using EnvDTE;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Spire.VisualStudioIntegrationTestDriver
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public sealed class IntegrationDriver : IWpfTextViewCreationListener
    {
        private const string Title = "Replace with SpireEnum<T>.From(value)";
        private const string Cast = "(ProbeKind)value";
        private static int started;

        [Import] internal ITextDocumentFactoryService Documents = null;
        [Import] internal ILightBulbBroker LightBulbs = null;
        [Import] internal ISuggestedActionCategoryRegistryService Categories = null;
        [Import] internal ISuggestedActionPriorityRegistryService Priorities = null;
        [Import] internal ITableManagerProvider Tables = null;
        [Import] internal IUIThreadOperationExecutor Operations = null;
        [Import(typeof(SVsServiceProvider))] internal IServiceProvider Services = null;

        public void TextViewCreated(IWpfTextView view)
        {
            TryStart(view);
            view.VisualElement.Loaded += (_, __) => TryStart(view);
            view.VisualElement.Dispatcher.BeginInvoke(new Action(() => TryStart(view)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void TryStart(IWpfTextView view)
        {
            var root = Environment.GetEnvironmentVariable("SPIRE_VS_TEST_PROJECT");
            var report = Environment.GetEnvironmentVariable("SPIRE_VS_TEST_REPORT");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(report) ||
                !File.Exists(Path.Combine(root, ".spire-unity-asmdefs")) || view.IsClosed) return;
            var hasDocument = Documents.TryGetTextDocument(view.TextBuffer, out var document);
            File.AppendAllText(report + ".views.log", DateTime.UtcNow.ToString("O") + " " + (hasDocument ? document.FilePath : "<document-not-ready>") + Environment.NewLine);
            if (!hasDocument ||
                !string.Equals(Path.GetFullPath(document.FilePath), Path.Combine(Path.GetFullPath(root), "Assets", "RuntimeAuto", "_rider.cs"), StringComparison.OrdinalIgnoreCase) ||
                Interlocked.CompareExchange(ref started, 1, 0) != 0) return;
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => RunAsync(view, root, report));
        }

        private async Task RunAsync(IWpfTextView view, string root, string reportPath)
        {
            var report = new Dictionary<string, object>
            {
                ["status"] = "starting",
                ["startedUtc"] = DateTime.UtcNow.ToString("O"),
                ["discovered"] = false,
                ["applied"] = false,
                ["generatedSymbols"] = false,
                ["project"] = root,
                ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            var subscriptions = new Dictionary<ITableDataSource, Tuple<ErrorSink, IDisposable>>();
            DTE dte = null;
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                dte = (DTE)Services.GetService(typeof(SDTE));
                report["solution"] = dte.Solution.FullName;
                report["visualStudioVersion"] = FileVersionInfo.GetVersionInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ProductVersion;
                Write(reportPath, report);
                var source = Path.Combine(root, "Assets", "RuntimeAuto", "_rider.cs");
                var before = File.ReadAllText(source);
                if (before.IndexOf(Cast, StringComparison.Ordinal) < 0 || before.IndexOf(Cast, StringComparison.Ordinal) != before.LastIndexOf(Cast, StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected exactly one unfixed cast in the IDE fixture.");
                var expected = before.Replace(Cast, "SpireEnum<ProbeKind>.From(value)");
                report["sourceBeforeSha256"] = Hash(before);
                var matrix = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(root, "matrix.json")));
                var scenarios = (System.Collections.IEnumerable)matrix["scenarios"];
                var paths = scenarios.Cast<Dictionary<string, object>>().Select(item => ((string)item["spire002Location"]).Replace('/', Path.DirectorySeparatorChar)).ToArray();
                foreach (var path in paths) dte.ItemOperations.OpenFile(Path.Combine(root, path));
                dte.ItemOperations.OpenFile(source);
                dte.ExecuteCommand("View.ErrorList");
                var table = Tables.GetTableManager(StandardTables.ErrorsTable);
                var deadline = DateTime.UtcNow.AddMinutes(10);
                List<Dictionary<string, object>> rows = null;
                while (DateTime.UtcNow < deadline)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    foreach (var data in table.Sources)
                    {
                        if (subscriptions.ContainsKey(data)) continue;
                        var sink = new ErrorSink(data.Identifier);
                        subscriptions.Add(data, Tuple.Create(sink, data.Subscribe(sink)));
                    }
                    rows = subscriptions.Values.SelectMany(item => item.Item1.Rows()).ToList();
                    report["diagnostics"] = rows;
                    if (paths.All(path => rows.Any(row => (string)row["id"] == "SPIRE002" && EndsWith((string)row["file"], path))) &&
                        rows.Any(row => (string)row["id"] == "SPIRE016" && EndsWith((string)row["file"], "Assets/RuntimeAuto/_rider.cs"))) break;
                    report["status"] = "waiting-for-live-diagnostics";
                    Write(reportPath, report);
                    await Task.Delay(1000);
                }
                if (rows == null || !paths.All(path => rows.Any(row => (string)row["id"] == "SPIRE002" && EndsWith((string)row["file"], path))) ||
                    !rows.Any(row => (string)row["id"] == "SPIRE016" && EndsWith((string)row["file"], "Assets/RuntimeAuto/_rider.cs")))
                    throw new InvalidOperationException("Visual Studio did not publish the required live SPIRE002/SPIRE016 diagnostics.");
                report["expectedSPIRE002"] = paths.Length;
                report["status"] = "diagnostics-ready";
                Write(reportPath, report);
                ISuggestedAction chosen = null;
                var offered = new List<string>();
                var unavailableProviders = new HashSet<string>();
                while (chosen == null && DateTime.UtcNow < deadline)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    dte.ItemOperations.OpenFile(source);
                    var snapshot = view.TextBuffer.CurrentSnapshot;
                    var offset = snapshot.GetText().IndexOf(Cast, StringComparison.Ordinal);
                    if (offset < 0) throw new InvalidOperationException("Fixture changed before the Quick Action was applied.");
                    view.Caret.MoveTo(new SnapshotPoint(snapshot, offset));
                    view.VisualElement.Focus();
                    var span = new SnapshotSpan(snapshot, offset, Cast.Length);
                    foreach (var provider in LightBulbs.GetSuggestedActionsSources(view, view.TextBuffer))
                    {
                        // VS installs this placeholder even when Copilot is not configured.
                        if (provider.GetType().FullName == "Microsoft.VisualStudio.Editor.Implementation.CopilotStubs.FixWithCopilotSuggestedActionSource")
                        {
                            report["unavailableCopilotStub"] = provider.GetType().FullName;
                            continue;
                        }
                        try
                        {
                            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                            {
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                IEnumerable<SuggestedActionSet> sets;
                                if (provider is IAsyncSuggestedActionsSource asynchronous)
                                {
                                    var declared = DeclaredPriorities(provider.GetType());
                                    var selected = Priorities.Priorities.Where(priority => declared.Contains(priority)).ToArray();
                                    if (selected.Length == 0)
                                    {
                                        unavailableProviders.Add(provider.GetType().FullName + ": no exported priorities on its provider type");
                                        report["unavailableProviders"] = unavailableProviders.ToArray();
                                        continue;
                                    }
                                    report["actionProvider"] = provider.GetType().FullName;
                                    report["requestedPriorities"] = selected;
                                    var collectors = selected.Select(priority => new ActionCollector(priority)).ToArray();
                                    await asynchronous.GetSuggestedActionsAsync(Categories.AllCodeFixes, span, collectors.Cast<ISuggestedActionSetCollector>().ToImmutableArray(), cancellation.Token);
                                    sets = collectors.SelectMany(collector => collector.Sets()).ToArray();
                                }
                                else
                                {
                                    if (!await provider.HasSuggestedActionsAsync(Categories.AllCodeFixes, span, cancellation.Token)) continue;
                                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                    sets = provider.GetSuggestedActions(Categories.AllCodeFixes, span, cancellation.Token);
                                }
                                chosen = await FindActionAsync(sets, offered, cancellation.Token);
                                if (chosen != null) break;
                            }
                        }
                        catch (NotImplementedException)
                        {
                            unavailableProviders.Add(provider.GetType().FullName);
                            report["unavailableProviders"] = unavailableProviders.ToArray();
                        }
                    }
                    if (chosen == null) { report["offered"] = offered.Distinct().ToArray(); Write(reportPath, report); await Task.Delay(1000); }
                }
                if (chosen == null) throw new InvalidOperationException("The actual IDE LightBulb sources did not offer the expected Spire Quick Action.");
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                report["discovered"] = true;
                report["action"] = chosen.DisplayText;
                if (chosen is ISuggestedAction3 modern)
                    Operations.Execute("Spire validation", "Applying the IDE Quick Action", false, true, context => modern.Invoke(context));
                else chosen.Invoke(CancellationToken.None);
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    dte.Documents.SaveAll();
                    if (File.ReadAllText(source) == expected) break;
                    await Task.Delay(200);
                }
                if (File.ReadAllText(source) != expected) throw new InvalidOperationException("The IDE did not persist the exact expected source transformation.");
                report["applied"] = true;
                report["sourceAfterSha256"] = Hash(expected);
                var refreshed = false;
                for (var attempt = 0; attempt < 120; attempt++)
                {
                    await Task.Delay(500);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    rows = subscriptions.Values.SelectMany(item => item.Item1.Rows()).ToList();
                    if (!rows.Any(row => (string)row["id"] == "SPIRE016" && EndsWith((string)row["file"], "Assets/RuntimeAuto/_rider.cs")) &&
                        paths.All(path => rows.Any(row => (string)row["id"] == "SPIRE002" && EndsWith((string)row["file"], path))))
                    { refreshed = true; break; }
                }
                if (!refreshed) throw new InvalidOperationException("Live diagnostics did not remove SPIRE016 while retaining the expected SPIRE002 diagnostics after the fix.");
                report["diagnosticsAfterFix"] = rows;
                report["diagnosticsRefreshed"] = true;
                await VerifyGeneratedSymbolAsync(dte, root);
                report["generatedSymbols"] = true;
                report["status"] = "pass";
            }
            catch (Exception error) { report["status"] = "fail"; report["error"] = error.ToString(); }
            finally
            {
                report["completedUtc"] = DateTime.UtcNow.ToString("O");
                Write(reportPath, report);
                foreach (var entry in subscriptions.Values) entry.Item2.Dispose();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (dte != null) dte.Quit();
            }
        }

        private static async Task<ISuggestedAction> FindActionAsync(IEnumerable<SuggestedActionSet> sets, List<string> offered, CancellationToken cancellation)
        {
            if (sets == null) return null;
            foreach (var set in sets)
                foreach (var action in set.Actions)
                {
                    offered.Add(action.DisplayText);
                    if (action.DisplayText.StartsWith(Title, StringComparison.Ordinal)) return action;
                    if (action.HasActionSets)
                    {
                        var result = await FindActionAsync(await action.GetActionSetsAsync(cancellation), offered, cancellation);
                        if (result != null) return result;
                    }
                }
            return null;
        }

        private static HashSet<string> DeclaredPriorities(Type source)
        {
            for (var type = source; type != null; type = type.DeclaringType)
            {
                var declared = type.GetCustomAttributes(typeof(SuggestedActionPriorityAttribute), false)
                    .Cast<SuggestedActionPriorityAttribute>().Select(attribute => attribute.Priority).ToArray();
                if (declared.Length != 0) return new HashSet<string>(declared);
            }
            return new HashSet<string>();
        }

        private static async Task VerifyGeneratedSymbolAsync(DTE dte, string root)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var source = Path.Combine(root, "Assets", "RuntimeAuto", "RuntimeAuto.cs");
            var document = dte.ItemOperations.OpenFile(source).Document;
            var text = File.ReadAllText(source);
            var offset = text.IndexOf("RuntimeAutoUnion.Kind", StringComparison.Ordinal) + "RuntimeAutoUnion.".Length;
            ((TextSelection)document.Selection).MoveToAbsoluteOffset(offset + 1);
            dte.ExecuteCommand("Edit.GoToDefinition");
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await Task.Delay(200);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var active = dte.ActiveDocument;
                if (active == null || string.Equals(active.FullName, source, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(active.Object("TextDocument") is TextDocument target)) continue;
                var generated = target.StartPoint.CreateEditPoint().GetText(target.EndPoint);
                if (generated.Contains("enum Kind") && generated.Contains("Value")) return;
            }
            throw new InvalidOperationException("Go To Definition did not resolve the generated Kind symbol in the IDE.");
        }

        private static bool EndsWith(string actual, string expected)
            => actual.Replace('\\', '/').EndsWith(expected.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static void Write(string path, object report)
            => File.WriteAllText(path, new JavaScriptSerializer().Serialize(report));
        private static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private sealed class ActionCollector : ISuggestedActionSetCollector
        {
            private readonly List<SuggestedActionSet> sets = new List<SuggestedActionSet>();
            public ActionCollector(string priority) { Priority = priority; }
            public string Priority { get; }
            public void Add(SuggestedActionSet set) { lock (sets) sets.Add(set); }
            public void Complete() { }
            public SuggestedActionSet[] Sets() { lock (sets) return sets.ToArray(); }
        }

        private sealed class ErrorSink : ITableDataSink
        {
            private readonly object sync = new object();
            private readonly string source;
            private readonly HashSet<ITableEntriesSnapshotFactory> factories = new HashSet<ITableEntriesSnapshotFactory>();
            private readonly HashSet<ITableEntriesSnapshot> snapshots = new HashSet<ITableEntriesSnapshot>();
            private readonly HashSet<ITableEntry> entries = new HashSet<ITableEntry>();
            public ErrorSink(string source) { this.source = source; }
            public bool IsStable { get; set; }
            public void AddEntries(IReadOnlyList<ITableEntry> items, bool removeAllEntries) { lock (sync) { if (removeAllEntries) RemoveAllEntries(); foreach (var item in items) entries.Add(item); } }
            public void AddFactory(ITableEntriesSnapshotFactory item, bool removeAllFactories) { lock (sync) { if (removeAllFactories) factories.Clear(); factories.Add(item); } }
            public void AddSnapshot(ITableEntriesSnapshot item, bool removeAllSnapshots) { lock (sync) { if (removeAllSnapshots) snapshots.Clear(); snapshots.Add(item); } }
            public void FactorySnapshotChanged(ITableEntriesSnapshotFactory item) { }
            public void RemoveAllEntries() { lock (sync) { entries.Clear(); snapshots.Clear(); factories.Clear(); } }
            public void RemoveAllFactories() { lock (sync) factories.Clear(); }
            public void RemoveAllSnapshots() { lock (sync) snapshots.Clear(); }
            public void RemoveEntries(IReadOnlyList<ITableEntry> items) { lock (sync) foreach (var item in items) entries.Remove(item); }
            public void RemoveFactory(ITableEntriesSnapshotFactory item) { lock (sync) factories.Remove(item); }
            public void RemoveSnapshot(ITableEntriesSnapshot item) { lock (sync) snapshots.Remove(item); }
            public void ReplaceEntries(IReadOnlyList<ITableEntry> oldItems, IReadOnlyList<ITableEntry> newItems) { RemoveEntries(oldItems); AddEntries(newItems, false); }
            public void ReplaceFactory(ITableEntriesSnapshotFactory oldItem, ITableEntriesSnapshotFactory newItem) { RemoveFactory(oldItem); AddFactory(newItem, false); }
            public void ReplaceSnapshot(ITableEntriesSnapshot oldItem, ITableEntriesSnapshot newItem) { RemoveSnapshot(oldItem); AddSnapshot(newItem, false); }
            public List<Dictionary<string, object>> Rows()
            {
                lock (sync)
                {
                    var result = new List<Dictionary<string, object>>();
                    foreach (var snapshot in snapshots.Concat(factories.Select(factory => factory.GetCurrentSnapshot())).Where(item => item != null))
                        for (var index = 0; index < snapshot.Count; index++)
                        {
                            snapshot.TryGetValue(index, StandardTableKeyNames.ErrorCode, out var code);
                            snapshot.TryGetValue(index, StandardTableKeyNames.DocumentName, out var file);
                            snapshot.TryGetValue(index, StandardTableKeyNames.Text, out var message);
                            snapshot.TryGetValue(index, StandardTableKeyNames.Line, out var line);
                            if (code is string id && id.StartsWith("SPIRE", StringComparison.Ordinal))
                                result.Add(new Dictionary<string, object> { ["id"] = id, ["file"] = file as string ?? "", ["message"] = message, ["line"] = line, ["source"] = source });
                        }
                    return result;
                }
            }
        }
    }
}
