#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Spire.UnityAsmdefIntegration.RuntimeAuto
{
    public sealed class OverlapConstructorPlayerProbe : MonoBehaviour
    {
        private const string ReportArgument = "-spire-overlap-report";
        private const string ReportFileName = "overlap-constructor-player-report.json";

        private void Start()
        {
#if ENABLE_IL2CPP
            const string backend = "IL2CPP";
#else
            const string backend = "Mono";
#endif

            var report = new PlayerReport
            {
                backend = backend,
                status = "fail",
                errors = string.Empty,
                executablePath = GetExecutablePath(),
            };

            try
            {
                if (AsmdefProbe.Verify())
                {
                    report.status = "pass";
                }
                else
                {
                    report.errors = "AsmdefProbe.Verify returned false.";
                }
            }
            catch (Exception exception)
            {
                report.errors = exception.ToString();
                Debug.LogException(exception);
            }

            var reportPath = ResolveReportPath();
            try
            {
                var directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                report.status = "fail";
                report.errors = string.IsNullOrEmpty(report.errors)
                    ? "Could not write the player report: " + exception
                    : report.errors + Environment.NewLine + "Could not write the player report: " + exception;
                Debug.LogException(exception);
            }

            Application.Quit(report.status == "pass" ? 0 : 1);
        }

        private static string ResolveReportPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                var argument = arguments[index];
                if (string.Equals(argument, ReportArgument, StringComparison.Ordinal))
                {
                    if (index + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                        return Path.GetFullPath(arguments[index + 1]);
                    break;
                }

                if (argument.StartsWith(ReportArgument + "=", StringComparison.Ordinal))
                {
                    var value = argument[(ReportArgument.Length + 1)..];
                    if (!string.IsNullOrWhiteSpace(value))
                        return Path.GetFullPath(value);
                    break;
                }
            }

            var logPath = Application.consoleLogPath;
            var logDirectory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDirectory))
                return Path.Combine(logDirectory, ReportFileName);

            return Path.Combine(Application.persistentDataPath, ReportFileName);
        }

        private static string GetExecutablePath()
        {
            var arguments = Environment.GetCommandLineArgs();
            return arguments.Length == 0 ? string.Empty : arguments[0];
        }

        [Serializable]
        private sealed class PlayerReport
        {
            public string backend = string.Empty;
            public string status = string.Empty;
            public string errors = string.Empty;
            public string executablePath = string.Empty;
        }
    }
}
