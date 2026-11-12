using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Houtamelo.Spire.Functional;
using Xunit;

namespace Houtamelo.Spire.Functional.Tests;

public sealed class ResultBehaviorTests
{
    [Fact]
    public void ConstructorsAndBasicOperationsPreserveVariants()
    {
        var error = new InvalidOperationException("failure");
        var unit = Result.Ok();
        var failed = Result.Err(error);
        var value = Result<int>.Ok(7);
        var failedValue = Result<int>.Err(error);

        Assert.True(unit.IsOk);
        Assert.False(unit.IsErr);
        Assert.True(failed.IsErr);
        Assert.Same(error, failed.unwrap_err());
        Assert.Equal(7, value.unwrap());
        Assert.Same(error, failedValue.unwrap_err());
        Assert.Equal(7, value.unwrap_or(0));
        Assert.Equal(0, failedValue.unwrap_or(0));
        Assert.True(value.as_untyped().IsOk);
        Assert.True(failedValue.as_untyped().IsErr);
        Assert.True(ResultImpl.Ok().IsOk);
        Assert.Equal(9, ResultImpl.Ok(9).unwrap());

        var exceptionFallbackCalls = 0;
        Assert.Equal(7, value.unwrap_or_else(() =>
        {
            exceptionFallbackCalls++;
            return 0;
        }));
        Assert.Equal(0, failedValue.unwrap_or_else(() =>
        {
            exceptionFallbackCalls++;
            return 0;
        }));
        Assert.Equal(1, exceptionFallbackCalls);
    }

    [Fact]
    public void MappingAndChainingSelectOnlyTheActiveBranch()
    {
        var error = new Exception("failure");
        var successful = Result<int>.Ok(3);
        var failed = Result<int>.Err(error);
        var mapCalls = 0;
        var errorCalls = 0;

        Assert.Equal(6, successful.map(value =>
        {
            mapCalls++;
            return value * 2;
        }).unwrap());
        Assert.Same(error, failed.map(value =>
        {
            mapCalls++;
            return value * 2;
        }).unwrap_err());
        Assert.Equal(6, successful.map_or(value => value * 2, 0));
        Assert.Equal(0, failed.map_or(value => value * 2, 0));
        Assert.Equal(6, successful.map_or_else(value => value * 2, _ => 0));
        Assert.Equal(0, failed.map_or_else(value => value * 2, exception =>
        {
            errorCalls++;
            Assert.Same(error, exception);
            return 0;
        }));
        Assert.Equal(1, mapCalls);
        Assert.Equal(1, errorCalls);

        var mappedError = new ArgumentException("mapped");
        Assert.Same(mappedError, failed.map_err(_ => mappedError).unwrap_err());
        Assert.Equal(3, successful.map_err(_ => mappedError).unwrap());

        Assert.Equal(8, successful.and_then(value => Result<int>.Ok(value + 5)).unwrap());
        Assert.Same(error, failed.and_then(value => Result<int>.Ok(value + 5)).unwrap_err());
        Assert.True(successful.and_then(_ => Result.Ok()).IsOk);
        Assert.Same(error, failed.and_then(_ => Result.Err(error)).unwrap_err());

        Assert.Equal(11, Result.Ok().map(() => 11).unwrap());
        Assert.Same(error, Result.Err(error).map(() => 11).unwrap_err());
        Assert.Equal("ok", Result.Ok().map_or_else(() => "ok", _ => "error"));
        Assert.Equal("failure", Result.Err(error).map_or_else(() => "ok", exception => exception.Message));
        Assert.Same(mappedError, Result.Err(error).map_err(_ => mappedError).unwrap_err());
        Assert.True(Result.Ok().map_err(_ => mappedError).IsOk);

        Assert.True(Result.Ok().and_then(() => Result.Ok()).IsOk);
        Assert.Same(error, failed.as_untyped().and_then(() => Result.Ok()).unwrap_err());
    }

    [Fact]
    public void InspectAndExpectPreserveUsefulExceptionContext()
    {
        var error = CaptureOriginException();
        var inspectedValue = 0;
        var inspectedError = 0;
        var successful = Result<int>.Ok(5);
        var failed = Result<int>.Err(error);

        successful.inspect(value => inspectedValue = value);
        successful.inspect_err(_ => inspectedError++);
        failed.inspect(_ => inspectedValue++);
        failed.inspect_err(exception =>
        {
            Assert.Same(error, exception);
            inspectedError++;
        });
        Assert.Equal(5, inspectedValue);
        Assert.Equal(1, inspectedError);

        var unwrapped = Assert.ThrowsAny<Exception>(() => failed.unwrap());
        Assert.Same(error, unwrapped);
        Assert.Contains(nameof(CaptureOriginException), unwrapped.StackTrace);

        Result.Ok().unwrap();
        var untypedFailed = Result.Err(error);
        var untypedUnwrapped = Assert.ThrowsAny<Exception>(() => untypedFailed.unwrap());
        Assert.Same(error, untypedUnwrapped);
        Assert.Contains(nameof(CaptureOriginException), untypedUnwrapped.StackTrace);

        var expected = Assert.Throws<Exception>(() => failed.expect("while loading"));
        Assert.Same(error, expected.InnerException);
        Assert.Contains("while loading", expected.Message);
        Assert.Contains(error.Message, expected.Message);
        Assert.Same(error, failed.expect_err("unused"));

        Result<int>.Ok(5).expect("unused");
        Result.Ok().expect("unused");
        var untypedExpected = Assert.Throws<Exception>(() => Result.Err(error).expect("while loading untyped"));
        Assert.Same(error, untypedExpected.InnerException);
        Assert.Contains("while loading untyped", untypedExpected.Message);
        Assert.Same(error, Result.Err(error).expect_err("unused"));

        var successfulExpectError = Assert.Throws<Exception>(() => successful.expect_err("expected failure"));
        Assert.Contains("expected failure", successfulExpectError.Message);
    }

    [Fact]
    public void UnwrapErrAndExpectRejectTheWrongVariant()
    {
        var successful = Result.Ok();
        var successfulValue = Result<int>.Ok(1);
        var unwrapError = Assert.Throws<Exception>(() => successful.unwrap_err());
        var typedUnwrapError = Assert.Throws<Exception>(() => successfulValue.unwrap_err());
        var expectError = Assert.Throws<Exception>(() => successful.expect_err("expected failure"));

        Assert.Contains("unwrap_err", unwrapError.Message);
        Assert.Contains("unwrap_err", typedUnwrapError.Message);
        Assert.Contains("expected failure", expectError.Message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CaptureOriginException()
    {
        try
        {
            throw new InvalidOperationException("origin");
        }
        catch (Exception error)
        {
            return error;
        }
    }
}

public sealed class ResultSequenceBehaviorTests
{
    [Fact]
    public void SequenceOperationsCollectTransformFoldAndPartition()
    {
        var firstError = new InvalidOperationException("first");
        var secondError = new ArgumentException("second");
        Result<int>[] results = {
            Result<int>.Ok(2),
            Result<int>.Err(firstError),
            Result<int>.Ok(4),
            Result<int>.Err(secondError),
        };

        Assert.Equal(2, ResultImpl.try_value(Result<int>.Ok(2)));
        Assert.Null(ResultImpl.try_value(Result<int>.Err(firstError)));
        Assert.Equal(new Exception[] { firstError, secondError }, results.iter_errs().ToArray());
        Assert.Equal(new[] { 2, 4 }, results.iter_oks().ToArray());
        Assert.Equal(new Exception[] { firstError, secondError }, ResultImpl.iter_errs(results).ToArray());
        Assert.Equal(new[] { 2, 4 }, ResultImpl.iter_oks(results).ToArray());
        Result[] untypedResults = { Result.Ok(), Result.Err(firstError), Result.Err(secondError) };
        Assert.Equal(new Exception[] { firstError, secondError }, ResultImpl.iter_errs(untypedResults).ToArray());

        var collected = results.try_collect();
        Assert.True(collected.IsErr);
        Assert.Same(firstError, collected.unwrap_err());
        Assert.Same(firstError, results.try_fold(0, (sum, value) => sum + value).unwrap_err());
        Assert.Same(firstError, results.try_map(value => value * 3).unwrap_err());

        Result<int>[] successful = { Result<int>.Ok(2), Result<int>.Ok(4) };
        Assert.Equal(new[] { 2, 4 }, successful.try_collect().unwrap());
        Assert.Equal(6, successful.try_fold(0, (sum, value) => sum + value).unwrap());
        Assert.Equal(new[] { 6, 12 }, successful.try_map(value => value * 3).unwrap());

        var visited = new List<int>();
        Assert.True(new[] { 1, 2, 3 }.try_for_each(value =>
        {
            visited.Add(value);
            return Result.Ok();
        }).IsOk);
        Assert.Equal(new[] { 1, 2, 3 }, visited);
        Assert.Same(firstError, new[] { 1, 2, 3 }.try_for_each(value =>
        {
            if (value == 2) return Result.Err(firstError);
            return Result.Ok();
        }).unwrap_err());

        var (oks, errors) = results.unzip();
        Assert.Equal(new[] { 2, 4 }, oks);
        Assert.Equal(new Exception[] { firstError, secondError }, errors);
    }

    [Fact]
    public void FlattenSupportsTypedUntypedAndEmptySequences()
    {
        var outerError = new Exception("outer");
        var innerError = new Exception("inner");

        Assert.Equal(3, ResultImpl.flatten(Result<Result<int>>.Ok(Result<int>.Ok(3))).unwrap());
        Assert.Same(innerError, ResultImpl.flatten(Result<Result<int>>.Ok(Result<int>.Err(innerError))).unwrap_err());
        Assert.Same(outerError, ResultImpl.flatten(Result<Result<int>>.Err(outerError)).unwrap_err());
        Assert.True(ResultImpl.flatten(Result<Result>.Ok(Result.Ok())).IsOk);

        var untypedFlattened = new[] {
            Result<Result>.Ok(Result.Ok()),
            Result<Result>.Err(outerError),
            Result<Result>.Ok(Result.Err(innerError)),
        }.flatten().ToArray();
        Assert.True(untypedFlattened[0].IsOk);
        Assert.Same(outerError, untypedFlattened[1].unwrap_err());
        Assert.Same(innerError, untypedFlattened[2].unwrap_err());

        var flattened = new[] {
            Result<Result<int>>.Ok(Result<int>.Ok(1)),
            Result<Result<int>>.Err(outerError),
            Result<Result<int>>.Ok(Result<int>.Err(innerError)),
        }.flatten().ToArray();
        Assert.Equal(1, flattened[0].unwrap());
        Assert.Same(outerError, flattened[1].unwrap_err());
        Assert.Same(innerError, flattened[2].unwrap_err());
        Assert.Empty(Array.Empty<Result<Result<int>>>().flatten());
        Assert.Empty(Array.Empty<Result<Result>>().flatten());
    }

    [Fact]
    public void LazySequenceOperationsDisposeTheirSourceEnumerator()
    {
        var disposed = 0;
        var error = new Exception("failure");
        using (var enumerator = ResultImpl.iter_errs(ResultsWithDisposal(error, () => disposed++)).GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            Assert.Same(error, enumerator.Current);
        }

        Assert.Equal(1, disposed);

        var collectDisposed = 0;
        var collected = ResultImpl.try_collect(ResultsWithTypedDisposal(error, () => collectDisposed++));
        Assert.Same(error, collected.unwrap_err());
        Assert.Equal(1, collectDisposed);

        var flattenMoves = 0;
        var flattenDisposed = 0;
        var lazyFlatten = ResultImpl.flatten(NestedResultsWithDisposal(
            error,
            () => flattenMoves++,
            () => flattenDisposed++));
        Assert.Equal(0, flattenMoves);
        using (var flattenEnumerator = lazyFlatten.GetEnumerator())
        {
            Assert.True(flattenEnumerator.MoveNext());
            Assert.True(flattenEnumerator.Current.IsOk);
            Assert.Equal(1, flattenMoves);
        }

        Assert.Equal(1, flattenDisposed);
    }

    private static IEnumerable<Result> ResultsWithDisposal(Exception error, Action onDispose)
    {
        try
        {
            yield return Result.Err(error);
            yield return Result.Ok();
        }
        finally
        {
            onDispose();
        }
    }

    private static IEnumerable<Result<int>> ResultsWithTypedDisposal(Exception error, Action onDispose)
    {
        try
        {
            yield return Result<int>.Ok(1);
            yield return Result<int>.Err(error);
            yield return Result<int>.Ok(3);
        }
        finally
        {
            onDispose();
        }
    }

    private static IEnumerable<Result<Result>> NestedResultsWithDisposal(
        Exception error,
        Action onMove,
        Action onDispose)
    {
        try
        {
            onMove();
            yield return Result<Result>.Ok(Result.Ok());
            onMove();
            yield return Result<Result>.Err(error);
        }
        finally
        {
            onDispose();
        }
    }
}

public sealed class OptionBehaviorTests
{
    [Fact]
    public void PresenceUnwrapFilteringAndFallbacksWork()
    {
        var some = OptionImpl.Some(4);
        var none = OptionImpl.None<int>();
        Assert.Equal(4, some.unwrap());
        Assert.Equal(4, some.expect("unused"));
        Assert.Throws<Exception>(() => none.unwrap());
        Assert.Contains("missing", Assert.Throws<Exception>(() => none.expect("missing")).Message);

        Assert.Equal(8, some.map_or(value => value * 2, 0));
        Assert.Equal(0, none.map_or(value => value * 2, 0));
        Assert.Equal(8, some.map_or_else(value => value * 2, () => 0));
        Assert.Equal(0, none.map_or_else(value => value * 2, () => 0));
        Assert.Equal(4, some.filter(value => value == 4));
        Assert.False(some.filter(value => value == 3).HasValue);
        Assert.False(none.filter(_ => true).HasValue);
        Assert.Equal(4, some.or(9));
        Assert.Equal(9, none.or(9));
        Assert.Equal(4, some.or_else(() => OptionImpl.Some(9)));
        Assert.Equal(9, none.or_else(() => OptionImpl.Some(9)));
    }

    [Fact]
    public void OptionCallbacksRemainLazyAndBooleanFactoriesInferValueTypes()
    {
        var mapCalls = 0;
        var fallbackCalls = 0;
        OptionImpl.None<int>().map_or(value =>
        {
            mapCalls++;
            return value;
        }, 0);
        OptionImpl.Some(1).map_or_else(value => value, () =>
        {
            fallbackCalls++;
            return 0;
        });
        Assert.Equal(0, mapCalls);
        Assert.Equal(0, fallbackCalls);

        var thenCalls = 0;
        Assert.Equal(7, true.then(() =>
        {
            thenCalls++;
            return 7;
        }));
        Assert.Equal(7, true.then(ReturnSeven));
        Assert.False(false.then(() =>
        {
            thenCalls++;
            return 8;
        }).HasValue);
        Assert.Equal(1, thenCalls);
        Assert.Equal(9, true.then_some(9));
        Assert.False(false.then_some(9).HasValue);

        var inspectCalls = 0;
        OptionImpl.Some(2).inspect(value => inspectCalls += value);
        OptionImpl.None<int>().inspect(_ => inspectCalls++);
        Assert.Equal(2, inspectCalls);
    }

    [Fact]
    public void OptionConvertsToResultsAndSupportsAllOutputShapes()
    {
        var error = new Exception("missing");
        Assert.True(OptionImpl.Some(2).ok_or(error).IsOk);
        Assert.Same(error, OptionImpl.None<int>().ok_or(error).unwrap_err());

        var factoryCalls = 0;
        Assert.True(OptionImpl.Some(2).ok_or_else(() =>
        {
            factoryCalls++;
            return error;
        }).IsOk);
        Assert.Same(error, OptionImpl.None<int>().ok_or_else(() =>
        {
            factoryCalls++;
            return error;
        }).unwrap_err());
        Assert.Equal(1, factoryCalls);

        Assert.Equal(6, OptionImplWithValueOutput.map(OptionImpl.Some(2), value => value * 3));
        Assert.False(OptionImplWithValueOutput.map(OptionImpl.None<int>(), value => value * 3).HasValue);
        Assert.Equal(5, OptionImplWithValueOutput.and(OptionImpl.Some(2), OptionImpl.Some(5)));
        Assert.False(OptionImplWithValueOutput.and(OptionImpl.None<int>(), OptionImpl.Some(5)).HasValue);
        Assert.Equal(6, OptionImplWithValueOutput.and_then(OptionImpl.Some(2), value => OptionImpl.Some(value * 3)));

        Assert.Equal("2", OptionImplWithRefOutput.map(OptionImpl.Some(2), value => value.ToString()));
        Assert.Null(OptionImplWithRefOutput.map(OptionImpl.None<int>(), value => value.ToString()));
        Assert.Equal("fallback", OptionImplWithRefOutput.and(OptionImpl.Some(2), "fallback"));
        Assert.Null(OptionImplWithRefOutput.and(OptionImpl.None<int>(), "fallback"));
        Assert.Equal("2", OptionImplWithRefOutput.and_then(OptionImpl.Some(2), value => value.ToString()));
        Assert.Null(OptionImplWithRefOutput.and_then(OptionImpl.None<int>(), value => value.ToString()));
    }

    private static int ReturnSeven() => 7;
}
