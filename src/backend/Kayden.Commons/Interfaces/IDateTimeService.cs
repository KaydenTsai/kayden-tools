namespace Kayden.Commons.Interfaces;

/// <summary>
/// 日期時間服務介面
/// </summary>
public interface IDateTimeService
{
    /// <summary>
    /// 目前 UTC 時間
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// 目前 UTC 時間（含時區偏移）
    /// </summary>
    DateTimeOffset UtcNowOffset { get; }
}
