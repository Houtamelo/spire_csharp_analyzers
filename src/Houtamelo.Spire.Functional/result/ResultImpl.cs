using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace Houtamelo.Spire.Functional;

/// <summary>
/// Provides common operations used with <see cref="Result"/> and <see cref="Result{T}"/>.
///
/// Tip: Adding the directive `using static Houtamelo.Spire.Functional.ResultImpl;`
/// allows you to create the `Ok` variant without specifying the entire result type.
/// <br/>
/// Example:
/// <br/>
/// Without the `using static ...` directive:
/// <br/>
/// <code>return Result&lt;List&lt;SomeType&gt;&gt;.Ok(expression);</code>
/// <br/>
/// With the `using static ...` directive:
/// <br/>
/// <code>return Ok(expression);</code>
/// </summary>
public static class ResultImpl {
    /// <summary>Creates a successful result without a payload.</summary>
    /// <returns>A successful result.</returns>
    public static Result Ok() => Result.Ok();

    /// <summary>Creates a successful result containing the specified value.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="value">The success value to store.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static Result<T> Ok<T>(T value) => value;

    /// <summary>Returns the success value as a nullable value, or <see langword="null"/> on failure.</summary>
    /// <typeparam name="T">The value type stored on success.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The stored success value, or <see langword="null"/> if the result is a failure.</returns>
    public static T? try_value<T>(this Result<T> result) where T: struct => result switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true } => null,
    };

    /// <summary>Lazily enumerates stored exceptions, skipping successful results.</summary>
    /// <param name="results">The results to enumerate.</param>
    /// <returns>The exceptions from failed results, in source order.</returns>
    [MustUseReturnValue]
    public static IEnumerable<Exception> iter_errs(this IEnumerable<Result> results) {
        foreach (var element in results) {
            switch (element) {
                case { IsOk: true }: break;
                case { IsErr: true, err: var (error) }: {
                    yield return error;

                    break;
                }
            }
        }
    }

    /// <summary>Lazily enumerates stored exceptions, skipping successful results.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <returns>The exceptions from failed results, in source order.</returns>
    [MustUseReturnValue]
    public static IEnumerable<Exception> iter_errs<T>(this IEnumerable<Result<T>> results) {
        foreach (var element in results) {
            switch (element) {
                case { IsOk: true }: break;
                case { IsErr: true, err: var (error) }: {
                    yield return error;

                    break;
                }
            }
        }
    }

    /// <summary>Lazily enumerates successful values, skipping failed results.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <returns>The values from successful results, in source order.</returns>
    [MustUseReturnValue]
    public static IEnumerable<T> iter_oks<T>(this IEnumerable<Result<T>> results) {
        foreach (var element in results) {
            switch (element) {
                case { IsOk: true, ok: var (value) }: {
                    yield return value;

                    break;
                }
                case { IsErr: true }: break;
            }
        }
    }

    /// <summary>Collects successful values into a list, stopping at the first failure.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <returns>
    /// A successful result containing all values in source order, or the first stored exception.
    /// An empty source produces a successful empty list.
    /// </returns>
    [MustUseReturnValue]
    public static Result<List<T>> try_collect<T>(this IEnumerable<Result<T>> results) {
        var list = new List<T>();

        foreach (var result in results) {
            switch (result) {
                case { IsOk: true, ok: var (value) }: {
                    list.Add(value);
                    break;
                }
                case { IsErr: true, err: var (error) }: {
                    return error;
                }
            }
        }

        return list;
    }

    /// <summary>Accumulates successful values in source order, stopping at the first failure.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <typeparam name="TSum">The type of the accumulator.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <param name="initial_value">The initial accumulator value.</param>
    /// <param name="f">The function that combines the accumulator with each successful value.</param>
    /// <returns>
    /// A successful result containing the final accumulator, or the first stored exception.
    /// An empty source produces a successful result containing <paramref name="initial_value"/>.
    /// </returns>
    [MustUseReturnValue]
    public static Result<TSum> try_fold<T, TSum>(
        this IEnumerable<Result<T>> results,
        TSum initial_value,
        Func<TSum, T, TSum> f
    ) {
        var sum = initial_value;

        foreach (var result in results) {
            switch (result) {
                case { IsOk: true, ok: var (value) }: {
                    sum = f(sum, value);
                    break;
                }
                case { IsErr: true, err: var (error) }: {
                    return error;
                }
            }
        }

        return sum;
    }

    /// <summary>Maps successful values into a list, stopping at the first failure.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <typeparam name="TMap">The type of the mapped values.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <param name="f">The function to apply to each successful value before any failure.</param>
    /// <returns>
    /// A successful result containing mapped values in source order, or the first stored exception.
    /// An empty source produces a successful empty list.
    /// </returns>
    [MustUseReturnValue]
    public static Result<List<TMap>> try_map<T, TMap>(
        this IEnumerable<Result<T>> results,
        Func<T, TMap> f
    ) {
        var list = new List<TMap>();

        foreach (var result in results) {
            switch (result) {
                case { IsOk: true, ok: var (value) }: {
                    list.Add(f(value));
                    break;
                }
                case { IsErr: true, err: var (error) }: {
                    return error;
                }
            }
        }

        return list;
    }

    /// <summary>Applies a result-returning function in source order, stopping at the first failure.</summary>
    /// <typeparam name="T">The type of the source elements.</typeparam>
    /// <param name="elements">The elements to enumerate.</param>
    /// <param name="f">The function to apply to each element until it returns a failure.</param>
    /// <returns>The first failure, or a successful result if every call succeeds or the source is empty.</returns>
    [MustUseReturnValue]
    public static Result try_for_each<T>(this IEnumerable<T> elements, Func<T, Result> f) {
        foreach (var element in elements) {
            if (f(element) is { IsErr: true, err: var (error) }) return error;
        }

        return Result.Ok();
    }

    /// <summary>Partitions all results into successful values and exceptions without stopping on failure.</summary>
    /// <typeparam name="T">The success type of the source results.</typeparam>
    /// <param name="results">The results to enumerate.</param>
    /// <returns>Lists of successful values and stored exceptions, each preserving source order.</returns>
    [MustUseReturnValue]
    public static (List<T> oks, List<Exception> errs) unzip<T>(this IEnumerable<Result<T>> results) {
        var oks = new List<T>();
        var errs = new List<Exception>();

        foreach (var result in results) {
            switch (result) {
                case { IsOk: true, ok: var (value) }: {
                    oks.Add(value);
                    break;
                }
                case { IsErr: true, err: var (error) }: {
                    errs.Add(error);
                    break;
                }
            }
        }

        return (oks, errs);
    }

    /// <summary>Removes one result layer, preserving an outer failure.</summary>
    /// <typeparam name="T">The success type of the inner result.</typeparam>
    /// <param name="self">The nested result to flatten.</param>
    /// <returns>The inner result on outer success, or a failure containing the outer exception.</returns>
    [MustUseReturnValue]
    public static Result<T> flatten<T>(this Result<Result<T>> self) => self switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Removes one result layer, preserving an outer failure.</summary>
    /// <param name="self">The nested result to flatten.</param>
    /// <returns>The inner result on outer success, or a failure containing the outer exception.</returns>
    [MustUseReturnValue]
    public static Result flatten(this Result<Result> self) => self switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Lazily removes one result layer from each element, preserving outer failures.</summary>
    /// <param name="self">The nested results to enumerate.</param>
    /// <returns>One flattened result per source element, in source order.</returns>
    [MustUseReturnValue]
    public static IEnumerable<Result> flatten(this IEnumerable<Result<Result>> self) {
        foreach (var result in self) {
            yield return result switch {
                { IsOk: true, ok: var (value) } => value,
                { IsErr: true, err: var (error) } => error,
            };
        }
    }

    /// <summary>Lazily removes one result layer from each element, preserving outer failures.</summary>
    /// <typeparam name="T">The success type of each inner result.</typeparam>
    /// <param name="self">The nested results to enumerate.</param>
    /// <returns>One flattened result per source element, in source order.</returns>
    [MustUseReturnValue]
    public static IEnumerable<Result<T>> flatten<T>(this IEnumerable<Result<Result<T>>> self) {
        foreach (var result in self) {
            yield return result switch {
                { IsOk: true, ok: var (value) } => value,
                { IsErr: true, err: var (error) } => error,
            };
        }
    }

    [DoesNotReturn]
    internal static TValue rethrow<TValue>(Exception error) {
        ExceptionDispatchInfo.Capture(error)!.Throw();
        throw null!; // Unreachable; required by the compiler.
    }
}
