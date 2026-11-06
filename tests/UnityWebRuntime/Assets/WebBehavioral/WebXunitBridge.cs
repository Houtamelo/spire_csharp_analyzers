#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Spire.UnityWebBehavioralTests
{

    internal sealed class WebFactCase
    {
        internal WebFactCase(Type type, MethodInfo method)
        {
            DeclaringType = type;
            Method = method;
            FullName = (type.FullName ?? type.Name) + "." + method.Name;
        }

        internal Type DeclaringType { get; }
        internal MethodInfo Method { get; }
        internal string FullName { get; }
    }

    internal static class WebXunitBridge
    {
        private const string FactAttributeName = "Xunit.FactAttribute";
        private const string TheoryAttributeName = "Xunit.TheoryAttribute";

        internal static IReadOnlyList<WebFactCase> Discover(Assembly assembly)
        {
            var cases = new List<WebFactCase>();
            foreach (var type in assembly.GetTypes()
                         .Where(type => type.IsClass &&
                             type.IsPublic &&
                             type.Namespace != null &&
                             type.Namespace.StartsWith("Spire.BehavioralTests.Tests", StringComparison.Ordinal))
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(method => method.MetadataToken))
                {
                    var attributes = method.GetCustomAttributes(inherit: false);
                    if (attributes.Any(attribute => attribute.GetType().FullName == TheoryAttributeName))
                        throw Unsupported(method, "Xunit.Theory is not supported by the WebGL bridge.");

                    var fact = attributes.FirstOrDefault(attribute => attribute.GetType().FullName == FactAttributeName);
                    if (fact == null)
                        continue;

                    var skip = fact.GetType().GetProperty("Skip", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(fact, index: null) as string;
                    if (!string.IsNullOrEmpty(skip))
                        throw Unsupported(method, "Skipped Xunit.Fact methods are not supported by the WebGL bridge.");
                    if (method.GetParameters().Length != 0)
                        throw Unsupported(method, "Fact methods with parameters are not supported by the WebGL bridge.");
                    if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
                        throw Unsupported(method, "Generic Fact methods are not supported by the WebGL bridge.");
                    if (!IsSupportedReturnType(method.ReturnType))
                        throw Unsupported(method, "Fact methods must return void, Task, or ValueTask.");
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        throw Unsupported(method, "Fact classes must have a public parameterless constructor.");

                    cases.Add(new WebFactCase(type, method));
                }
            }

            return cases.OrderBy(testCase => testCase.FullName, StringComparer.Ordinal).ToArray();
        }

        internal static async Task InvokeAsync(WebFactCase testCase)
        {
            object? instance = null;
            Exception? failure = null;
            try
            {
                instance = Activator.CreateInstance(testCase.DeclaringType);
                var result = testCase.Method.Invoke(instance, parameters: null);
                await AwaitResult(result, testCase.Method.ReturnType);
            }
            catch (Exception exception)
            {
                failure = Unwrap(exception);
            }

            try
            {
                if (instance is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (instance is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception exception)
            {
                var disposalFailure = Unwrap(exception);
                failure = failure == null ? disposalFailure : new AggregateException(failure, disposalFailure);
            }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static async Task AwaitResult(object? result, Type returnType)
        {
            if (result == null || returnType == typeof(void))
                return;
            if (result is Task task)
            {
                await task;
                return;
            }
            if (returnType.FullName == "System.Threading.Tasks.ValueTask")
            {
                var asTask = returnType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
                if (asTask == null)
                    throw new InvalidOperationException("ValueTask does not expose AsTask().");
                await (Task)asTask.Invoke(result, parameters: null)!;
                return;
            }
            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1")
            {
                var asTask = returnType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
                if (asTask == null)
                    throw new InvalidOperationException("ValueTask<T> does not expose AsTask().");
                await (Task)asTask.Invoke(result, parameters: null)!;
            }
        }

        private static bool IsSupportedReturnType(Type type)
            => type == typeof(void) || typeof(Task).IsAssignableFrom(type) ||
               type.FullName == "System.Threading.Tasks.ValueTask" ||
               (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1");

        private static Exception Unwrap(Exception exception)
            => exception is TargetInvocationException target && target.InnerException != null
                ? target.InnerException
                : exception;

        private static InvalidOperationException Unsupported(MethodInfo method, string reason)
            => new("Unsupported WebGL Fact '" + (method.DeclaringType?.FullName ?? method.DeclaringType?.Name) +
                "." + method.Name + "': " + reason);
    }

}
