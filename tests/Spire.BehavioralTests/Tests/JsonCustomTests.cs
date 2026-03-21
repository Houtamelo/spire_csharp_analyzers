using System.Text.Json;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class JsonCustomTests
{
    // ── Custom Discriminator — STJ ──────────────────────────────

    [Fact]
    public void Stj_CustomDiscriminator_UsesType()
    {
        var c = JsonCustomDiscStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        Assert.Contains("\"type\"", json);
        Assert.DoesNotContain("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }

    [Fact]
    public void Stj_CustomDiscriminator_RoundTrip()
    {
        var c = JsonCustomDiscStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonCustomDiscStj>(json);
        Assert.Equal(JsonCustomDiscStj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void Stj_CustomDiscriminator_Fieldless()
    {
        var p = JsonCustomDiscStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonCustomDiscStj>(json);
        Assert.Equal(JsonCustomDiscStj.Kind.Point, d.tag);
    }

    // ── Custom Discriminator — NSJ ──────────────────────────────

    [Fact]
    public void Nsj_CustomDiscriminator_UsesType()
    {
        var c = JsonCustomDiscNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        Assert.Contains("\"type\"", json);
        Assert.DoesNotContain("\"kind\"", json);
    }

    [Fact]
    public void Nsj_CustomDiscriminator_RoundTrip()
    {
        var c = JsonCustomDiscNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonCustomDiscNsj>(json);
        Assert.Equal(JsonCustomDiscNsj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }

    // ── JsonName Attribute — STJ ────────────────────────────────

    [Fact]
    public void Stj_JsonName_VariantOverride()
    {
        var c = JsonNamedStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        // [JsonName("circle")] overrides variant discriminator value
        Assert.Contains("\"circle\"", json);
        Assert.DoesNotContain("\"Circle\"", json);
    }

    [Fact]
    public void Stj_JsonName_FieldOverride()
    {
        var c = JsonNamedStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        // [JsonName("r")] overrides field name in JSON
        Assert.Contains("\"r\"", json);
        Assert.DoesNotContain("\"radius\"", json);
    }

    [Fact]
    public void Stj_JsonName_RoundTrip()
    {
        var c = JsonNamedStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonNamedStj>(json);
        Assert.Equal(JsonNamedStj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void Stj_JsonName_Fieldless()
    {
        var p = JsonNamedStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        Assert.Contains("\"pt\"", json);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonNamedStj>(json);
        Assert.Equal(JsonNamedStj.Kind.Point, d.tag);
    }

    [Fact]
    public void Stj_JsonName_MultiField()
    {
        var r = JsonNamedStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        Assert.Contains("\"rect\"", json);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonNamedStj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    // ── JsonName Attribute — NSJ ────────────────────────────────

    [Fact]
    public void Nsj_JsonName_VariantOverride()
    {
        var c = JsonNamedNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        Assert.Contains("\"circle\"", json);
    }

    [Fact]
    public void Nsj_JsonName_FieldOverride()
    {
        var c = JsonNamedNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        Assert.Contains("\"r\"", json);
    }

    [Fact]
    public void Nsj_JsonName_RoundTrip()
    {
        var c = JsonNamedNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonNamedNsj>(json);
        Assert.Equal(JsonNamedNsj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }
}
