namespace Houtamelo.Spire;

/// <summary>Zero-parameter func inliner.</summary>
public interface IFuncInliner<TR>
{
    TR Invoke();
}

/// <summary>One-parameter func inliner.</summary>
public interface IFuncInliner<T1, TR>
{
    TR Invoke(T1 a1);
}

/// <summary>Two-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, TR>
{
    TR Invoke(T1 a1, T2 a2);
}

/// <summary>Three-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3);
}

/// <summary>Four-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4);
}

/// <summary>Five-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5);
}

/// <summary>Six-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6);
}

/// <summary>Seven-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, T7, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7);
}

/// <summary>Eight-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, T7, T8, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8);
}
