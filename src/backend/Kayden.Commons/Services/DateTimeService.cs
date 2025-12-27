using Kayden.Commons.Interfaces;

namespace Kayden.Commons.Services;

/// <summary>
/// 日期時間服務實作
/// </summary>
public class DateTimeService : IDateTimeService
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}
