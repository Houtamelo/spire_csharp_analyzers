using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a delegate-typed parameter so the Spire source generator emits a twin overload
/// of the containing method where the parameter is replaced with a generic struct
/// constrained to the matching <see cref="IActionInliner"/> / <see cref="IFuncInliner{TR}"/>
/// shape. Direct invocations of the parameter in the method body are rewritten to
/// <c>.Invoke(...)</c> on the generic struct, enabling JIT monomorphization.
/// </summary>
/// <remarks>
/// The containing type (and every enclosing type) must be <c>partial</c>. Delegate
/// arity must be ≤ 8. Nullability of the delegate parameter is preserved as
/// <see cref="Nullable{T}"/> on the twin. The parameter may only appear as the
/// target of an invocation or inside a single-assignment <c>var</c> alias; other
/// uses produce SPIRE021.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class InlinableAttribute : Attribute
{
}
