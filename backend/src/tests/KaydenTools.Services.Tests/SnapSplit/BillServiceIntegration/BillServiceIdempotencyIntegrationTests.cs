using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 冪等性整合測試
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceIdempotencyIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region 冪等性整合測試

    [Fact]
    public async Task SyncBillAsync_重複首次同步_相同LocalId_應回傳現有帳單而非建立重複()
    {
        // Arrange
        var owner = await SeedUser();
        var localId = $"idempotency-test-{Guid.NewGuid():N}";

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = null,
            BaseVersion = 0,
            Name = "冪等性測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", Name = "Bob", DisplayOrder = 1 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>
                {
                    new()
                    {
                        LocalId = "e1",
                        Name = "午餐",
                        Amount = 300,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1", "m2" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        // Act - 第一次請求
        var result1 = await BillService.SyncBillAsync(request, owner.Id);

        // Assert - 第一次應成功建立
        result1.IsSuccess.Should().BeTrue();
        var billId = result1.Value.RemoteId;
        result1.Value.IdMappings.Members.Should().ContainKey("m1");
        result1.Value.IdMappings.Members.Should().ContainKey("m2");
        result1.Value.IdMappings.Expenses.Should().ContainKey("e1");

        // 驗證資料庫確實建立了帳單
        var billInDb = await ReloadBillFromDb(billId);
        billInDb.Should().NotBeNull();
        billInDb!.Members.Should().HaveCount(2);
        billInDb.Expenses.Should().HaveCount(1);

        // 記錄第一次建立的成員和費用 ID
        var firstMemberIds = billInDb.Members.Select(m => m.Id).ToHashSet();
        var firstExpenseIds = billInDb.Expenses.Select(e => e.Id).ToHashSet();

        // Act - 第二次請求（模擬網路超時重試，使用相同 LocalId 但無 RemoteId）
        ClearChangeTracker();
        var result2 = await BillService.SyncBillAsync(request, owner.Id);

        // Assert - 第二次應回傳現有帳單
        result2.IsSuccess.Should().BeTrue();
        result2.Value.RemoteId.Should().Be(billId, "應回傳相同的帳單 ID");

        // 應包含 LatestBill 讓前端重建狀態
        result2.Value.LatestBill.Should().NotBeNull("應回傳 LatestBill 讓前端重建狀態");
        result2.Value.LatestBill!.Members.Should().HaveCount(2);
        result2.Value.LatestBill!.Expenses.Should().HaveCount(1);

        // 驗證資料庫沒有建立重複的帳單
        var allBillsForOwner = await Db.Bills
            .Where(b => b.OwnerId == owner.Id && b.LocalClientId == localId)
            .ToListAsync();
        allBillsForOwner.Should().HaveCount(1, "不應建立重複的帳單");

        // 驗證成員沒有重複
        var billAfterSecondSync = await ReloadBillFromDb(billId);
        billAfterSecondSync!.Members.Should().HaveCount(2, "成員不應重複");
        billAfterSecondSync.Expenses.Should().HaveCount(1, "費用不應重複");

        // 驗證是原本的成員，不是新建的
        var currentMemberIds = billAfterSecondSync.Members.Select(m => m.Id).ToHashSet();
        currentMemberIds.SetEquals(firstMemberIds).Should().BeTrue("應該是原本的成員 ID");
    }

    [Fact]
    public async Task SyncBillAsync_不同用戶相同LocalId_應各自建立帳單()
    {
        // Arrange - 使用唯一 email 避免測試之間的資料衝突
        var owner1 = await SeedUser(email: $"user1-{Guid.NewGuid():N}@test.com");
        var owner2 = await SeedUser(email: $"user2-{Guid.NewGuid():N}@test.com");
        var localId = $"same-local-id-{Guid.NewGuid():N}";

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = null,
            BaseVersion = 0,
            Name = "共用 LocalId 帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "成員", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result1 = await BillService.SyncBillAsync(request, owner1.Id);
        var result2 = await BillService.SyncBillAsync(request, owner2.Id);

        // Assert - 兩個用戶應該各自建立帳單
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.RemoteId.Should().NotBe(result2.Value.RemoteId, "不同用戶應建立不同的帳單");

        // 驗證資料庫有兩個帳單
        var allBillsWithLocalId = await Db.Bills
            .Where(b => b.LocalClientId == localId)
            .ToListAsync();
        allBillsWithLocalId.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncBillAsync_無OwnerId_不執行冪等性檢查_每次建立新帳單()
    {
        // Arrange
        var localId = $"anonymous-{Guid.NewGuid():N}";

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = null,
            BaseVersion = 0,
            Name = "匿名帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act - 無 owner，每次都建立新帳單（這是預期行為，因為沒有 ownerId 無法做冪等性檢查）
        var result1 = await BillService.SyncBillAsync(request, null);
        var result2 = await BillService.SyncBillAsync(request, null);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.RemoteId.Should().NotBe(result2.Value.RemoteId,
            "匿名帳單無法做冪等性檢查，每次都建立新帳單");

        // 兩個帳單都應該儲存 LocalClientId
        var bill1 = await ReloadBillFromDb(result1.Value.RemoteId);
        var bill2 = await ReloadBillFromDb(result2.Value.RemoteId);
        bill1!.LocalClientId.Should().Be(localId);
        bill2!.LocalClientId.Should().Be(localId);
    }

    [Fact]
    public async Task SyncBillAsync_LocalClientId應正確儲存到資料庫()
    {
        // Arrange
        var owner = await SeedUser();
        var localId = $"store-test-{Guid.NewGuid():N}";

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = null,
            BaseVersion = 0,
            Name = "儲存測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await BillService.SyncBillAsync(request, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        bill.Should().NotBeNull();
        bill!.LocalClientId.Should().Be(localId, "LocalClientId 應正確儲存到資料庫");
    }

    #endregion

    #region DeltaSync 冪等性測試

    /// <summary>
    /// 測試場景：網路超時後重試，相同 LocalId 的成員 ADD 操作應該冪等
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSyncIdempotency")]
    public async Task DeltaSyncAsync_重複新增成員_相同LocalId_應回傳現有成員而非建立重複()
    {
        // Arrange
        var bill = await SeedBill("冪等性測試");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var initialMemberCount = billAfterSetup.Members.Count;

        var request = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "new-member-1", Name = "新成員", DisplayOrder = 10 }
                }
            }
        };

        // Act - 第一次請求
        var result1 = await BillService.DeltaSyncAsync(bill.Id, request, null);
        result1.IsSuccess.Should().BeTrue("第一次請求應成功");
        var memberId1 = result1.Value.IdMappings.Members["new-member-1"];

        // 更新 BaseVersion 模擬網路超時重試（客戶端不知道第一次成功）
        // 注意：真實重試場景中，客戶端會使用相同的 BaseVersion
        ClearChangeTracker();

        // Act - 第二次請求（相同內容，模擬重試）
        var result2 = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result2.IsSuccess.Should().BeTrue("第二次請求應成功（冪等）");

        // 應該回傳相同的成員 ID（而非建立新成員）
        result2.Value.IdMappings.Members.Should().ContainKey("new-member-1");
        var memberId2 = result2.Value.IdMappings.Members["new-member-1"];
        memberId2.Should().Be(memberId1, "相同 LocalId 應回傳相同的 RemoteId");

        // 驗證資料庫沒有重複成員
        var billAfterRetry = await ReloadBillFromDb(bill.Id);
        billAfterRetry!.Members.Should().HaveCount(initialMemberCount + 1, "不應建立重複成員");
    }

    /// <summary>
    /// 測試場景：網路超時後重試，相同 LocalId 的費用 ADD 操作應該冪等
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSyncIdempotency")]
    public async Task DeltaSyncAsync_重複新增費用_相同LocalId_應回傳現有費用而非建立重複()
    {
        // Arrange
        var bill = await SeedBill("費用冪等性測試");
        var member = await SeedMember(bill, "付款者");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var memberId = billAfterSetup.Members.First().Id;

        var request = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "new-expense-1",
                        Name = "新費用",
                        Amount = 500,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act - 第一次請求
        var result1 = await BillService.DeltaSyncAsync(bill.Id, request, null);
        result1.IsSuccess.Should().BeTrue("第一次請求應成功");
        var expenseId1 = result1.Value.IdMappings.Expenses["new-expense-1"];

        ClearChangeTracker();

        // Act - 第二次請求（相同內容，模擬重試）
        var result2 = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result2.IsSuccess.Should().BeTrue("第二次請求應成功（冪等）");
        result2.Value.IdMappings.Expenses.Should().ContainKey("new-expense-1");
        var expenseId2 = result2.Value.IdMappings.Expenses["new-expense-1"];
        expenseId2.Should().Be(expenseId1, "相同 LocalId 應回傳相同的 RemoteId");

        // 驗證資料庫沒有重複費用
        var billAfterRetry = await ReloadBillFromDb(bill.Id);
        billAfterRetry!.Expenses.Should().HaveCount(1, "不應建立重複費用");
    }

    /// <summary>
    /// 測試場景：網路超時後重試，相同 LocalId 的細項 ADD 操作應該冪等
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSyncIdempotency")]
    public async Task DeltaSyncAsync_重複新增細項_相同LocalId_應回傳現有細項而非建立重複()
    {
        // Arrange
        var bill = await SeedBill("細項冪等性測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedExpense(bill, "主費用", 1000, paidBy: member, isItemized: true);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var memberId = billAfterSetup.Members.First().Id;
        var expenseId = billAfterSetup.Expenses.First().Id;

        var request = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "new-item-1",
                        ExpenseId = expenseId.ToString(),
                        Name = "新細項",
                        Amount = 200,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act - 第一次請求
        var result1 = await BillService.DeltaSyncAsync(bill.Id, request, null);
        result1.IsSuccess.Should().BeTrue("第一次請求應成功");
        var itemId1 = result1.Value.IdMappings.ExpenseItems["new-item-1"];

        ClearChangeTracker();

        // Act - 第二次請求（相同內容，模擬重試）
        var result2 = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result2.IsSuccess.Should().BeTrue("第二次請求應成功（冪等）");
        result2.Value.IdMappings.ExpenseItems.Should().ContainKey("new-item-1");
        var itemId2 = result2.Value.IdMappings.ExpenseItems["new-item-1"];
        itemId2.Should().Be(itemId1, "相同 LocalId 應回傳相同的 RemoteId");

        // 驗證資料庫沒有重複細項
        var billAfterRetry = await ReloadBillFromDb(bill.Id);
        var expenseAfterRetry = billAfterRetry!.Expenses.First();
        expenseAfterRetry.Items.Should().HaveCount(1, "不應建立重複細項");
    }

    /// <summary>
    /// 測試場景：同一請求包含多個相同 LocalId 的 ADD（惡意或程式錯誤）
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSyncIdempotency")]
    public async Task DeltaSyncAsync_單次請求包含重複LocalId_應只建立一個實體()
    {
        // Arrange
        var bill = await SeedBill("重複LocalId測試");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        var request = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "duplicate-id", Name = "第一個", DisplayOrder = 0 },
                    new() { LocalId = "duplicate-id", Name = "第二個", DisplayOrder = 1 } // 重複 LocalId
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert - 應該只建立一個成員
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings.Members.Should().ContainKey("duplicate-id");

        var billAfterSync = await ReloadBillFromDb(bill.Id);
        billAfterSync!.Members.Should().HaveCount(1, "重複 LocalId 應只建立一個成員");
    }

    #endregion
}
