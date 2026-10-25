using System;
using System.IO;
using System.Reflection;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

internal static class EmbeddedSourceTemplate
{
    internal static string Read(string basename)
    {
        var assembly = typeof(EmbeddedSourceTemplate).GetTypeInfo().Assembly;
        var prefix = typeof(EmbeddedSourceTemplate).Namespace + ".Templates.";
        var resourceName = prefix + basename;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded source template '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
