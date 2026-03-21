using System.Text.Json;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class JsonClassTests
{
    // ── System.Text.Json ────────────────────────────────────────

    [Fact]
    public void Stj_WithField_RoundTrip()
    {
        JsonCmdStj cmd = new JsonCmdStj.Stop("shutdown");
        var json = System.Text.Json.JsonSerializer.Serialize(cmd);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonCmdStj>(json)!;
        var stop = Assert.IsType<JsonCmdStj.Stop>(d);
        Assert.Equal("shutdown", stop.Reason);
    }

    [Fact]
    public void Stj_Fieldless_RoundTrip()
    {
        JsonCmdStj cmd = new JsonCmdStj.Start();
        var json = System.Text.Json.JsonSerializer.Serialize(cmd);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonCmdStj>(json)!;
        Assert.IsType<JsonCmdStj.Start>(d);
    }

    [Fact]
    public void Stj_Discriminator_Present()
    {
        JsonCmdStj cmd = new JsonCmdStj.Stop("x");
        var json = System.Text.Json.JsonSerializer.Serialize(cmd);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Stop\"", json);
        Assert.Contains("\"Reason\"", json);
    }

    // ── Newtonsoft.Json ─────────────────────────────────────────

    [Fact]
    public void Nsj_WithField_RoundTrip()
    {
        JsonCmdNsj cmd = new JsonCmdNsj.Stop("shutdown");
        var json = JsonConvert.SerializeObject(cmd);
        var d = JsonConvert.DeserializeObject<JsonCmdNsj>(json)!;
        var stop = Assert.IsType<JsonCmdNsj.Stop>(d);
        Assert.Equal("shutdown", stop.Reason);
    }

    [Fact]
    public void Nsj_Fieldless_RoundTrip()
    {
        JsonCmdNsj cmd = new JsonCmdNsj.Start();
        var json = JsonConvert.SerializeObject(cmd);
        var d = JsonConvert.DeserializeObject<JsonCmdNsj>(json)!;
        Assert.IsType<JsonCmdNsj.Start>(d);
    }

    [Fact]
    public void Nsj_Discriminator_Present()
    {
        JsonCmdNsj cmd = new JsonCmdNsj.Stop("x");
        var json = JsonConvert.SerializeObject(cmd);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Stop\"", json);
    }
}
