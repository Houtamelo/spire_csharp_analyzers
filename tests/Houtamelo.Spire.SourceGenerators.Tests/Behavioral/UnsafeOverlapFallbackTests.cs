using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Regression coverage retained from the former fallback suite, now targeting
/// the safe variant-payload representation rather than the removed byte buffer.
public sealed class VariantPayloadStorageTests : BehavioralTestBase
{
    private const string UnknownSizeSource = """
        using Houtamelo.Spire;
        public struct Opaque { public long first; public long second; }
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command OpaqueValue(Opaque value);
            [Variant] public static partial Command Count(int count);
            [Variant] public static partial Command Empty();
        }
        """;

    [Fact]
    public void ConcreteUnmanagedPayload_RoundTripsWithoutUnsafeStorage()
    {
        var assembly = CompileAndLoad(UnknownSizeSource);
        var unionType = GetType(assembly, "Command");
        var opaqueType = GetType(assembly, "Opaque");
        var opaque = Activator.CreateInstance(opaqueType)!;
        opaqueType.GetField("first")!.SetValue(opaque, 11L);
        opaqueType.GetField("second")!.SetValue(opaque, 22L);
        var value = InvokeFactory(unionType, "OpaqueValue", opaque);
        var copied = RuntimeHelpers.GetObjectValue(value);
        var payload = ReadProperty(copied, "opaqueValue")!;
        var opaqueValue = payload.GetType().GetProperty("value")!.GetValue(payload)!;
        Assert.Equal(11L, opaqueValue.GetType().GetField("first")!.GetValue(opaqueValue));
        Assert.Equal(22L, opaqueValue.GetType().GetField("second")!.GetValue(opaqueValue));
    }

    [Fact]
    public void FieldlessAndDefaultVariantsRemainConstructible()
    {
        var assembly = CompileAndLoad(UnknownSizeSource);
        var unionType = GetType(assembly, "Command");
        var empty = InvokeFactory(unionType, "Empty");
        Assert.Equal(GetKindValue(unionType, "Empty"), ReadKind(empty));
        var count = InvokeFactory(unionType, "Count", 37);
        Assert.Equal(37, ReadProperty(count, "count"));
        var defaultValue = Activator.CreateInstance(unionType)!;
        Assert.Equal(GetKindValue(unionType, "OpaqueValue"), ReadKind(defaultValue));
    }

    [Fact]
    public void SharedReferenceStorage_PreservesNullAndIdentity()
    {
        const string source = """
            using Houtamelo.Spire;
            [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
            public partial struct Message
            {
                [Variant] public static partial Message Text(string text);
                [Variant] public static partial Message Number(int number);
            }
            """;
        var assembly = CompileAndLoad(source);
        var unionType = GetType(assembly, "Message");
        var text = new string(new[] { 's', 'h', 'a', 'r', 'e', 'd' });
        var value = InvokeFactory(unionType, "Text", text);
        Assert.Same(text, ReadProperty(value, "text"));
        Assert.Null(ReadProperty(InvokeFactory(unionType, "Text", (object)null!), "text"));
        Assert.Equal(7, ReadProperty(InvokeFactory(unionType, "Number", 7), "number"));
    }
}
