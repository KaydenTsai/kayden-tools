using Kayden.Commons.Interfaces;

namespace KaydenTools.TestUtilities.Fakes;

/// <summary>
/// 測試用的日期時間服務
/// </summary>
public class FakeDateTimeService : IDateTimeService
{
    private DateTime? _frozenTime;

    public DateTime UtcNow => _frozenTime ?? DateTime.UtcNow;

    public DateTimeOffset UtcNowOffset => _frozenTime.HasValue
        ? new DateTimeOffset(_frozenTime.Value, TimeSpan.Zero)
        : DateTimeOffset.UtcNow;

    /// <summary>
    /// 凍結時間為指定值
    /// </summary>
    public void Freeze(DateTime time) => _frozenTime = time;

    /// <summary>
    /// 解凍時間，恢復使用真實時間
    /// </summary>
    public void Unfreeze() => _frozenTime = null;

    /// <summary>
    /// 凍結為當前 UTC 時間
    /// </summary>
    public void FreezeNow() => _frozenTime = DateTime.UtcNow;
}
