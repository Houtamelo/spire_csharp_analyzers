#if !SPIRE_UNITY_TEST_HOST
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevTools.Tests;

// The Unity test host builds DevTools as a library without its MCP entry point.
public sealed class McpStartupTests
{
    [Theory]
    [InlineData("Information")]
    [InlineData("Trace")]
    public async Task Startup_DiscoversAndCallsToolsWithOnlyJsonOnStdout(string logLevel)
    {
        await RunServerAsync(logLevel, async (process, cancellation) =>
        {
            await InitializeAsync(process, cancellation);
            var result = await RequestAsync(process, 2, "tools/list", new { }, cancellation);
            Assert.Equal(new[]
            {
                "copy", "create_directory", "dotnet_build", "dotnet_restore", "dotnet_test",
                "list_files", "move", "parse_syntax_tree", "remove", "remove_recursive",
            }, result.GetProperty("result").GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString()).OrderBy(name => name, StringComparer.Ordinal));

            var call = await RequestAsync(process, 3, "tools/call", new
            {
                name = "parse_syntax_tree",
                arguments = new { code = "class Example { }" },
            }, cancellation);
            Assert.Contains("ClassDeclarationSyntax", call.GetProperty("result")
                .GetProperty("content")[0].GetProperty("text").GetString());
        });
    }

    [Fact]
    public async Task Startup_UnknownMethodReturnsProtocolErrorAndDiscoveryStillWorks()
    {
        await RunServerAsync("Trace", async (process, cancellation) =>
        {
            await InitializeAsync(process, cancellation);
            var failure = await RequestAsync(process, 2, "devtools/unknown", new { }, cancellation);
            Assert.Equal(-32601, failure.GetProperty("error").GetProperty("code").GetInt32());

            var result = await RequestAsync(process, 3, "tools/list", new { }, cancellation);
            Assert.Equal(10, result.GetProperty("result").GetProperty("tools").GetArrayLength());
        });
    }

    private static async Task InitializeAsync(Process process, CancellationToken cancellation)
    {
        var response = await RequestAsync(process, 1, "initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "devtools-startup-tests", version = "1.0" },
        }, cancellation);
        Assert.Equal("DevTools", response.GetProperty("result").GetProperty("serverInfo")
            .GetProperty("name").GetString());
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized",
        }));
        await process.StandardInput.FlushAsync(cancellation);
    }

    private static async Task<JsonElement> RequestAsync(
        Process process, int id, string method, object parameters, CancellationToken cancellation)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = parameters,
        }));
        await process.StandardInput.FlushAsync(cancellation);
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellation);
            Assert.False(line is null, "The MCP server closed stdout before responding to " + method);
            using var document = JsonDocument.Parse(line!);
            var message = document.RootElement;
            Assert.Equal("2.0", message.GetProperty("jsonrpc").GetString());
            if (message.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                return message.Clone();
        }
    }

    private static async Task RunServerAsync(
        string logLevel, Func<Process, CancellationToken, Task> exchange)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(typeof(SyntaxTreeTool).Assembly.Location);
        info.Environment["Logging__LogLevel__Default"] = logLevel;
        using var process = Process.Start(info)!;
        var errors = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await exchange(process, cancellation.Token);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await errors;
        }

        Assert.Contains("ModelContextProtocol", await errors);
    }
}
#endif
