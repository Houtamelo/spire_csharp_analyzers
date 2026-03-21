using Xunit;

namespace Spire.BehavioralTests.Tests;

public class ClassTests
{
    [Fact]
    public void Construction_And_FieldAccess()
    {
        var stop = new CommandCls.Stop("shutdown");
        Assert.Equal("shutdown", stop.Reason);
    }

    [Fact]
    public void TypePatternMatching()
    {
        CommandCls cmd = new CommandCls.Stop("reason");
        var result = cmd switch
        {
            CommandCls.Start => "start",
            CommandCls.Stop s => $"stop:{s.Reason}",
            _ => "unknown"
        };
        Assert.Equal("stop:reason", result);
    }

    [Fact]
    public void AllVariants_Match()
    {
        Assert.Equal("start", Match(new CommandCls.Start()));
        Assert.Equal("stop", Match(new CommandCls.Stop("x")));

        static string Match(CommandCls c) => c switch
        {
            CommandCls.Start => "start",
            CommandCls.Stop => "stop",
            _ => "unknown"
        };
    }

    [Fact]
    public void Polymorphism()
    {
        CommandCls cmd = new CommandCls.Start();
        Assert.IsType<CommandCls.Start>(cmd);
        Assert.IsAssignableFrom<CommandCls>(cmd);
    }

    [Fact]
    public void Sealed_And_Abstract()
    {
        Assert.True(typeof(CommandCls).IsAbstract);
        Assert.True(typeof(CommandCls.Start).IsSealed);
        Assert.True(typeof(CommandCls.Stop).IsSealed);
    }
}
