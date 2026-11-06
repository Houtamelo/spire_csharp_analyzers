using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace DevTools.Tests;

public sealed class PlatformFactContractTests
{
    [Fact]
    public void LinuxFact_UsesCanonicalSkipReasonForCurrentHost()
    {
        var method = typeof(PlatformFactFixture).GetMethod(
            nameof(PlatformFactFixture.Linux),
            BindingFlags.Instance | BindingFlags.Public)!;
        var attribute = method.GetCustomAttribute<LinuxFactAttribute>()!;

        Assert.Equal(
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? null : "Requires Linux; current platform is " + CurrentPlatform() + ".",
            attribute.Skip);
    }

    [Fact]
    public void WindowsFact_UsesCanonicalSkipReasonForCurrentHost()
    {
        var method = typeof(PlatformFactFixture).GetMethod(
            nameof(PlatformFactFixture.Windows),
            BindingFlags.Instance | BindingFlags.Public)!;
        var attribute = method.GetCustomAttribute<WindowsFactAttribute>()!;

        Assert.Equal(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? null : "Requires Windows; current platform is " + CurrentPlatform() + ".",
            attribute.Skip);
    }

    [Fact]
    public void PlatformFactAttributes_DoNotChangeTheMethodIdentityOrFactContract()
    {
        var linux = typeof(PlatformFactFixture).GetMethod(nameof(PlatformFactFixture.Linux))!;
        var windows = typeof(PlatformFactFixture).GetMethod(nameof(PlatformFactFixture.Windows))!;

        Assert.Contains(linux.GetCustomAttributes<FactAttribute>(), attribute => attribute is LinuxFactAttribute);
        Assert.Contains(windows.GetCustomAttributes<FactAttribute>(), attribute => attribute is WindowsFactAttribute);
        Assert.Empty(linux.GetParameters());
        Assert.Empty(windows.GetParameters());
    }

    private static string CurrentPlatform()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
            : "Other";

    public abstract class PlatformFactFixture
    {
        [LinuxFact]
        public void Linux()
        {
        }

        [WindowsFact]
        public void Windows()
        {
        }
    }
}
