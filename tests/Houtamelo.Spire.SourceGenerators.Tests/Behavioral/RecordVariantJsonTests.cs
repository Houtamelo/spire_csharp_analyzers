using System;
using Houtamelo.Spire;
using Newtonsoft.Json;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// JSON integration coverage for authored record-struct payloads.
public sealed class RecordVariantJsonTests : BehavioralTestBase
{
    [Fact]
    public void SystemTextJson_MixedRecordVariantRoundTripsUsingConfiguredNames()
    {
        var assembly = CompileAndLoad(SystemTextJsonSource);
        var union = GetType(assembly, "RecordJsonUnion");
        var probe = GetType(assembly, "RecordJsonSourceProbe");
        var value = probe.GetMethod("MakeSlider")!.Invoke(null, null)!;

        var json = System.Text.Json.JsonSerializer.Serialize(value, union);
        Assert.Contains("\"slider\"", json);
        Assert.Contains("\"amount\"", json);

        var roundTrip = System.Text.Json.JsonSerializer.Deserialize(json, union)!;
        Assert.Equal(GetKindValue(union, "Slider"), ReadKind(roundTrip));
        var payload = ReadPayload(roundTrip, "slider");
        Assert.Equal(7, payload.GetType().GetProperty("value")!.GetValue(payload));
    }

    [Fact]
    public void NewtonsoftJson_MixedRecordVariantRoundTripsUsingConfiguredNames()
    {
        var assembly = CompileAndLoad(NewtonsoftJsonSource);
        var union = GetType(assembly, "RecordJsonUnion");
        var probe = GetType(assembly, "RecordJsonSourceProbe");
        var value = probe.GetMethod("MakeSlider")!.Invoke(null, null)!;

        var json = JsonConvert.SerializeObject(value);
        Assert.Contains("\"slider\"", json);
        Assert.Contains("\"amount\"", json);

        var roundTrip = JsonConvert.DeserializeObject(json, union)!;
        Assert.Equal(GetKindValue(union, "Slider"), ReadKind(roundTrip));
        var payload = ReadPayload(roundTrip, "slider");
        Assert.Equal(7, payload.GetType().GetProperty("value")!.GetValue(payload));
    }

    private const string SystemTextJsonSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct RecordJsonUnion
        {
            [Variant, JsonName("slider")]
            public record struct Slider([JsonName("amount")] int value);
            [Variant] public static partial RecordJsonUnion Empty();
        }
        public static class RecordJsonSourceProbe
        {
            public static RecordJsonUnion MakeSlider() => new RecordJsonUnion.Slider(7);
        }
        """;

    private const string NewtonsoftJsonSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
        public partial struct RecordJsonUnion
        {
            [Variant, JsonName("slider")]
            public record struct Slider([JsonName("amount")] int value);
            [Variant] public static partial RecordJsonUnion Empty();
        }
        public static class RecordJsonSourceProbe
        {
            public static RecordJsonUnion MakeSlider() => new RecordJsonUnion.Slider(7);
        }
        """;
}
