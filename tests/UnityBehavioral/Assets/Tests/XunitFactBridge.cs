#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Spire.UnityBehavioralTests;

/// <summary>
/// Runs the existing xUnit behavioral tests through Unity's NUnit test adapter.
/// </summary>
public static class XunitTestBridge
{
    private const string FactAttributeName = "Xunit.FactAttribute";
    private const string TheoryAttributeName = "Xunit.TheoryAttribute";
    private const string SkipPropertyName = "Skip";

    public static IReadOnlyList<XunitFactCase> Discover(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        return Discover(assembly.GetTypes().Where(type =>
            !type.IsDefined(typeof(XunitBridgeInfrastructureAttribute), inherit: false)));
    }

    internal static IReadOnlyList<XunitFactCase> Discover(IEnumerable<Type> types)
    {
        if (types == null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        var cases = new List<XunitFactCase>();
        foreach (var type in types.OrderBy(GetTypeName, StringComparer.Ordinal))
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Instance |
                         BindingFlags.Static |
                         BindingFlags.DeclaredOnly)
                     .OrderBy(method => method.MetadataToken))
            {
                var attributes = method.GetCustomAttributes(inherit: false);
                var theoryAttribute = attributes.FirstOrDefault(IsTheoryAttribute);
                if (theoryAttribute != null)
                {
                    throw Unsupported(method, "Xunit.Theory is not supported by the Unity bridge.");
                }

                var factAttribute = attributes.FirstOrDefault(IsFactAttribute);
                if (factAttribute == null)
                {
                    continue;
                }

                ValidateFact(type, method, factAttribute);
                cases.Add(new XunitFactCase(type, method));
            }
        }

        return cases
            .OrderBy(testCase => testCase.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static async Task InvokeAsync(XunitFactCase testCase)
    {
        if (testCase == null)
        {
            throw new ArgumentNullException(nameof(testCase));
        }

        object? instance = null;
        Exception? failure = null;
        try
        {
            instance = Activator.CreateInstance(testCase.DeclaringType);
            var result = testCase.Method.Invoke(instance, parameters: null);
            await AwaitResult(result, testCase.Method.ReturnType).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = UnwrapInvocationException(exception);
        }

        try
        {
            await DisposeAsync(instance).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var disposalFailure = UnwrapInvocationException(exception);
            failure = failure == null
                ? disposalFailure
                : new AggregateException(failure, disposalFailure);
        }

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void ValidateFact(Type type, MethodInfo method, object factAttribute)
    {
        var fullName = GetMethodName(method);
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            throw Unsupported(method, "Fact methods must belong to a concrete, non-generic class.");
        }

        if (!type.IsPublic && !type.IsNestedPublic)
        {
            throw Unsupported(method, "Fact methods must belong to a public class.");
        }

        if (method.IsStatic)
        {
            throw Unsupported(method, "Static Fact methods are not supported by the Unity bridge.");
        }

        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
        {
            throw Unsupported(method, "Generic Fact methods are not supported by the Unity bridge.");
        }

        if (method.GetParameters().Length != 0)
        {
            throw Unsupported(method, "Fact methods with parameters are not supported by the Unity bridge.");
        }

        if (!IsSupportedReturnType(method.ReturnType))
        {
            throw Unsupported(method, $"Fact method '{fullName}' must return void, Task, or ValueTask.");
        }

        var skip = factAttribute.GetType().GetProperty(
            SkipPropertyName,
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(factAttribute, index: null) as string;
        if (!string.IsNullOrEmpty(skip))
        {
            throw Unsupported(method, "Skipped Xunit.Fact methods are not supported by the Unity bridge.");
        }

        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw Unsupported(method, "Fact classes must have a public parameterless constructor.");
        }
    }

    private static bool IsSupportedReturnType(Type returnType)
    {
        if (returnType == typeof(void) || typeof(Task).IsAssignableFrom(returnType))
        {
            return true;
        }

        return IsValueTask(returnType);
    }

    private static async Task AwaitResult(object? result, Type returnType)
    {
        if (result == null || returnType == typeof(void))
        {
            return;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        if (IsValueTask(returnType))
        {
            var asTask = returnType.GetMethod(
                "AsTask",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (asTask == null)
            {
                throw new InvalidOperationException(
                    $"ValueTask return type '{returnType.FullName}' does not expose AsTask().");
            }

            var taskResult = asTask.Invoke(result, parameters: null) as Task;
            if (taskResult == null)
            {
                throw new InvalidOperationException(
                    $"ValueTask return type '{returnType.FullName}' produced no Task.");
            }

            await taskResult.ConfigureAwait(false);
        }
    }

    private static async Task DisposeAsync(object? instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool IsFactAttribute(object attribute)
        => string.Equals(attribute.GetType().FullName, FactAttributeName, StringComparison.Ordinal);

    private static bool IsTheoryAttribute(object attribute)
        => string.Equals(attribute.GetType().FullName, TheoryAttributeName, StringComparison.Ordinal);

    private static bool IsValueTask(Type type)
    {
        if (type.FullName == "System.Threading.Tasks.ValueTask")
        {
            return true;
        }

        return type.IsGenericType &&
               type.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1";
    }

    private static Exception UnwrapInvocationException(Exception exception)
        => exception is TargetInvocationException target && target.InnerException != null
            ? target.InnerException
            : exception;

    private static InvalidOperationException Unsupported(MethodInfo method, string reason)
        => new InvalidOperationException($"Unsupported xUnit test '{GetMethodName(method)}': {reason}");

    private static string GetTypeName(Type type)
        => type.FullName ?? type.Name;

    private static string GetMethodName(MethodInfo method)
        => $"{GetTypeName(method.DeclaringType!)}.{method.Name}";
}

public sealed class XunitFactCase
{
    internal XunitFactCase(Type declaringType, MethodInfo method)
    {
        DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
        Method = method ?? throw new ArgumentNullException(nameof(method));
        FullName = $"{GetTypeName(declaringType)}.{method.Name}";
    }

    public Type DeclaringType { get; }

    public MethodInfo Method { get; }

    public string FullName { get; }

    private static string GetTypeName(Type type)
        => type.FullName ?? type.Name;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class XunitBridgeInfrastructureAttribute : Attribute
{
}

[XunitBridgeInfrastructure]
public sealed class XunitFactNUnitRunner
{
    public static IEnumerable<TestCaseData> Cases()
    {
        return XunitTestBridge.Discover(typeof(XunitFactNUnitRunner).Assembly)
            .Select(testCase => new TestCaseData(testCase).SetName(testCase.FullName));
    }

    [TestCaseSource(nameof(Cases))]
    public async Task Run(XunitFactCase testCase)
    {
        await XunitTestBridge.InvokeAsync(testCase);
        // Unity Test Framework 1.4.5 retains a stale yield object when the final
        // Task completes synchronously. Let its coroutine observe a pending Task.
        await Task.Yield();
    }
}
