using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a method so the Spire source generator emits a sibling <c>readonly struct</c>
/// named <c>{MethodName}Inliner</c> implementing the matching <see cref="IActionInliner"/>
/// or <see cref="IFuncInliner{TR}"/> shape. The generated struct forwards <c>Invoke</c>
/// calls to the attributed method so consumers can substitute it in generic
/// struct-constrained APIs and get JIT monomorphization.
/// </summary>
/// <remarks>
/// The declaring type and every enclosing type must be declared <c>partial</c>.
/// Parameter modifiers (ref/in/out/ref readonly/params) are not supported in v1
/// and will produce SPIRE017. Declaring <c>ref struct</c> types are not
/// supported and produce SPIRE018. Total arity (plus instance for non-static
/// methods) cannot exceed 8 and will produce SPIRE019.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class InlinerStructAttribute : Attribute
{
}
