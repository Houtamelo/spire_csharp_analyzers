#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using NUnitTestCaseData = NUnit.Framework.TestCaseData;
using NUnitTestCaseSourceAttribute = NUnit.Framework.TestCaseSourceAttribute;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Spire.UnityTestHarness;

public static class XunitReflectionBridge
{
    public const string TestAssembliesEnvironmentVariable = "SPIRE_UNITY_TEST_ASSEMBLIES";
    public const string DiscoveryManifestEnvironmentVariable = "SPIRE_UNITY_TEST_DISCOVERY_PATH";

    private const BindingFlags TestMethodFlags =
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    public static IReadOnlyList<XunitReflectionCase> DiscoverLoadedAssemblies()
    {
        return Discover(LoadConfiguredAssemblies());
    }

    public static IReadOnlyList<Assembly> LoadConfiguredAssemblies()
    {
        var raw = Environment.GetEnvironmentVariable(TestAssembliesEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"{TestAssembliesEnvironmentVariable} must list the portable xUnit test assemblies separated by ';'.");
        }

        var names = raw.Split(';')
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToArray();
        if (names.Length == 0)
            throw new InvalidOperationException($"{TestAssembliesEnvironmentVariable} listed no assemblies.");

        var duplicates = names
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"{TestAssembliesEnvironmentVariable} listed duplicate assemblies: " +
                string.Join(", ", duplicates));
        }

        var assemblies = new List<Assembly>(names.Length);
        foreach (var name in names)
        {
            var matching = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => string.Equals(
                    assembly.GetName().Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matching.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple loaded assemblies match configured test assembly '{name}'.");
            }

            Assembly assembly;
            try
            {
                assembly = matching.Length == 1
                    ? matching[0]
                    : Assembly.Load(new AssemblyName(name));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not load configured test assembly '{name}'.", exception);
            }

            if (!string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Configured test assembly '{name}' resolved to '{assembly.GetName().Name}'.");
            }

            assemblies.Add(assembly);
        }

        return assemblies;
    }

    public static IReadOnlyList<XunitReflectionCase> Discover(Assembly assembly)
        => Discover(new[] { assembly ?? throw new ArgumentNullException(nameof(assembly)) });

    public static IReadOnlyList<XunitReflectionCase> Discover(IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null)
            throw new ArgumentNullException(nameof(assemblies));

        var requested = assemblies.ToArray();
        var cases = Discover(requested.SelectMany(assembly => GetConcreteTypes(assembly)
            .Where(type => !type.IsDefined(typeof(XunitBridgeInfrastructureAttribute), false))));
        foreach (var assembly in requested)
        {
            var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "Unknown";
            if (!cases.Any(testCase => string.Equals(
                    testCase.AssemblyName,
                    assemblyName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Configured test assembly '{assemblyName}' contained no discoverable xUnit tests.");
            }
        }

        return cases;
    }

    internal static IReadOnlyList<XunitReflectionCase> Discover(IEnumerable<Type> types)
    {
        if (types == null)
            throw new ArgumentNullException(nameof(types));

        var cases = new List<XunitReflectionCase>();
        foreach (var fixtureType in types
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
                     .OrderBy(type => TypeName(type), StringComparer.Ordinal))
        {
            foreach (var method in fixtureType.GetMethods(TestMethodFlags)
                         .OrderBy(GetMethodSortKey, StringComparer.Ordinal))
            {
                var fact = method.GetCustomAttributes(inherit: true)
                    .OfType<FactAttribute>()
                    .FirstOrDefault();
                if (fact == null)
                    continue;

                ValidateMethod(fixtureType, method, fact);
                if (fact is TheoryAttribute)
                {
                    ExpandTheory(cases, fixtureType, method, fact);
                }
                else
                {
                    cases.Add(new XunitReflectionCase(
                        fixtureType,
                        method,
                        Array.Empty<object?>(),
                        rowIndex: null));
                }
            }
        }

        var ordered = cases
            .OrderBy(testCase => testCase.Identity, StringComparer.Ordinal)
            .ToArray();
        var duplicateIdentities = ordered
            .GroupBy(testCase => testCase.Identity, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIdentities.Length > 0)
        {
            throw new InvalidOperationException(
                "Duplicate xUnit case identities: " + string.Join(", ", duplicateIdentities));
        }

        return ordered;
    }

    public static async Task InvokeAsync(XunitReflectionCase testCase)
    {
        if (testCase == null)
            throw new ArgumentNullException(nameof(testCase));

        object? instance = null;
        Exception? failure = null;
        try
        {
            instance = CreateFixture(testCase.FixtureType);
            if (instance is IAsyncLifetime asyncLifetime)
                await asyncLifetime.InitializeAsync().ConfigureAwait(false);

            var result = testCase.Method.Invoke(instance, testCase.Arguments);
            await AwaitResult(result, testCase.Method.ReturnType).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = UnwrapInvocationException(exception);
        }

        try
        {
            await DisposeFixtureAsync(instance).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var disposalFailure = UnwrapInvocationException(exception);
            failure = failure == null
                ? disposalFailure
                : new AggregateException(failure, disposalFailure);
        }

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public static string ExportDiscoveryManifest(IEnumerable<XunitReflectionCase>? cases = null)
    {
        var discovered = (cases ?? DiscoverLoadedAssemblies())
            .OrderBy(testCase => testCase.Identity, StringComparer.Ordinal)
            .ToArray();
        var duplicateIdentities = discovered
            .GroupBy(testCase => testCase.Identity, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIdentities.Length > 0)
        {
            throw new InvalidOperationException(
                "Duplicate xUnit case identities: " + string.Join(", ", duplicateIdentities));
        }

        var builder = new StringBuilder("{\"cases\":[");
        for (var index = 0; index < discovered.Length; index++)
        {
            if (index > 0)
                builder.Append(',');
            builder.Append("{\"identity\":\"")
                .Append(JsonEscape(discovered[index].Identity))
                .Append("\",\"displayName\":\"")
                .Append(JsonEscape(discovered[index].DisplayName))
                .Append("\",\"assembly\":\"")
                .Append(JsonEscape(discovered[index].AssemblyName))
                .Append("\",\"fixture\":\"")
                .Append(JsonEscape(TypeName(discovered[index].FixtureType)))
                .Append("\",\"method\":\"")
                .Append(JsonEscape(discovered[index].Method.Name))
                .Append("\",\"row\":")
                .Append(discovered[index].RowIndex?.ToString(CultureInfo.InvariantCulture) ?? "null")
                .Append(",\"arguments\":")
                .Append(discovered[index].ArgumentsJson)
                .Append('}');
        }

        return builder.Append("]}").ToString();
    }

    private static void ExpandTheory(
        List<XunitReflectionCase> cases,
        Type fixtureType,
        MethodInfo method,
        FactAttribute fact)
    {
        var dataAttributes = method.GetCustomAttributes(inherit: true)
            .OfType<DataAttribute>()
            .ToArray();
        if (dataAttributes.Length == 0)
        {
            throw Unsupported(method, "a Theory has no DataAttribute.");
        }

        var rowIndex = 0;
        foreach (var dataAttribute in dataAttributes)
        {
            IEnumerable<object[]> rows;
            try
            {
                rows = dataAttribute.GetData(method)
                    ?? throw new InvalidOperationException("the DataAttribute returned null.");
            }
            catch (Exception exception)
            {
                throw Unsupported(method, $"data discovery failed: {UnwrapInvocationException(exception).Message}");
            }

            foreach (var row in rows)
            {
                if (row == null)
                    throw Unsupported(method, "the DataAttribute returned a null row.");

                if (row.Length != method.GetParameters().Length)
                {
                    throw Unsupported(
                        method,
                        $"data row {rowIndex} has {row.Length} values but the method expects " +
                        $"{method.GetParameters().Length}.");
                }

                cases.Add(new XunitReflectionCase(fixtureType, method, row, rowIndex));
                rowIndex++;
            }
        }

        if (rowIndex == 0)
            throw Unsupported(method, "all DataAttributes produced zero rows.");
    }

    private static void ValidateMethod(Type fixtureType, MethodInfo method, FactAttribute fact)
    {
        if (fact.Skip is not null)
            throw Unsupported(method, "Skip is not supported by the Unity bridge.");
        if (method.IsStatic)
            throw Unsupported(method, "static test methods are not supported.");
        if (method.IsGenericMethod || method.ContainsGenericParameters)
            throw Unsupported(method, "generic test methods are not supported.");
        if (!fixtureType.IsPublic && !fixtureType.IsNestedPublic)
            throw Unsupported(method, "test fixtures must be public.");
        if (fixtureType.GetConstructors(TestMethodFlags).Length == 0)
            throw Unsupported(method, "the fixture has no usable constructor.");
        if (!IsSupportedReturnType(method.ReturnType))
            throw Unsupported(method, "test methods must return void, Task, or ValueTask.");
        if (fact is not TheoryAttribute && method.GetParameters().Length != 0)
            throw Unsupported(method, "Fact methods cannot have parameters.");
    }

    private static object CreateFixture(Type fixtureType)
    {
        var constructors = fixtureType.GetConstructors(TestMethodFlags)
            .OrderBy(constructor => constructor.GetParameters().Length);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            var supported = true;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameterType = parameters[index].ParameterType;
                if (parameterType == typeof(ITestOutputHelper))
                    arguments[index] = new NullTestOutputHelper();
                else if (parameterType == typeof(IMessageSink))
                    arguments[index] = new NullMessageSink();
                else if (parameters[index].HasDefaultValue)
                    arguments[index] = parameters[index].DefaultValue;
                else
                    supported = false;
            }

            if (supported)
            {
                try
                {
                    return constructor.Invoke(arguments);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }
        }

        throw new InvalidOperationException(
            $"Unsupported xUnit fixture '{TypeName(fixtureType)}': no constructor can be supplied by the Unity bridge.");
    }

    private static async Task DisposeFixtureAsync(object? instance)
    {
        if (instance is IAsyncLifetime asyncLifetime)
            await asyncLifetime.DisposeAsync().ConfigureAwait(false);

        if (instance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (instance is IDisposable disposable)
            disposable.Dispose();
    }

    private static async Task AwaitResult(object? result, Type returnType)
    {
        if (result == null || returnType == typeof(void))
            return;
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        if (!IsValueTask(returnType))
            return;
        var asTask = returnType.GetMethod("AsTask", BindingFlags.Instance | BindingFlags.Public);
        if (asTask == null)
            throw new InvalidOperationException($"ValueTask type '{returnType.FullName}' has no AsTask().");
        var taskResult = asTask.Invoke(result, null) as Task;
        if (taskResult == null)
            throw new InvalidOperationException($"ValueTask type '{returnType.FullName}' returned no Task.");
        await taskResult.ConfigureAwait(false);
    }

    private static bool IsSupportedReturnType(Type type)
        => type == typeof(void) || typeof(Task).IsAssignableFrom(type) || IsValueTask(type);

    private static bool IsValueTask(Type type)
        => type.FullName == "System.Threading.Tasks.ValueTask" ||
           type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1";

    private static IEnumerable<Type> GetConcreteTypes(Assembly assembly)
        => GetTypes(assembly)
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters);

    private static Type[] GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var details = string.Join(
                "; ",
                exception.LoaderExceptions
                    .Where(error => error != null)
                    .Select(error => error!.Message));
            throw new InvalidOperationException(
                $"Could not load test types from '{assembly.FullName}': {details}", exception);
        }
    }

    private static string GetMethodSortKey(MethodInfo method)
        => $"{method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))})";

    private static string TypeName(Type type)
        => type.FullName ?? type.Name;

    private static InvalidOperationException Unsupported(MethodInfo method, string reason)
        => new($"Unsupported xUnit test '{TypeName(method.DeclaringType!)}.{method.Name}': {reason}");

    private static Exception UnwrapInvocationException(Exception exception)
        => exception is TargetInvocationException target && target.InnerException != null
            ? target.InnerException
            : exception;

    private static string JsonEscape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}

public sealed class XunitReflectionCase
{
    internal XunitReflectionCase(Type fixtureType, MethodInfo method, object?[] arguments, int? rowIndex)
    {
        FixtureType = fixtureType;
        Method = method;
        Arguments = arguments;
        RowIndex = rowIndex;
        AssemblyName = fixtureType.Assembly.GetName().Name ?? fixtureType.Assembly.FullName ?? "Unknown";
        MethodIdentity = $"{AssemblyName}:{TypeName(fixtureType)}.{method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))})";
        ArgumentsJson = FormatArgumentsJson(arguments);
        var hasArguments = arguments.Length > 0 || rowIndex is not null;
        Identity = !hasArguments
            ? MethodIdentity
            : $"{MethodIdentity}[{ArgumentsJson}]";
        DisplayName = !hasArguments
            ? $"{TypeName(fixtureType)}.{method.Name}"
            : $"{TypeName(fixtureType)}.{method.Name}({FormatArguments(arguments)})";
    }

    public Type FixtureType { get; }
    public MethodInfo Method { get; }
    public object?[] Arguments { get; }
    public int? RowIndex { get; }
    public string AssemblyName { get; }
    public string MethodIdentity { get; }
    public string Identity { get; }
    public string DisplayName { get; }
    public string ArgumentsJson { get; }

    private static string TypeName(Type type) => type.FullName ?? type.Name;

    private static string FormatArguments(IEnumerable<object?> arguments)
        => string.Join(", ", arguments.Select(FormatArgument));

    private static string FormatArgumentsJson(IEnumerable<object?> arguments)
        => $"[{string.Join(",", arguments.Select(FormatArgumentJson))}]";

    private static string FormatArgumentJson(object? value)
    {
        if (value == null)
            return "null";
        if (value is string text)
            return $"\"{JsonEscape(text)}\"";
        if (value is char character)
            return $"\"{JsonEscape(character.ToString())}\"";
        if (value is bool boolean)
            return boolean ? "true" : "false";
        if (value is Array array)
            return $"[{string.Join(",", array.Cast<object?>().Select(FormatArgumentJson))}]";
        if (value is Enum enumeration)
            return $"\"{JsonEscape(TypeName(value.GetType()) + "." + enumeration)}\"";
        if (value is byte or sbyte or ushort or short or uint or int or ulong or long or decimal)
            return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture) ?? "0";
        if (value is float single && !float.IsNaN(single) && !float.IsInfinity(single))
            return single.ToString(CultureInfo.InvariantCulture);
        if (value is double @double && !double.IsNaN(@double) && !double.IsInfinity(@double))
            return @double.ToString(CultureInfo.InvariantCulture);
        if (value is IFormattable formattable)
        {
            var formatted = formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
            return $"\"{JsonEscape(formatted)}\"";
        }

        return $"\"{JsonEscape(value.ToString() ?? value.GetType().Name)}\"";
    }

    private static string JsonEscape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string FormatArgument(object? value)
    {
        if (value == null)
            return "null";
        if (value is string text)
            return $"\"{text.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        if (value is char character)
            return $"'{character}'";
        if (value is Array array)
            return $"[{string.Join(", ", array.Cast<object?>().Select(FormatArgument))}]";
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? value.ToString()!;
        return value.ToString() ?? value.GetType().Name;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class XunitBridgeInfrastructureAttribute : Attribute
{
}

[XunitBridgeInfrastructure]
public sealed class XunitReflectionNUnitRunner
{
    public static IEnumerable<NUnitTestCaseData> Cases()
    {
        var cases = XunitReflectionBridge.DiscoverLoadedAssemblies();
        var manifestPath = Environment.GetEnvironmentVariable(
            XunitReflectionBridge.DiscoveryManifestEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            var directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                manifestPath,
                XunitReflectionBridge.ExportDiscoveryManifest(cases),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return cases
            .Select(testCase => new NUnitTestCaseData(testCase).SetName(testCase.DisplayName));
    }

    [NUnitTestCaseSourceAttribute(nameof(Cases))]
    public async Task Run(XunitReflectionCase testCase)
    {
        await XunitReflectionBridge.InvokeAsync(testCase);
    }
}

internal sealed class NullMessageSink : IMessageSink
{
    public bool OnMessage(IMessageSinkMessage message) => true;
}

internal sealed class NullTestOutputHelper : ITestOutputHelper
{
    public void Write(string format, params object[] args)
    {
    }

    public void WriteLine(string format)
    {
    }

    public void WriteLine(string format, params object[] args)
    {
    }
}
