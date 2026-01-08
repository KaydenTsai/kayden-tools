namespace Kayden.Commons.Common;

/// <summary>
/// 錯誤資訊
/// </summary>
/// <param name="Code">錯誤碼</param>
/// <param name="Message">錯誤訊息</param>
public record Error(string Code, string Message)
{
    /// <summary>
    /// 無錯誤
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// 隱式轉換：字串轉為錯誤（使用通用錯誤碼）
    /// </summary>
    public static implicit operator Error(string message)
    {
        return new Error("ERROR", message);
    }
}

/// <summary>
/// 操作結果
/// </summary>
public class Result
{
    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="error">錯誤資訊</param>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 是否失敗
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 錯誤資訊
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static Result Success()
    {
        return new Result(true, Error.None);
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <param name="error">錯誤資訊</param>
    public static Result Failure(Error error)
    {
        return new Result(false, error);
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <param name="code">錯誤碼</param>
    /// <param name="message">錯誤訊息</param>
    public static Result Failure(string code, string message)
    {
        return new Result(false, new Error(code, message));
    }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    /// <typeparam name="T">值類型</typeparam>
    /// <param name="value">回傳值</param>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value, true, Error.None);
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <typeparam name="T">值類型</typeparam>
    /// <param name="error">錯誤資訊</param>
    public static Result<T> Failure<T>(Error error)
    {
        return new Result<T>(default, false, error);
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <typeparam name="T">值類型</typeparam>
    /// <param name="code">錯誤碼</param>
    /// <param name="message">錯誤訊息</param>
    public static Result<T> Failure<T>(string code, string message)
    {
        return new Result<T>(default, false, new Error(code, message));
    }
}

/// <summary>
/// 操作結果（含回傳值）
/// </summary>
/// <typeparam name="T">值類型</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="value">回傳值</param>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="error">錯誤資訊</param>
    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// 回傳值（失敗時存取會拋出例外）
    /// </summary>
    /// <exception cref="InvalidOperationException">當結果為失敗時存取</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value when result is failure. Error: {Error.Message}");

    /// <summary>
    /// 隱式轉換：值轉為成功結果
    /// </summary>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }
}
