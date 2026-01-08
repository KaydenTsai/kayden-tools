using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService CRUD 與分享碼整合測試
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceCrudIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region CRUD 測試

    [Fact]
    public async Task CreateAsync_應建立帳單並存入資料庫()
    {
        // Arrange
        var owner = await SeedUser();
        var dto = new CreateBillDto("整合測試帳單");

        // Act
        var result = await BillService.CreateAsync(dto, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("整合測試帳單");
        result.Value.Id.Should().NotBeEmpty();

        // 驗證資料庫 - 直接檢查建立的帳單存在
        var bill = await ReloadBillFromDb(result.Value.Id);
        bill.Should().NotBeNull();
        bill!.OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task CreateAsync_無擁有者_應建立匿名帳單()
    {
        // Arrange
        var dto = new CreateBillDto("匿名帳單");

        // Act
        var result = await BillService.CreateAsync(dto, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var bill = await ReloadBillFromDb(result.Value.Id);
        bill!.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_帳單存在_應回傳完整帳單資料()
    {
        // Arrange
        var bill = await SeedCompleteBill(memberCount: 3, expenseCount: 2);

        // Act
        var result = await BillService.GetByIdAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(bill.Id);
        result.Value.Members.Should().HaveCount(3);
        result.Value.Expenses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_帳單不存在_應回傳錯誤()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await BillService.GetByIdAsync(nonExistentId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_應更新帳單名稱()
    {
        // Arrange
        var bill = await SeedBill("原始名稱");
        var updateDto = new UpdateBillDto("更新後名稱");

        // Act
        var result = await BillService.UpdateAsync(bill.Id, updateDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("更新後名稱");

        // 驗證資料庫
        var reloaded = await ReloadBillFromDb(bill.Id);
        reloaded!.Name.Should().Be("更新後名稱");
    }

    [Fact]
    public async Task DeleteAsync_應從資料庫移除帳單()
    {
        // Arrange
        var bill = await SeedBill("待刪除帳單");

        // Act
        var result = await BillService.DeleteAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertBillNotExists(bill.Id);
    }

    [Fact]
    public async Task DeleteAsync_帳單含關聯資料_應軟刪除帳單()
    {
        // Arrange
        var bill = await SeedCompleteBill(memberCount: 2, expenseCount: 2);
        var billId = bill.Id;

        // Act
        var result = await BillService.DeleteAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 注意：系統使用軟刪除 (soft delete)
        // Bill 透過 QueryFilter 隱藏，但 Members/Expenses 仍存在
        // （因為實際 row 沒有被刪除，所以 cascade delete 不會觸發）
        await AssertBillNotExists(billId); // QueryFilter 會隱藏已刪除的 Bill

        // 驗證帳單確實被軟刪除
        var softDeletedBill = await Db.Bills
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == billId);
        softDeletedBill.Should().NotBeNull();
        softDeletedBill!.IsDeleted.Should().BeTrue();

        // 關聯資料仍存在（軟刪除不觸發級聯刪除）
        await AssertMemberCount(billId, 2);
        await AssertExpenseCount(billId, 2);
    }

    #endregion

    #region 分享碼測試

    [Fact]
    public async Task GenerateShareCodeAsync_應產生8位分享碼()
    {
        // Arrange
        var bill = await SeedBill("測試帳單");

        // Act
        var result = await BillService.GenerateShareCodeAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveLength(8);
        result.Value.Should().MatchRegex("^[A-Z0-9]{8}$");

        // 驗證資料庫
        var reloaded = await ReloadBillFromDb(bill.Id);
        reloaded!.ShareCode.Should().Be(result.Value);
    }

    [Fact]
    public async Task GetByShareCodeAsync_分享碼存在_應回傳帳單()
    {
        // Arrange
        var bill = await SeedBill("分享帳單");
        var shareCodeResult = await BillService.GenerateShareCodeAsync(bill.Id);

        // Act
        var result = await BillService.GetByShareCodeAsync(shareCodeResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(bill.Id);
    }

    [Fact]
    public async Task GetByShareCodeAsync_分享碼不存在_應回傳錯誤()
    {
        // Act
        var result = await BillService.GetByShareCodeAsync("INVALID1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
