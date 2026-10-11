using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Houtamelo.Spire;

/// Base type for payload objects emitted for Unity-serializable unions.
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[Serializable]
public abstract class UnitySerializationPayloadBase
{
}

/// Managed-only support for preparing and hydrating generated unions nested in
/// ordinary serializable structs, classes, arrays, and lists.
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class UnitySerializationBridge
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldPlans =
        new ConcurrentDictionary<Type, FieldInfo[]>();

    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", InstanceFlags)!;

    [ThreadStatic]
    private static TransformState? ActiveTransformState;

    [ThreadStatic]
    private static EqualityState? ActiveEqualityState;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Prepare(object? value)
        => ExecuteTransform(value, restore: false, prior: null, preserveManagedRoot: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Prepare(object? value, object? prior)
        => ExecuteTransform(value, restore: false, prior, preserveManagedRoot: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? PrepareDynamic(object? value, object? prior)
        => ExecuteTransform(value, restore: false, prior, preserveManagedRoot: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? PrepareManagedReference(object? value, object? prior)
        => ExecuteTransform(value, restore: false, prior, preserveManagedRoot: true);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Prepare<T>(T value)
    {
        if (value is null)
            return value;

        var prepared = Prepare((object?)value);
        return prepared is null ? default! : (T)prepared;
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Prepare<T>(T value, T prior)
    {
        if (value is null)
            return value;

        var prepared = Prepare((object?)value, prior);
        return prepared is null ? default! : (T)prepared;
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? Restore(object? value)
        => ExecuteTransform(value, restore: true, prior: null, preserveManagedRoot: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? RestoreDynamic(object? value)
        => ExecuteTransform(value, restore: true, prior: null, preserveManagedRoot: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? RestoreManagedReference(object? value)
        => ExecuteTransform(value, restore: true, prior: null, preserveManagedRoot: true);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T Restore<T>(T value)
    {
        if (value is null)
            return value;

        var restored = Restore((object?)value);
        return restored is null ? default! : (T)restored;
    }

    /// Compares values according to the fields Unity serializes. This deliberately
    /// avoids user-defined Equals implementations for serializable composites.
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static bool __SpireSerializedValuesEqual(object? current, object? snapshot)
        => ExecuteEquality(current, snapshot, requireReferenceIdentity: false);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static bool __SpireSerializedManagedReferenceValuesEqual(object? current, object? snapshot)
        => ExecuteEquality(current, snapshot, requireReferenceIdentity: true);

    /// Creates an isolated inline snapshot for an Inspector edit while keeping
    /// Unity managed-reference fields shared by identity.
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static object? __SpireCloneSerializedSnapshot(object? value)
        => CloneSerializedSnapshot(value, new CloneState(), preserveManagedReference: false);

    private static bool ExecuteEquality(
        object? current,
        object? snapshot,
        bool requireReferenceIdentity)
    {
        var state = ActiveEqualityState;
        var ownsState = state is null;
        if (ownsState)
        {
            state = new EqualityState();
            ActiveEqualityState = state;
        }

        try
        {
            return AreSerializedValuesEqual(
                current,
                snapshot,
                state!,
                requireReferenceIdentity);
        }
        finally
        {
            if (ownsState)
                ActiveEqualityState = null;
        }
    }

    private static object? ExecuteTransform(
        object? value,
        bool restore,
        object? prior,
        bool preserveManagedRoot)
    {
        var state = ActiveTransformState;
        var ownsState = state is null;
        if (ownsState)
        {
            state = new TransformState();
            ActiveTransformState = state;
        }

        try
        {
            return Transform(value, restore, state!, prior, preserveManagedRoot);
        }
        finally
        {
            if (ownsState)
                ActiveTransformState = null;
        }
    }

    private static object? Transform(
        object? value,
        bool restore,
        TransformState state,
        object? prior,
        bool preserveManagedRoot = false)
    {
        if (value is null)
            return null;

        var type = value.GetType();
        if (IsSimple(type) || IsUnityNativeReference(type))
            return value;

        if (preserveManagedRoot && !type.IsValueType)
        {
            if (state.ManagedRoots.ContainsKey(value))
                return value;

            state.ManagedRoots[value] = value;
        }

        var generatedMethod = type.GetMethod(
            restore ? "__SpireHydrateSerialized" : "__SpirePrepareForSerialization",
            InstanceFlags,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        if (generatedMethod is not null &&
            type.GetField("_serializedPayload", InstanceFlags) is FieldInfo cacheField)
        {
            if (!restore && prior is not null && prior.GetType() == type)
            {
                var priorPayload = cacheField.GetValue(prior);
                if (priorPayload is not null)
                    cacheField.SetValue(value, priorPayload);
            }

            var result = generatedMethod.Invoke(value, null);
            return result ?? value;
        }

        if (value is IList list)
        {
            if (value is Array array && array.Rank != 1)
                return value;

            if (!HasGeneratedState(value))
                return value;

            return TransformList(
                value,
                list,
                restore,
                state,
                prior,
                preserveManagedRoot);
        }

        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false) ||
            !HasGeneratedState(value))
        {
            return value;
        }

        if (!preserveManagedRoot && state.Copies.TryGetValue(value, out var existingCopy))
            return existingCopy;

        if (!restore &&
            !preserveManagedRoot &&
            prior is not null &&
            prior.GetType() == type &&
            ExecuteEquality(value, prior, requireReferenceIdentity: false))
        {
            return prior;
        }

        var copy = preserveManagedRoot
            ? value
            : MemberwiseCloneMethod.Invoke(value, null)!;
        if (!preserveManagedRoot)
            state.Copies[value] = copy;

        foreach (var field in GetFieldPlan(type))
        {
            var child = field.GetValue(value);
            var priorChild = prior is null || prior.GetType() != type
                ? null
                : field.GetValue(prior);
            var transformed = Transform(
                child,
                restore,
                state,
                priorChild,
                preserveManagedRoot: preserveManagedRoot || IsManagedReferenceField(field));
            field.SetValue(copy, transformed);
        }

        return copy;
    }

    private static object TransformList(
        object value,
        IList list,
        bool restore,
        TransformState state,
        object? prior,
        bool preserveManagedRoot)
    {
        if (!preserveManagedRoot && state.Copies.TryGetValue(value, out var existingCopy))
            return existingCopy;

        if (!restore &&
            !preserveManagedRoot &&
            prior is not null &&
            prior.GetType() == value.GetType() &&
            ExecuteEquality(value, prior, requireReferenceIdentity: false))
        {
            return prior;
        }

        var copy = preserveManagedRoot ? value : CreateListCopy(value, list.Count);
        if (!preserveManagedRoot)
            state.Copies[value] = copy;

        var destination = (IList)copy;
        if (preserveManagedRoot && !copy.GetType().IsArray)
        {
            while (destination.Count > list.Count)
                destination.RemoveAt(destination.Count - 1);
        }

        for (var index = 0; index < list.Count; index++)
        {
            var child = list[index];
            var priorChild = GetListElement(prior, index);
            var transformed = Transform(child, restore, state, priorChild, preserveManagedRoot);
            if (copy is Array array)
                array.SetValue(transformed, index);
            else if (index < destination.Count)
                destination[index] = transformed;
            else
                destination.Add(transformed);
        }

        return copy;
    }

    private static object CreateListCopy(object value, int count)
    {
        var type = value.GetType();
        if (type.IsArray)
            return Array.CreateInstance(type.GetElementType()!, count);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return Activator.CreateInstance(type, count) ??
                throw new InvalidOperationException($"Could not create a copy of {type}.");
        }

        throw new InvalidOperationException($"Unsupported serialized collection type {type}.");
    }

    private static bool AreSerializedValuesEqual(
        object? current,
        object? snapshot,
        EqualityState state,
        bool requireReferenceIdentity = false)
    {
        if (current is null || snapshot is null)
            return current is null && snapshot is null;
        if (ReferenceEquals(current, snapshot))
            return true;
        if (requireReferenceIdentity && !ReferenceEquals(current, snapshot))
            return false;

        var currentType = current.GetType();
        if (HasGeneratedUnionMethods(currentType))
        {
            var serializedSnapshot = snapshot;
            if (snapshot.GetType() == currentType &&
                currentType.GetField("_serializedPayload", InstanceFlags) is FieldInfo snapshotCacheField)
            {
                serializedSnapshot = snapshotCacheField.GetValue(snapshot);
            }
            var method = currentType.GetMethod(
                "__SpireSerializedStateCurrent",
                InstanceFlags,
                binder: null,
                new[] { typeof(object) },
                modifiers: null);
            if (method is not null)
                return method.Invoke(current, new[] { serializedSnapshot }) is true;
        }

        if (IsUnityNativeReference(currentType) || IsUnityNativeReference(snapshot.GetType()))
            return ReferenceEquals(current, snapshot);
        if (IsSimple(currentType))
            return current.Equals(snapshot);
        if (currentType != snapshot.GetType())
            return false;

        if (current is IList currentList && snapshot is IList snapshotList)
        {
            if (currentList.Count != snapshotList.Count)
                return false;
            if (!state.TryVisit(current, snapshot))
                return true;
            for (var index = 0; index < currentList.Count; index++)
            {
                if (!AreSerializedValuesEqual(
                        currentList[index],
                        snapshotList[index],
                        state,
                        requireReferenceIdentity))
                    return false;
            }
            return true;
        }

        if (!currentType.IsDefined(typeof(SerializableAttribute), inherit: false))
            return currentType.IsValueType ? current.Equals(snapshot) : ReferenceEquals(current, snapshot);

        if (!state.TryVisit(current, snapshot))
            return true;
        foreach (var field in GetFieldPlan(currentType))
        {
            if (!AreSerializedValuesEqual(
                    field.GetValue(current),
                    field.GetValue(snapshot),
                    state,
                    IsManagedReferenceField(field)))
                return false;
        }
        return true;
    }

    private static object? CloneSerializedSnapshot(
        object? value,
        CloneState state,
        bool preserveManagedReference)
    {
        if (value is null || IsSimple(value.GetType()) || IsUnityNativeReference(value.GetType()))
            return value;
        if (preserveManagedReference && !value.GetType().IsValueType)
            return value;
        if (state.Copies.TryGetValue(value, out var existingCopy))
            return existingCopy;

        var type = value.GetType();
        if (HasGeneratedUnionMethods(type))
        {
            var unionCopy = MemberwiseCloneMethod.Invoke(value, null)!;
            state.Copies[value] = unionCopy;
            if (type.GetField("_serializedPayload", InstanceFlags) is FieldInfo cacheField)
            {
                var payload = cacheField.GetValue(value);
                cacheField.SetValue(
                    unionCopy,
                    CloneSerializedSnapshot(payload, state, preserveManagedReference: false));
            }
            return unionCopy;
        }

        if (value is IList list)
        {
            if (value is Array array && array.Rank != 1)
                return value;

            var listCopy = CreateListCopy(value, list.Count);
            state.Copies[value] = listCopy;
            var destination = (IList)listCopy;
            for (var index = 0; index < list.Count; index++)
            {
                var child = CloneSerializedSnapshot(list[index], state, preserveManagedReference: false);
                if (listCopy is Array destinationArray)
                    destinationArray.SetValue(child, index);
                else
                    destination.Add(child);
            }
            return listCopy;
        }

        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false))
            return value;

        var copy = MemberwiseCloneMethod.Invoke(value, null)!;
        state.Copies[value] = copy;
        foreach (var field in GetFieldPlan(type))
        {
            field.SetValue(
                copy,
                CloneSerializedSnapshot(
                    field.GetValue(value),
                    state,
                    IsManagedReferenceField(field)));
        }
        return copy;
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
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return ContainsGeneratedState(type.GetGenericArguments()[0], visiting);
        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false) || !visiting.Add(type))
            return false;
        try
        {
            return GetFieldPlan(type).Any(field => ContainsGeneratedState(field.FieldType, visiting));
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static bool HasGeneratedState(object value)
        => ContainsGeneratedState(value.GetType(), new HashSet<Type>()) ||
            ContainsGeneratedStateInManagedGraph(value, new HashSet<object>(ReferenceComparer.Instance));

    private static bool ContainsGeneratedStateInManagedGraph(
        object value,
        HashSet<object> visiting)
    {
        var type = value.GetType();
        if (HasGeneratedUnionMethods(type) || IsSimple(type) || IsUnityNativeReference(type))
            return HasGeneratedUnionMethods(type);
        if (!visiting.Add(value))
            return false;

        if (value is IList list)
        {
            if (value is Array array && array.Rank != 1)
                return false;
            for (var index = 0; index < list.Count; index++)
            {
                if (list[index] is object child &&
                    ContainsGeneratedStateInManagedGraph(child, visiting))
                    return true;
            }
            return false;
        }

        if (!type.IsDefined(typeof(SerializableAttribute), inherit: false))
            return false;
        foreach (var field in GetFieldPlan(type))
        {
            if (IsManagedReferenceField(field))
            {
                if (field.GetValue(value) is object child &&
                    ContainsGeneratedStateInManagedGraph(child, visiting))
                    return true;
            }
            else if (ContainsGeneratedState(field.FieldType, new HashSet<Type>()))
            {
                return true;
            }
        }
        return false;
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
            return false;
        if (field.IsPublic)
            return true;
        return field.GetCustomAttributes(inherit: false).Any(attribute =>
            attribute.GetType().FullName is "UnityEngine.SerializeField" or "UnityEngine.SerializeReference");
    }

    private static bool IsManagedReferenceField(FieldInfo field)
        => field.GetCustomAttributes(inherit: false).Any(attribute =>
            attribute.GetType().FullName == "UnityEngine.SerializeReference");

    private static bool IsSimple(Type type)
        => type == typeof(string) || type.IsPrimitive || type.IsEnum ||
            type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid);

    private static bool IsUnityNativeReference(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, "UnityEngine.Object", StringComparison.Ordinal))
                return true;
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
        internal readonly Dictionary<object, object> ManagedRoots =
            new Dictionary<object, object>(ReferenceComparer.Instance);
    }

    private sealed class CloneState
    {
        internal readonly Dictionary<object, object> Copies =
            new Dictionary<object, object>(ReferenceComparer.Instance);
    }

    private sealed class EqualityState
    {
        private readonly HashSet<ReferencePair> _visited = new HashSet<ReferencePair>();

        internal bool TryVisit(object left, object right)
            => _visited.Add(new ReferencePair(left, right));
    }

    private sealed class ReferencePair : IEquatable<ReferencePair>
    {
        private readonly object _left;
        private readonly object _right;
        private readonly int _hashCode;

        internal ReferencePair(object left, object right)
        {
            _left = left;
            _right = right;
            _hashCode = (RuntimeHelpers.GetHashCode(left) * 397) ^ RuntimeHelpers.GetHashCode(right);
        }

        public bool Equals(ReferencePair? other)
            => other is not null && ReferenceEquals(_left, other._left) && ReferenceEquals(_right, other._right);

        public override bool Equals(object? obj) => Equals(obj as ReferencePair);
        public override int GetHashCode() => _hashCode;
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();

        public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
