using System.Text.Json;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class JsonGenericTests
{
    // ── System.Text.Json — Additive ─────────────────────────────

    [Fact]
    public void Stj_Add_IntSome_RoundTrip()
    {
        var o = JsonOptionAddStj<int>.Some(42);
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionAddStj<int>>(json);
        Assert.Equal(JsonOptionAddStj<int>.Kind.Some, d.kind);
        Assert.Equal(42, d.some.value);
    }

    [Fact]
    public void Stj_Add_StringSome_RoundTrip()
    {
        var o = JsonOptionAddStj<string>.Some("hello");
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionAddStj<string>>(json);
        Assert.Equal(JsonOptionAddStj<string>.Kind.Some, d.kind);
        Assert.Equal("hello", d.some.value);
    }

    [Fact]
    public void Stj_Add_None_RoundTrip()
    {
        var o = JsonOptionAddStj<int>.None();
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionAddStj<int>>(json);
        Assert.Equal(JsonOptionAddStj<int>.Kind.None, d.kind);
    }

    // ── Newtonsoft — Additive ───────────────────────────────────

    [Fact]
    public void Nsj_Add_IntSome_RoundTrip()
    {
        var o = JsonOptionAddNsj<int>.Some(42);
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<int>>(json);
        Assert.Equal(JsonOptionAddNsj<int>.Kind.Some, d.kind);
        Assert.Equal(42, d.some.value);
    }

    [Fact]
    public void Nsj_Add_StringSome_RoundTrip()
    {
        var o = JsonOptionAddNsj<string>.Some("hello");
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<string>>(json);
        Assert.Equal(JsonOptionAddNsj<string>.Kind.Some, d.kind);
        Assert.Equal("hello", d.some.value);
    }

    [Fact]
    public void Nsj_Add_None_RoundTrip()
    {
        var o = JsonOptionAddNsj<int>.None();
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<int>>(json);
        Assert.Equal(JsonOptionAddNsj<int>.Kind.None, d.kind);
    }

}
