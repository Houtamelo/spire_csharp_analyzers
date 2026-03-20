//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default on a class without [MustBeInit].
#nullable enable
public class NoReport_DefaultPlainClass
{
    PlainClass? Ok() => default;
}
