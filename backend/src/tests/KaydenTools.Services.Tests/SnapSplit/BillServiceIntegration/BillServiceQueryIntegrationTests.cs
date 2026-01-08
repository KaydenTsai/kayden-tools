using FluentAssertions;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 查詢整合測試
/// 擁有者查詢、關聯用戶查詢
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceQueryIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region 擁有者查詢測試

    [Fact]
    public async Task GetByOwnerIdAsync_應回傳該用戶的所有帳單摘要()
    {
        // Arrange
        var owner = await SeedUser();
        var otherOwner = await SeedUser();
        await SeedBill("帳單1", owner.Id);
        await SeedBill("帳單2", owner.Id);
        await SeedBill("其他人的帳單", otherOwner.Id); // 不應包含

        // Act
        var result = await BillService.GetByOwnerIdAsync(owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(b => b.Name).Should().Contain(new[] { "帳單1", "帳單2" });
    }

    [Fact]
    public async Task GetByOwnerIdAsync_用戶無帳單_應回傳空列表()
    {
        // Arrange
        var owner = await SeedUser();

        // Act
        var result = await BillService.GetByOwnerIdAsync(owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region 關聯用戶查詢測試

    [Fact]
    public async Task GetByLinkedUserIdAsync_應回傳用戶認領的所有帳單()
    {
        // Arrange - 建立真實用戶（避免 FK 約束錯誤）
        var user = await SeedUser();
        var userId = user.Id;

        // 建立帳單並讓成員認領
        var bill1 = await SeedBill("認領帳單1");
        await SeedMember(bill1, "我", linkedUserId: userId);

        var bill2 = await SeedBill("認領帳單2");
        await SeedMember(bill2, "我", linkedUserId: userId);

        // 建立未認領的帳單（不應包含）
        var bill3 = await SeedBill("未認領帳單");
        await SeedMember(bill3, "其他人");

        // 清除追蹤，確保查詢從資料庫取得最新資料
        ClearChangeTracker();

        // Act
        var result = await BillService.GetByLinkedUserIdAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(b => b.Name).Should().Contain(new[] { "認領帳單1", "認領帳單2" });
    }

    #endregion
}
