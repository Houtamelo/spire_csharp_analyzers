using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Houtamelo.Spire;

/// <summary>
/// Base type for payload objects emitted for Unity-serializable discriminated
/// unions. It intentionally has no Unity dependency so the runtime bridge can
/// be referenced by the normal netstandard package.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[Serializable]
public abstract class UnitySerializationPayloadBase
{
    [NonSerialized]
    private object? _owner;

    /// <summary>
    /// Claims this payload for one generated union runtime copy. The owner is
    /// runtime-only, so Unity can still preserve managed-reference identity
    /// while copied values detach before a refresh mutates the payload.
    /// </summary>
    /// <param name="owner">The runtime token of the generated union copy.</param>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public bool __SpireTryClaim(object owner)
    {
        if (_owner is null)
        {
            _owner = owner;
            return true;
        }

        return ReferenceEquals(_owner, owner);
    }

    /// <summary>Clears the runtime-only ownership stamp after deserialization.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void __SpireResetOwner()
        => _owner = null;
}

/// <summary>
/// Managed-only support for preparing and hydrating generated unions nested in
/// ordinary serializable structs, classes, arrays, and lists. The bridge returns
/// detached snapshots, caches the small field plans it needs, and never calls
/// user serialization callbacks.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class UnitySerializationBridge
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldPlans =
        new ConcurrentDictionary<Type, FieldInfo[]>();

    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", InstanceFlags)!;

    /// <summary>
    /// Prepares generated union caches below a managed composite while preserving
    /// scalar/reference values in a detached managed snapshot.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Prepare(object? value)
        => Transform(value, restore: false, new TransformState(), prior: null);

    /// <summary>
    /// Prepares a managed value while reusing a prior generated union snapshot
    /// wherever the value has the same generated union type.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Prepare(object? value, object? prior)
        => Transform(value, restore: false, new TransformState(), prior);

    /// <summary>
    /// Prepares a field whose concrete Unity-serializable shape is known only
    /// after a generic type parameter is constructed.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? PrepareDynamic(object? value, object? prior)
        => Transform(value, restore: false, new TransformState(), prior);

    /// <summary>Typed form of <see cref="Prepare(object?)"/>.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Prepare<T>(T value)
    {
        if (value is null)
            return value;

        var prepared = Prepare((object?)value);
        return prepared is null ? default! : (T)prepared;
    }

    /// <summary>Typed form of the prior-snapshot preparation operation.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Prepare<T>(T value, T prior)
    {
        if (value is null)
            return value;

        var prepared = Prepare((object?)value, prior);
        return prepared is null ? default! : (T)prepared;
    }

    /// <summary>
    /// Hydrates generated union runtime slots below a managed composite while
    /// preserving scalar/reference values in a detached managed snapshot.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Restore(object? value)
        => Transform(value, restore: true, new TransformState(), prior: null);

    /// <summary>Hydrates a runtime value whose concrete shape is generic.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? RestoreDynamic(object? value)
        => Transform(value, restore: true, new TransformState(), prior: null);

    /// <summary>Typed form of <see cref="Restore(object?)"/>.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Restore<T>(T value)
    {
        if (value is null)
            return value;

        var restored = Restore((object?)value);
        return restored is null ? default! : (T)restored;
    }

    private static object? Transform(
        object? value,
        bool restore,
        TransformState state,
        object? prior)
    {
        if (value is null)
            return null;

        var type = value.GetType();
        if (IsSimple(type) || IsUnityNativeReference(type))
            return value;

        var generatedMethod = type.GetMethod(
            restore ? "__SpireHydrateSerialized" : "__SpirePrepareForSerialization",
            InstanceFlags,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        if (generatedMethod is not null &&
            type.GetField("_serializedPayload", InstanceFlags) is not null)
        {
            if (!restore && prior is not null && prior.GetType() == type)
            {
                var cacheField = type.GetField("_serializedPayload", InstanceFlags);
                if (cacheField is not null && cacheField.GetValue(value) is null)
                {
                    var priorPayload = cacheField.GetValue(prior);
                    var ownerField = type.GetField("__SpireSerializationOwner", InstanceFlags);
                    var owner = ownerField?.GetValue(value);
                    if (owner is null ||
                        priorPayload is not UnitySerializationPayloadBase typedPayload ||
                        typedPayload.__SpireTryClaim(owner))
                    {
                        cacheField.SetValue(value, priorPayload);
                    }
                }
            }

            var result = generatedMethod.Invoke(value, null);
            return restore ? result ?? value : value;
        }

        if (value is IList list)
        {
            if (!ContainsGeneratedState(type, new HashSet<Type>()))
                return value;

            return TransformList(value, list, restore, state, prior);
        }

        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false) ||
            !ContainsGeneratedState(type, new HashSet<Type>()))
            return value;

        if (state.Copies.TryGetValue(value, out var existingCopy))
            return existingCopy;

        var copy = type.IsValueType
            ? value
            : MemberwiseCloneMethod.Invoke(value, null)!;
        state.Copies[value] = copy;

        foreach (var field in GetFieldPlan(type))
        {
            var child = field.GetValue(value);

            var priorChild = prior is null || prior.GetType() != type
                ? null
                : field.GetValue(prior);
            var transformed = Transform(child, restore, state, priorChild);
            if (field.FieldType.IsValueType || !ReferenceEquals(child, transformed))
                field.SetValue(copy, transformed);
        }

        return copy;
    }

    private static object TransformList(
        object value,
        IList list,
        bool restore,
        TransformState state,
        object? prior)
    {
        if (state.Copies.TryGetValue(value, out var existingCopy))
            return existingCopy;

        var copy = CreateListCopy(value, list.Count);
        state.Copies[value] = copy;

        for (var index = 0; index < list.Count; index++)
        {
            var child = list[index];
            var priorChild = GetListElement(prior, index);
            var transformed = Transform(child, restore, state, priorChild);
            if (copy is Array array)
                array.SetValue(transformed, index);
            else
                ((IList)copy).Add(transformed);
        }

        return copy;
    }

    private static object CreateListCopy(object value, int count)
    {
        var type = value.GetType();
        if (type.IsArray)
            return Array.CreateInstance(type.GetElementType()!, count);

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return Activator.CreateInstance(type, count) ??
                throw new InvalidOperationException($"Could not create a copy of {type}.");
        }

        throw new InvalidOperationException($"Unsupported serialized collection type {type}.");
    }

    private static FieldInfo[] GetFieldPlan(Type type)
        => FieldPlans.GetOrAdd(type, static candidate =>
        {
            var fields = new List<FieldInfo>();
            for (var current = candidate;
                 current is not null && current != typeof(object);
                 current = current.BaseType)
            {
                fields.AddRange(current
                    .GetFields(InstanceFlags | BindingFlags.DeclaredOnly)
                    .Where(IsSerializedField));
            }

            fields.Reverse();
            return fields.ToArray();
        });

    private static bool ContainsGeneratedState(Type type, HashSet<Type> visiting)
    {
        if (HasGeneratedUnionMethods(type))
            return true;

        if (type.IsArray)
            return ContainsGeneratedState(type.GetElementType()!, visiting);

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return ContainsGeneratedState(type.GetGenericArguments()[0], visiting);
        }

        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false) ||
            !visiting.Add(type))
        {
            return false;
        }

        try
        {
            return GetFieldPlan(type).Any(field =>
                ContainsGeneratedState(field.FieldType, visiting));
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static bool HasGeneratedUnionMethods(Type type)
        => type.GetMethod(
               "__SpirePrepareForSerialization",
               InstanceFlags,
               binder: null,
               Type.EmptyTypes,
               modifiers: null) is not null &&
            type.GetField("_serializedPayload", InstanceFlags) is not null;

    private static bool IsSerializedField(FieldInfo field)
    {
        if (field.IsStatic || field.IsLiteral || field.IsInitOnly ||
            field.IsDefined(typeof(NonSerializedAttribute), inherit: false))
        {
            return false;
        }

        if (field.IsPublic)
            return true;

        return field.GetCustomAttributes(inherit: false).Any(attribute =>
            attribute.GetType().FullName == "UnityEngine.SerializeField");
    }

    private static bool IsSimple(Type type)
        => type == typeof(string) ||
            type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(Guid);

    private static bool IsUnityNativeReference(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(
                    current.FullName,
                    "UnityEngine.Object",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        var assemblyName = type.Assembly.GetName().Name;
        return type.Namespace is not null &&
            type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal) &&
            assemblyName is not null &&
            assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal);
    }

    private static object? GetListElement(object? value, int index)
    {
        if (value is not IList list || index < 0 || index >= list.Count)
            return null;

        return list[index];
    }

    private sealed class TransformState
    {
        internal readonly Dictionary<object, object> Copies =
            new Dictionary<object, object>(ReferenceComparer.Instance);
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();

        public new bool Equals(object? left, object? right)
            => ReferenceEquals(left, right);

        public int GetHashCode(object value)
            => RuntimeHelpers.GetHashCode(value);
    }
}
