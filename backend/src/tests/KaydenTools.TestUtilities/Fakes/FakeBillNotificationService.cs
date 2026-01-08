using KaydenTools.Core.Interfaces;

namespace KaydenTools.TestUtilities.Fakes;

/// <summary>
/// 測試用的帳單通知服務（不實際發送通知）
/// </summary>
public class FakeBillNotificationService : IBillNotificationService
{
    private readonly List<(Guid BillId, long Version, Guid? UserId)> _notifications = new();

    /// <summary>
    /// 已發送的通知記錄
    /// </summary>
    public IReadOnlyList<(Guid BillId, long Version, Guid? UserId)> SentNotifications => _notifications;

    /// <summary>
    /// 清除通知記錄
    /// </summary>
    public void Clear() => _notifications.Clear();

    public Task NotifyBillUpdatedAsync(Guid billId, long version, Guid? excludeUserId = null)
    {
        _notifications.Add((billId, version, excludeUserId));
        return Task.CompletedTask;
    }
}
