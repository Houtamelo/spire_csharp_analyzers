using System;
using System.Runtime.InteropServices;
using Xunit;

namespace DevTools.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Skip = "Requires Linux; current platform is " + PlatformName.Current + ".";
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Requires Windows; current platform is " + PlatformName.Current + ".";
    }
}

internal static class PlatformName
{
    public static string Current
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            return "Other";
        }
    }
}
