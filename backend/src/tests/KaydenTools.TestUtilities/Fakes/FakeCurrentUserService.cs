using KaydenTools.Core.Interfaces;

namespace KaydenTools.TestUtilities.Fakes;

/// <summary>
/// 測試用的當前用戶服務
/// </summary>
public class FakeCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public bool IsAuthenticated => UserId.HasValue;

    /// <summary>
    /// 設定當前用戶
    /// </summary>
    public void SetUser(Guid userId) => UserId = userId;

    /// <summary>
    /// 清除當前用戶
    /// </summary>
    public void ClearUser() => UserId = null;
}
