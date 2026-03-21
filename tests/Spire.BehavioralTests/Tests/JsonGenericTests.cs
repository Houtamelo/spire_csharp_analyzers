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
        Assert.Equal(JsonOptionAddStj<int>.Kind.Some, d.tag);
        Assert.Equal(42, d.value);
    }

    [Fact]
    public void Stj_Add_StringSome_RoundTrip()
    {
        var o = JsonOptionAddStj<string>.Some("hello");
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionAddStj<string>>(json);
        Assert.Equal(JsonOptionAddStj<string>.Kind.Some, d.tag);
        Assert.Equal("hello", d.value);
    }

    [Fact]
    public void Stj_Add_None_RoundTrip()
    {
        var o = JsonOptionAddStj<int>.None();
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionAddStj<int>>(json);
        Assert.Equal(JsonOptionAddStj<int>.Kind.None, d.tag);
    }

    // ── System.Text.Json — BoxedFields ──────────────────────────

    [Fact]
    public void Stj_Bf_IntSome_RoundTrip()
    {
        var o = JsonOptionBfStj<int>.Some(99);
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionBfStj<int>>(json);
        Assert.Equal(JsonOptionBfStj<int>.Kind.Some, d.tag);
        Assert.Equal(99, d.value);
    }

    // ── System.Text.Json — BoxedTuple ───────────────────────────

    [Fact]
    public void Stj_Bt_IntSome_RoundTrip()
    {
        var o = JsonOptionBtStj<int>.Some(77);
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonOptionBtStj<int>>(json);
        Assert.Equal(JsonOptionBtStj<int>.Kind.Some, d.tag);
        Assert.Equal(77, d.value);
    }

    // ── Newtonsoft — Additive ───────────────────────────────────

    [Fact]
    public void Nsj_Add_IntSome_RoundTrip()
    {
        var o = JsonOptionAddNsj<int>.Some(42);
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<int>>(json);
        Assert.Equal(JsonOptionAddNsj<int>.Kind.Some, d.tag);
        Assert.Equal(42, d.value);
    }

    [Fact]
    public void Nsj_Add_StringSome_RoundTrip()
    {
        var o = JsonOptionAddNsj<string>.Some("hello");
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<string>>(json);
        Assert.Equal(JsonOptionAddNsj<string>.Kind.Some, d.tag);
        Assert.Equal("hello", d.value);
    }

    [Fact]
    public void Nsj_Add_None_RoundTrip()
    {
        var o = JsonOptionAddNsj<int>.None();
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionAddNsj<int>>(json);
        Assert.Equal(JsonOptionAddNsj<int>.Kind.None, d.tag);
    }

    // ── Newtonsoft — BoxedFields ────────────────────────────────

    [Fact]
    public void Nsj_Bf_IntSome_RoundTrip()
    {
        var o = JsonOptionBfNsj<int>.Some(99);
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionBfNsj<int>>(json);
        Assert.Equal(JsonOptionBfNsj<int>.Kind.Some, d.tag);
        Assert.Equal(99, d.value);
    }

    // ── Newtonsoft — BoxedTuple ─────────────────────────────────

    [Fact]
    public void Nsj_Bt_IntSome_RoundTrip()
    {
        var o = JsonOptionBtNsj<int>.Some(77);
        var json = JsonConvert.SerializeObject(o);
        var d = JsonConvert.DeserializeObject<JsonOptionBtNsj<int>>(json);
        Assert.Equal(JsonOptionBtNsj<int>.Kind.Some, d.tag);
        Assert.Equal(77, d.value);
    }
}
