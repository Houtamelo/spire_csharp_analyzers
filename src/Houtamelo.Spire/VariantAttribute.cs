using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a static partial method as a variant declaration on a struct discriminated union.
/// The method name becomes the variant name; parameters become variant fields.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VariantAttribute : Attribute { }
