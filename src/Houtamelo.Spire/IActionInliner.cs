namespace Houtamelo.Spire;

/// <summary>
/// Stateless forwarder struct interface for zero-parameter void methods.
/// Used by [InlinerStruct]-generated types to enable JIT monomorphization
/// in place of Action delegate indirection.
/// </summary>
public interface IActionInliner
{
    void Invoke();
}

/// <summary>One-parameter void inliner.</summary>
public interface IActionInliner<T1>
{
    void Invoke(T1 a1);
}

/// <summary>Two-parameter void inliner.</summary>
public interface IActionInliner<T1, T2>
{
    void Invoke(T1 a1, T2 a2);
}

/// <summary>Three-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3>
{
    void Invoke(T1 a1, T2 a2, T3 a3);
}

/// <summary>Four-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4);
}

/// <summary>Five-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5);
}

/// <summary>Six-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6);
}

/// <summary>Seven-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6, T7>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7);
}

/// <summary>Eight-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6, T7, T8>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8);
}
