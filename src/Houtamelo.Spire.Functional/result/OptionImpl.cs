using System;
using JetBrains.Annotations;

// ReSharper disable ConvertNullableToShortForm

namespace Houtamelo.Spire.Functional;

/// <summary>Provides factories and operations for optional values represented by nullable value types.</summary>
/// <remarks>Exceptions thrown by callbacks propagate to the caller.</remarks>
public static class OptionImpl {
    /// <summary>Creates an option containing the specified value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="value">The value to store, including its default value.</param>
    /// <returns>A nullable value whose <c>HasValue</c> is <see langword="true"/>.</returns>
    // ReSharper disable once InconsistentNaming
    [Pure]
    public static Nullable<T> Some<T>(T value) where T: struct => value;

    /// <summary>Creates an option without a value.</summary>
    /// <typeparam name="T">The type of value the option could contain.</typeparam>
    /// <returns>A nullable value whose <c>HasValue</c> is <see langword="false"/>.</returns>
    // ReSharper disable once InconsistentNaming
    public static Nullable<T> None<T>() where T: struct => null;

    /// <summary>Returns the stored value, or throws if the option has no value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to unwrap.</param>
    /// <returns>The value stored in the option.</returns>
    /// <exception cref="Exception"><paramref name="option"/> has no value.</exception>
    [Pure]
    public static T unwrap<T>(this Nullable<T> option) where T: struct => option switch {
        T some => some,
        null => throw new("Error: Called `unwrap` on a null value."),
    };

    /// <summary>Returns the stored value, or throws with the supplied message if the option has no value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to unwrap.</param>
    /// <param name="print_if_null">The message to include in the exception if the option has no value.</param>
    /// <returns>The value stored in the option.</returns>
    /// <exception cref="Exception"><paramref name="option"/> has no value.</exception>
    [Pure]
    public static T expect<T>(this Nullable<T> option, string print_if_null) where T: struct =>
        option switch {
            T some => some,
            null => throw new($"{print_if_null}\n\tError: Called `expect` on a null value."),
        };

    /// <summary>Transforms the stored value, or returns a supplied fallback if the option has no value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the returned value.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="f">The function to invoke with the stored value only when it is present.</param>
    /// <param name="default_value">The fallback value, evaluated eagerly.</param>
    /// <returns>The transformed value, or <paramref name="default_value"/> if the option has no value.</returns>
    [MustUseReturnValue]
    public static TMap map_or<T, TMap>(this Nullable<T> option, Func<T, TMap> f, TMap default_value)
        where T: struct
        => option switch {
            T some => f(some),
            null => default_value,
        };

    /// <summary>Transforms the stored value, or computes a fallback if the option has no value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the returned value.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="f_map">The function to invoke with the stored value only when it is present.</param>
    /// <param name="f_else">The function to invoke only when the option has no value.</param>
    /// <returns>The value returned by the selected callback.</returns>
    [MustUseReturnValue]
    public static TMap map_or_else<T, TMap>(this Nullable<T> option, Func<T, TMap> f_map, Func<TMap> f_else)
        where T: struct
        => option switch {
            T some => f_map(some),
            null => f_else(),
        };

    /// <summary>Invokes an action with the stored value only when it is present.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to inspect.</param>
    /// <param name="f">The action to invoke when a value is present.</param>
    /// <returns>The default <see cref="Void"/> value.</returns>
    public static Void inspect<T>(this Nullable<T> option, Action<T> f) where T: struct {
        if (option is T some) f(some);

        return default;
    }

    /// <summary>Computes an optional value only when the condition is true.</summary>
    /// <typeparam name="T">The type of the computed value.</typeparam>
    /// <param name="condition">Whether to compute and return a value.</param>
    /// <param name="f">The function to invoke only when <paramref name="condition"/> is true.</param>
    /// <returns>The computed value wrapped in an option, or <see langword="null"/> if the condition is false.</returns>
    [MustUseReturnValue]
    public static Nullable<T> then<T>(this bool condition, Func<T> f)
        where T: struct {
        if (condition) return f();
        else return null;
    }

    /// <summary>Returns the supplied value as an option only when the condition is true.</summary>
    /// <typeparam name="T">The type of the supplied value.</typeparam>
    /// <param name="condition">Whether to return an option containing the value.</param>
    /// <param name="value">The value to wrap, evaluated eagerly.</param>
    /// <returns>An option containing <paramref name="value"/>, or <see langword="null"/> if the condition is false.</returns>
    [MustUseReturnValue]
    public static Nullable<T> then_some<T>(this bool condition, T value)
        where T: struct {
        if (condition) return value;
        else return null;
    }

    /// <summary>Keeps the stored value only if it satisfies the predicate.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to filter.</param>
    /// <param name="f">The predicate to invoke only when a value is present.</param>
    /// <returns>The original option if its value satisfies the predicate; otherwise, <see langword="null"/>.</returns>
    [MustUseReturnValue]
    public static Nullable<T> filter<T>(this Nullable<T> option, Predicate<T> f)
        where T: struct {
        if (option is T some && f(some)) return option;
        else return null;
    }

    /// <summary>Returns this option if it has a value, or a supplied fallback otherwise.</summary>
    /// <typeparam name="T">The type of the optional values.</typeparam>
    /// <param name="option">The preferred option.</param>
    /// <param name="other">The fallback option, evaluated eagerly.</param>
    /// <returns><paramref name="option"/> if it has a value; otherwise, <paramref name="other"/>.</returns>
    [MustUseReturnValue]
    public static Nullable<T> or<T>(this Nullable<T> option, Nullable<T> other) where T: struct
        => option switch {
            T some => some,
            null => other,
        };

    /// <summary>Returns this option if it has a value, or computes a fallback otherwise.</summary>
    /// <typeparam name="T">The type of the optional values.</typeparam>
    /// <param name="option">The preferred option.</param>
    /// <param name="f">The function to invoke only when the option has no value.</param>
    /// <returns><paramref name="option"/> if it has a value; otherwise, the option returned by <paramref name="f"/>.</returns>
    [MustUseReturnValue]
    public static Nullable<T> or_else<T>(this Nullable<T> option, Func<Nullable<T>> f) where T: struct
        => option switch {
            T some => some,
            null => f(),
        };

    /// <summary>Converts a present value to a successful result, or an absent value to a supplied failure.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to convert.</param>
    /// <param name="err">The exception to store on failure, evaluated eagerly.</param>
    /// <returns>A successful result containing the stored value, or a failed result containing <paramref name="err"/>.</returns>
    [MustUseReturnValue]
    public static Result<T> ok_or<T>(this Nullable<T> option, Exception err) where T: struct
        => option switch {
            T some => Result<T>.Ok(some),
            null => err,
        };

    /// <summary>Converts a present value to a successful result, or computes an exception for an absent value.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="option">The option to convert.</param>
    /// <param name="f">The exception factory to invoke only when the option has no value.</param>
    /// <returns>A successful result containing the stored value, or a failed result containing the computed exception.</returns>
    [MustUseReturnValue]
    public static Result<T> ok_or_else<T>(this Nullable<T> option, Func<Exception> f) where T: struct
        => option switch {
            T some => Result<T>.Ok(some),
            null => f(),
        };
}

/// <summary>Provides option operations whose outputs are nullable value types.</summary>
/// <remarks>Exceptions thrown by callbacks propagate to the caller.</remarks>
public static class OptionImplWithValueOutput {
    /// <summary>Transforms the stored value into another value type, preserving absence.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the transformed value.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="f">The function to invoke with the stored value only when it is present.</param>
    /// <returns>An option containing the transformed value, or <see langword="null"/> if the source has no value.</returns>
    [MustUseReturnValue]
    public static Nullable<TMap> map<T, TMap>(this Nullable<T> option, Func<T, TMap> f)
        where T: struct
        where TMap: struct
        => option switch {
            T some => f(some),
            null => null,
        };

    /// <summary>Returns another option only when this option has a value.</summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TMap">The type of the other option's value.</typeparam>
    /// <param name="option">The option whose presence is required.</param>
    /// <param name="other">The option to return when the source has a value, evaluated eagerly.</param>
    /// <returns><paramref name="other"/> if the source has a value; otherwise, <see langword="null"/>.</returns>
    [MustUseReturnValue]
    public static Nullable<TMap> and<T, TMap>(this Nullable<T> option, Nullable<TMap> other)
        where T: struct
        where TMap: struct
        => option switch {
            T => other,
            null => null,
        };

    /// <summary>Computes another option from the stored value, preserving absence.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the next option's value.</typeparam>
    /// <param name="option">The option to use as input.</param>
    /// <param name="f">The option-returning function to invoke with the stored value only when it is present.</param>
    /// <returns>The option returned by <paramref name="f"/>, or <see langword="null"/> if the source has no value.</returns>
    [MustUseReturnValue]
    public static Nullable<TMap> and_then<T, TMap>(this Nullable<T> option, Func<T, Nullable<TMap>> f)
        where T: struct
        where TMap: struct
        => option switch {
            T some => f(some),
            null => null,
        };
}

/// <summary>Provides option operations whose outputs are nullable reference types.</summary>
/// <remarks>Exceptions thrown by callbacks propagate to the caller.</remarks>
public static class OptionImplWithRefOutput {
    /// <summary>Transforms the stored value into a reference type, preserving absence as null.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the transformed reference.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="f">The function to invoke with the stored value only when it is present.</param>
    /// <returns>The transformed reference, or <see langword="null"/> if the source has no value.</returns>
    [MustUseReturnValue]
    public static TMap? map<T, TMap>(this Nullable<T> option, Func<T, TMap> f)
        where T: struct
        where TMap: class
        => option switch {
            T some => f(some),
            null => null,
        };

    /// <summary>Returns a supplied reference only when this option has a value.</summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TMap">The type of the supplied reference.</typeparam>
    /// <param name="option">The option whose presence is required.</param>
    /// <param name="other">The nullable reference to return when the source has a value, evaluated eagerly.</param>
    /// <returns><paramref name="other"/> if the source has a value; otherwise, <see langword="null"/>.</returns>
    [MustUseReturnValue]
    public static TMap? and<T, TMap>(this Nullable<T> option, TMap? other)
        where T: struct
        where TMap: class
        => option switch {
            T => other,
            null => null,
        };

    /// <summary>Computes a nullable reference from the stored value, preserving absence as null.</summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <typeparam name="TMap">The type of the computed reference.</typeparam>
    /// <param name="option">The option to use as input.</param>
    /// <param name="f">The function to invoke with the stored value only when it is present; it may return null.</param>
    /// <returns>The reference returned by <paramref name="f"/>, or <see langword="null"/> if the source has no value.</returns>
    [MustUseReturnValue]
    public static TMap? and_then<T, TMap>(this Nullable<T> option, Func<T, TMap?> f)
        where T: struct
        where TMap: class
        => option switch {
            T some => f(some),
            null => null,
        };
}
