namespace FinFlow.Domain.Abstractions;

public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper) =>
        result.IsSuccess ? Result.Success(mapper(result.Value)) : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> Map<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> mapper)
    {
        var result = await resultTask;
        return result.Map(mapper);
    }

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> binder) =>
        result.IsSuccess ? binder(result.Value) : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<Result<TOut>>> binder) =>
        result.IsSuccess ? await binder(result.Value) : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> Bind<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> binder)
    {
        var result = await resultTask;
        return await result.Bind(binder);
    }

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess) action(result.Value);
        return result;
    }

    public static async Task<Result<T>> Tap<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess) await action(result.Value);
        return result;
    }

    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        if (result.IsFailure) return result;
        return predicate(result.Value) ? result : Result.Failure<T>(error);
    }

    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
            if (result.IsFailure) return result;
        return Result.Success();
    }

    public static T GetValueOrDefault<T>(this Result<T> result, T defaultValue = default!) =>
        result.IsSuccess ? result.Value : defaultValue;
}
