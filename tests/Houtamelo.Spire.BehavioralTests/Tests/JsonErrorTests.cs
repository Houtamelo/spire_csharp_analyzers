using System;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class JsonErrorTests
{
    // ── STJ — Missing discriminator ─────────────────────────────

    [Fact]
    public void Stj_MissingDiscriminator_Throws()
    {
        var json = """{"radius":3.14}""";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json));
    }

    // ── STJ — Unknown kind value ────────────────────────────────

    [Fact]
    public void Stj_UnknownKind_Throws()
    {
        var json = """{"kind":"Triangle","base":1.0}""";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json));
    }

    // ── STJ — Empty object ──────────────────────────────────────

    [Fact]
    public void Stj_EmptyObject_Throws()
    {
        var json = """{}""";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json));
    }

    // ── NSJ — Missing discriminator ─────────────────────────────

    [Fact]
    public void Nsj_MissingDiscriminator_Throws()
    {
        var json = """{"radius":3.14}""";
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject<JsonShapeAddNsj>(json));
    }

    // ── NSJ — Unknown kind value ────────────────────────────────

    [Fact]
    public void Nsj_UnknownKind_Throws()
    {
        var json = """{"kind":"Triangle","base":1.0}""";
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject<JsonShapeAddNsj>(json));
    }

    // ── STJ — Wrong discriminator name ignored ──────────────────

    [Fact]
    public void Stj_WrongDiscriminatorName_Throws()
    {
        // Union expects "kind", but JSON has "type"
        var json = """{"type":"Circle","radius":3.14}""";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json));
    }
}
