namespace FinFlow.Domain.Abstractions;

public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");
    public static Error FromException(Exception ex) => new("Error.Exception", ex.Message);
    public static implicit operator string(Error error) => error.Code;
    public override string ToString() => Code;
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException();
        if (!isSuccess && error == Error.None) throw new InvalidOperationException();
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);
    public static Result Create(bool condition, Error error) => condition ? Success() : Failure(error);
    public static Result<TValue> Create<TValue>(TValue? value, Error error) =>
        value is not null ? Success(value) : Failure<TValue>(error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;
    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access value of failed result. Error: {Error.Code}");
    public static implicit operator Result<TValue>(TValue? value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
