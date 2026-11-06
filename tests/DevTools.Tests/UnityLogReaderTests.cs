using System;
using System.IO;
using System.Reflection;
using System.Text;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityLogReaderTests
{
    private static readonly Func<string, string> ReadSharedText =
        (Func<string, string>)typeof(ToolContext).Assembly.GetType("Spire.Tool.FileOps", throwOnError: true)!
            .GetMethod("ReadSharedText", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<string, string>));

    [Fact]
    public void ReadSharedText_ReadsFlushedOutputWithoutClosingTheRealWriter()
    {
        using var fixture = new LogFixture();
        using var writer = new FileStream(fixture.Path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        var first = Encoding.UTF8.GetBytes("ready → Ω\r\n");
        writer.Write(first, 0, first.Length);
        writer.Flush();

        Assert.Equal("ready → Ω\r\n", ReadSharedText(fixture.Path));

        var second = Encoding.UTF8.GetBytes("finished\n");
        writer.Write(second, 0, second.Length);
        writer.Flush();
        Assert.Equal("ready → Ω\r\nfinished\n", ReadSharedText(fixture.Path));
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    public void ReadSharedText_DetectsBomAndPreservesLineEndings(string encodingName)
    {
        using var fixture = new LogFixture();
        const string text = "assertion Ω\r\nnext line\n";
        File.WriteAllText(fixture.Path, text, Encoding.GetEncoding(encodingName));

        Assert.Equal(text, ReadSharedText(fixture.Path));
    }

    [Fact]
    public void ReadSharedText_StillRejectsAnExclusivelyLockedFile()
    {
        using var fixture = new LogFixture();
        using var writer = new FileStream(fixture.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        Assert.Throws<IOException>(() => ReadSharedText(fixture.Path));
    }

    [Fact]
    public void ReadSharedText_StillRejectsAMissingFile()
    {
        using var fixture = new LogFixture();

        Assert.Throws<FileNotFoundException>(() => ReadSharedText(fixture.Path));
    }

    private sealed class LogFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "spire-unity-log-" + Guid.NewGuid().ToString("N") + ".log");

        public void Dispose() => File.Delete(Path);
    }
}
