#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Spire.UnityWebBehavioralTests
{

    [Serializable]
    internal sealed class WebBehavioralCaseResult
    {
        public string name = string.Empty;
        public string status = string.Empty;
        public string error = string.Empty;
    }

    [Serializable]
    internal sealed class WebBehavioralResult
    {
        public string unityVersion = string.Empty;
        public string backend = string.Empty;
        public string runtimePlatform = string.Empty;
        public int processorBits;
        public bool debugBuild;
        public string status = string.Empty;
        public int expectedFacts;
        public int executedFacts;
        public WebBehavioralCaseResult[] cases = Array.Empty<WebBehavioralCaseResult>();
        public string[] failures = Array.Empty<string>();
        public bool forcedFailure;
    }

    [Serializable]
    internal sealed class WebFactManifest
    {
        public string[] names = Array.Empty<string>();
    }

    [UnityEngine.Scripting.Preserve]
    internal sealed class WebBehavioralRunner : MonoBehaviour
    {
        private const string Marker = "SPIRE_WEB_BEHAVIORAL_RESULT: ";
        private const string FactMarker = "SPIRE_WEB_FACT_RESULT: ";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRunner()
        {
            var host = new GameObject("SpireWebBehavioralRunner");
            DontDestroyOnLoad(host);
            host.AddComponent<WebBehavioralRunner>();
        }

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var cases = new List<WebBehavioralCaseResult>();
            var failures = new List<string>();
            var report = new WebBehavioralResult
            {
                unityVersion = Application.unityVersion,
                backend = "IL2CPP",
                runtimePlatform = Application.platform.ToString(),
                processorBits = IntPtr.Size * 8,
                debugBuild = Debug.isDebugBuild,
                cases = Array.Empty<WebBehavioralCaseResult>(),
                failures = Array.Empty<string>(),
            };

            IReadOnlyList<WebFactCase>? discovered = null;
            Exception? setupFailure = null;
            try
            {
                Require(Application.platform == RuntimePlatform.WebGLPlayer, "Application.platform was not WebGLPlayer.");
#if !ENABLE_IL2CPP
                throw new InvalidOperationException("WebGL behavioral runtime was not built with IL2CPP.");
#endif
                var manifestAsset = Resources.Load<TextAsset>("spire-web-facts");
                Require(manifestAsset != null, "The original behavioral fact manifest was not included.");
                var manifest = JsonUtility.FromJson<WebFactManifest>(manifestAsset!.text);
                Require(manifest != null && manifest.names != null, "The behavioral fact manifest was invalid.");
                var expectedNames = manifest!.names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
                discovered = WebXunitBridge.Discover(typeof(WebBehavioralRunner).Assembly);
                var actualNames = discovered.Select(testCase => testCase.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                Require(expectedNames.SequenceEqual(actualNames),
                    "Behavioral fact identity mismatch. Expected " + expectedNames.Length + ", discovered " + actualNames.Length + ".");
                report.expectedFacts = expectedNames.Length;
            }
            catch (Exception exception)
            {
                setupFailure = exception;
            }

            if (setupFailure != null)
            {
                failures.Add(setupFailure.ToString());
            }
            else
            {
                foreach (var testCase in discovered!)
                {
                    var task = WebXunitBridge.InvokeAsync(testCase);
                    while (!task.IsCompleted)
                        yield return null;

                    var result = new WebBehavioralCaseResult
                    {
                        name = testCase.FullName,
                        status = task.IsFaulted || task.IsCanceled ? "fail" : "pass",
                        error = task.IsFaulted
                            ? task.Exception?.GetBaseException().ToString() ?? "Fact task faulted."
                            : task.IsCanceled ? "Fact task was canceled." : string.Empty,
                    };
                    cases.Add(result);
                    report.executedFacts++;
                    if (result.status == "fail")
                        failures.Add(result.name + ": " + result.error);
                    EmitCase(result);
                }

                if (HasFailureQuery())
                {
                    report.forcedFailure = true;
                    var syntheticMethod = typeof(WebNegativeControlFixture).GetMethod(
                        nameof(WebNegativeControlFixture.Fail),
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    Require(syntheticMethod != null, "The WebGL negative-control method was not found.");
                    var syntheticCase = new WebFactCase(typeof(WebNegativeControlFixture), syntheticMethod!);
                    var syntheticTask = WebXunitBridge.InvokeAsync(syntheticCase);
                    while (!syntheticTask.IsCompleted)
                        yield return null;

                    var syntheticError = syntheticTask.IsFaulted
                        ? syntheticTask.Exception?.GetBaseException().ToString() ?? "Synthetic negative control faulted without an exception."
                        : "Intentional browser transport negative control unexpectedly passed.";
                    failures.Add("spireFailure=1: " + syntheticError);
                    EmitCase(new WebBehavioralCaseResult
                    {
                        name = "spireFailure=1",
                        status = "fail",
                        error = syntheticError,
                    });
                }
            }

            report.status = failures.Count == 0 && report.executedFacts == report.expectedFacts ? "pass" : "fail";
            report.cases = cases.ToArray();
            report.failures = failures.ToArray();
            Emit(report);
        }

        private static bool HasFailureQuery()
            => Application.absoluteURL.IndexOf("spireFailure=1", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void EmitCase(WebBehavioralCaseResult result)
            => Debug.Log(FactMarker + JsonUtility.ToJson(result, false));

        private static void Emit(WebBehavioralResult result)
        {
            var json = JsonUtility.ToJson(result, false);
            Debug.Log(Marker + json);
#if UNITY_WEBGL
            SpireWebReport("behavioral", json);
#endif
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

#if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void SpireWebReport(string kind, string json);
#endif
    }

    public sealed class WebNegativeControlFixture
    {
        public void Fail()
            => Xunit.Assert.True(false, "Intentional browser transport negative control.");
    }

}
