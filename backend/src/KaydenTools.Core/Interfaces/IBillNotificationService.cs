namespace KaydenTools.Core.Interfaces;

/// <summary>
/// 帳單通知服務
/// </summary>
public interface IBillNotificationService
{
    /// <summary>
    /// 通知所有相關用戶帳單已更新
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="newVersion">新版本號</param>
    /// <param name="userId">觸發更新的使用者 ID (可為 null，代表匿名)</param>
    Task NotifyBillUpdatedAsync(Guid billId, long newVersion, Guid? userId);
}
