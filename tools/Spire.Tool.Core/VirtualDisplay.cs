namespace Spire.Tool;

internal static class VirtualDisplay
{
    internal static async Task<T> RunAsync<T>(
        string workingDirectory,
        string? errorLog,
        Func<string, Task<T>> action)
    {
        using var display = ProcessRunner.Start(
            "Xvfb",
            new[] { "-displayfd", "1", "-screen", "0", "1440x1000x24", "-nolisten", "tcp" },
            workingDirectory,
            ownProcessGroup: true);
        var errors = display.StandardError.ReadToEndAsync();
        try
        {
            var read = display.StandardOutput.ReadLineAsync();
            if (await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false) != read)
                throw new TimeoutException("Xvfb did not allocate a display.");
            var number = await read.ConfigureAwait(false);
            if (!int.TryParse(number, out _))
                throw new InvalidOperationException("Xvfb did not allocate a display.");

            return await action(":" + number).ConfigureAwait(false);
        }
        finally
        {
            ProcessRunner.Kill(display, ownProcessGroup: true);
            await DevTools.ProcessCompatibility.WaitForExitAsync(display).ConfigureAwait(false);
            var error = await errors.ConfigureAwait(false);
            if (errorLog != null)
                FileOps.Write(errorLog, error);
        }
    }
}
