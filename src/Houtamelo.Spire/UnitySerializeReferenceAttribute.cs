using System;

namespace Houtamelo.Spire;

/// <summary>
/// Serializes a variant parameter as a Unity managed reference in generated
/// payload fields. Applies only to Unity-serializable unions; Unity determines
/// whether the field type supports managed-reference serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class UnitySerializeReferenceAttribute : Attribute
{
}
