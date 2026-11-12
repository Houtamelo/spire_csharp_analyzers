using System;
using JetBrains.Annotations;

namespace Houtamelo.Spire.Functional;

/// <summary>A value representing successful completion without a payload.</summary>
public struct Void { }

/// <summary>Represents successful completion or a failure containing an exception.</summary>
/// <remarks>Exceptions thrown by callbacks propagate to the caller.</remarks>
[DiscriminatedUnion]
public partial struct Result {
    /// <summary>Creates a successful result without a payload.</summary>
    /// <returns>A successful result.</returns>
    [Variant] public static partial Result Ok();

    /// <summary>Creates a failed result containing the specified exception.</summary>
    /// <param name="error">The exception to store.</param>
    /// <returns>A failed result containing <paramref name="error"/>.</returns>
    [Variant] public static partial Result Err(Exception error);

    /// <summary>Converts an exception to a failed result.</summary>
    /// <param name="exception">The exception to store.</param>
    /// <returns>A failed result containing <paramref name="exception"/>.</returns>
    public static implicit operator Result(Exception exception) => Err(exception);

    /// <summary>Returns a payload-free success value, or throws the stored exception on failure.</summary>
    /// <returns>The default <see cref="Void"/> value if the result is successful.</returns>
    /// <exception cref="Exception">The result is a failure; the stored exception is thrown.</exception>
    public readonly Void unwrap() => this switch {
        { IsOk: true } => default,
        { IsErr: true, err: var (error) } => ResultImpl.rethrow<Void>(error),
    };

    /// <summary>Returns the stored exception, or throws if the result is successful.</summary>
    /// <returns>The exception stored in the failed result.</returns>
    /// <exception cref="Exception">The result is successful.</exception>
    public readonly Exception unwrap_err() => this switch {
        { IsOk: true } => throw new("Error: Called `unwrap_err()` on a `Ok`."),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Returns a payload-free success value, or throws with the supplied message on failure.</summary>
    /// <param name="print_if_err">The message to include in the thrown exception if the result is a failure.</param>
    /// <returns>The default <see cref="Void"/> value if the result is successful.</returns>
    /// <exception cref="Exception">The result is a failure; the message includes the stored exception.</exception>
    public readonly Void expect(string print_if_err) => this switch {
        { IsOk: true } => default,
        { IsErr: true, err: var (error) } => throw new($"{print_if_err}.\n\tError: {error.Message}. \n", error),
    };

    /// <summary>Returns the stored exception, or throws with the supplied message on success.</summary>
    /// <param name="print_if_ok">The message to include in the thrown exception if the result is successful.</param>
    /// <returns>The exception stored in the failed result.</returns>
    /// <exception cref="Exception">The result is successful.</exception>
    public readonly Exception expect_err(string print_if_ok) => this switch {
        { IsOk: true } => throw new($"{print_if_ok}\n\tError: Called `expect_err` on a `Ok`."),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Computes a success value, preserving the stored exception on failure.</summary>
    /// <typeparam name="TMap">The type of the computed value.</typeparam>
    /// <param name="f">The function to invoke only if the result is successful.</param>
    /// <returns>A successful result containing the computed value, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result<TMap> map<TMap>(Func<TMap> f) => this switch {
        { IsOk: true } => f(),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Computes a value using the callback for the current result variant.</summary>
    /// <typeparam name="TMap">The type of the computed value.</typeparam>
    /// <param name="f_map">The function to invoke only on success.</param>
    /// <param name="f_else">The function to invoke with the stored exception only on failure.</param>
    /// <returns>The value returned by the selected callback.</returns>
    [MustUseReturnValue]
    public readonly TMap map_or_else<TMap>(
        Func<TMap> f_map,
        Func<Exception, TMap> f_else
    ) => this switch {
        { IsOk: true } => f_map(),
        { IsErr: true, err: var (error) } => f_else(error),
    };

    /// <summary>Transforms the stored exception, preserving success.</summary>
    /// <param name="f">The function to invoke with the stored exception only on failure.</param>
    /// <returns>A failed result containing the transformed exception, or a successful result.</returns>
    [MustUseReturnValue]
    public readonly Result map_err(Func<Exception, Exception> f) => this switch {
        { IsOk: true } => Ok(),
        { IsErr: true, err: var (error) } => f(error),
    };

    /// <summary>Computes another result on success, preserving the stored exception on failure.</summary>
    /// <typeparam name="TMap">The success type of the next result.</typeparam>
    /// <param name="f">The function to invoke only if the result is successful.</param>
    /// <returns>The result returned by <paramref name="f"/>, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result<TMap> and_then<TMap>(Func<Result<TMap>> f) => this switch {
        { IsOk: true } => f(),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Computes another result on success, preserving the stored exception on failure.</summary>
    /// <param name="f">The function to invoke only if the result is successful.</param>
    /// <returns>The result returned by <paramref name="f"/>, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result and_then(Func<Result> f) => this switch {
        { IsOk: true } => f(),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Invokes an action with the stored exception only if the result is a failure.</summary>
    /// <param name="f">The action to invoke on failure.</param>
    /// <returns>The default <see cref="Void"/> value.</returns>
    public readonly Void inspect_err(Action<Exception> f) {
        if (this is { IsErr: true, err: var (error) }) {
            f(error);
        }

        return default;
    }
}

/// <summary>Represents a successful value or a failure containing an exception.</summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <remarks>Exceptions thrown by callbacks propagate to the caller.</remarks>
[DiscriminatedUnion]
public partial struct Result<T> {
    /// <summary>Creates a successful result containing the specified value.</summary>
    /// <param name="value">The success value to store.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    [Variant] public static partial Result<T> Ok(T value);

    /// <summary>Creates a failed result containing the specified exception.</summary>
    /// <param name="error">The exception to store.</param>
    /// <returns>A failed result containing <paramref name="error"/>.</returns>
    [Variant] public static partial Result<T> Err(Exception error);

    /// <summary>Converts an exception to a failed result.</summary>
    /// <param name="exception">The exception to store.</param>
    /// <returns>A failed result containing <paramref name="exception"/>.</returns>
    public static implicit operator Result<T>(Exception exception) => Err(exception);

    /// <summary>Converts a value to a successful result.</summary>
    /// <param name="value">The success value to store.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary>Discards the success value while preserving the result's success or stored exception.</summary>
    /// <param name="typed_result">The result whose success value is discarded.</param>
    /// <returns>A payload-free success, or a failure containing the original exception.</returns>
    public static implicit operator Result(Result<T> typed_result) => typed_result switch {
        { IsOk: true } => Result.Ok(),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Discards the success value while preserving the result's success or stored exception.</summary>
    /// <returns>A payload-free success, or a failure containing the original exception.</returns>
    public readonly Result as_untyped() => this switch {
        { IsOk: true } => Result.Ok(),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Returns the success value, or throws the stored exception on failure.</summary>
    /// <returns>The value stored in the successful result.</returns>
    /// <exception cref="Exception">The result is a failure; the stored exception is thrown.</exception>
    public readonly T unwrap() => this switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true, err: var (error) } => ResultImpl.rethrow<T>(error),
    };

    /// <summary>Returns the stored exception, or throws if the result is successful.</summary>
    /// <returns>The exception stored in the failed result.</returns>
    /// <exception cref="Exception">The result is successful.</exception>
    public readonly Exception unwrap_err() => this switch {
        { IsOk: true } => throw new("Error: Called `unwrap_err` on a `Ok`."),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Returns the success value, or the supplied fallback on failure.</summary>
    /// <param name="default_value">The value to return on failure.</param>
    /// <returns>The stored success value, or <paramref name="default_value"/>.</returns>
    public readonly T unwrap_or(T default_value) => this switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true } => default_value,
    };

    /// <summary>Returns the success value, or computes a fallback on failure.</summary>
    /// <param name="f">The function to invoke only if the result is a failure.</param>
    /// <returns>The stored success value, or the value returned by <paramref name="f"/>.</returns>
    public readonly T unwrap_or_else(Func<T> f) => this switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true } => f(),
    };

    /// <summary>Returns the success value, or throws with the supplied message on failure.</summary>
    /// <param name="print_if_err">The message to include in the thrown exception if the result is a failure.</param>
    /// <returns>The value stored in the successful result.</returns>
    /// <exception cref="Exception">The result is a failure; the message includes the stored exception.</exception>
    public readonly T expect(string print_if_err) => this switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true, err: var (error) } => throw new($"{print_if_err}.\n\tError: {error.Message}. \n", error),
    };

    /// <summary>Returns the stored exception, or throws with the supplied message on success.</summary>
    /// <param name="print_if_ok">The message to include in the thrown exception if the result is successful.</param>
    /// <returns>The exception stored in the failed result.</returns>
    /// <exception cref="Exception">The result is successful.</exception>
    public readonly Exception expect_err(string print_if_ok) => this switch {
        { IsOk: true } => throw new($"{print_if_ok}\n\tError: Called `expect_err` on a `Ok`."),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Transforms the success value, preserving the stored exception on failure.</summary>
    /// <typeparam name="TMap">The type of the transformed value.</typeparam>
    /// <param name="f">The function to invoke with the stored value only on success.</param>
    /// <returns>A successful result containing the transformed value, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result<TMap> map<TMap>(Func<T, TMap> f) => this switch {
        { IsOk: true, ok: var (value) } => f(value),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Transforms the success value, or returns a supplied fallback on failure.</summary>
    /// <typeparam name="TMap">The type of the returned value.</typeparam>
    /// <param name="f">The function to invoke with the stored value only on success.</param>
    /// <param name="default_value">The value to return on failure.</param>
    /// <returns>The transformed success value, or <paramref name="default_value"/>.</returns>
    [MustUseReturnValue]
    public readonly TMap map_or<TMap>(
        Func<T, TMap> f,
        TMap default_value
    ) => this switch {
        { IsOk: true, ok: var (value) } => f(value),
        { IsErr: true } => default_value,
    };

    /// <summary>Computes a value using the callback for the current result variant.</summary>
    /// <typeparam name="TMap">The type of the computed value.</typeparam>
    /// <param name="f_map">The function to invoke with the stored value only on success.</param>
    /// <param name="f_else">The function to invoke with the stored exception only on failure.</param>
    /// <returns>The value returned by the selected callback.</returns>
    [MustUseReturnValue]
    public readonly TMap map_or_else<TMap>(
        Func<T, TMap> f_map,
        Func<Exception, TMap> f_else
    ) => this switch {
        { IsOk: true, ok: var (value) } => f_map(value),
        { IsErr: true, err: var (error) } => f_else(error),
    };

    /// <summary>Transforms the stored exception, preserving the value on success.</summary>
    /// <param name="f">The function to invoke with the stored exception only on failure.</param>
    /// <returns>A failed result containing the transformed exception, or the original successful value.</returns>
    [MustUseReturnValue]
    public readonly Result<T> map_err(Func<Exception, Exception> f) => this switch {
        { IsOk: true, ok: var (value) } => value,
        { IsErr: true, err: var (error) } => f(error),
    };

    /// <summary>Computes another result from the success value, preserving the stored exception on failure.</summary>
    /// <typeparam name="TMap">The success type of the next result.</typeparam>
    /// <param name="f">The function to invoke with the stored value only on success.</param>
    /// <returns>The result returned by <paramref name="f"/>, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result<TMap> and_then<TMap>(Func<T, Result<TMap>> f) => this switch {
        { IsOk: true, ok: var (value) } => f(value),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Computes a payload-free result from the success value, preserving the stored exception on failure.</summary>
    /// <param name="f">The function to invoke with the stored value only on success.</param>
    /// <returns>The result returned by <paramref name="f"/>, or the original failure.</returns>
    [MustUseReturnValue]
    public readonly Result and_then(Func<T, Result> f) => this switch {
        { IsOk: true, ok: var (value) } => f(value),
        { IsErr: true, err: var (error) } => error,
    };

    /// <summary>Invokes an action with the stored value only if the result is successful.</summary>
    /// <param name="f">The action to invoke on success.</param>
    /// <returns>The default <see cref="Void"/> value.</returns>
    public readonly Void inspect(Action<T> f) {
        if (this is { IsOk: true, ok: var (value) }) {
            f(value);
        }

        return default;
    }

    /// <summary>Invokes an action with the stored exception only if the result is a failure.</summary>
    /// <param name="f">The action to invoke on failure.</param>
    /// <returns>The default <see cref="Void"/> value.</returns>
    public readonly Void inspect_err(Action<Exception> f) {
        if (this is { IsErr: true, err: var (error) }) {
            f(error);
        }

        return default;
    }
}
