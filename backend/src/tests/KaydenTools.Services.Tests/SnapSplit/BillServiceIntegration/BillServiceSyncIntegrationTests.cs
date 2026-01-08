using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 同步整合測試
/// SyncBill、DeltaSync、刪除同步
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceSyncIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region SyncBill 整合測試

    [Fact]
    public async Task SyncBillAsync_首次同步_應建立完整帳單結構()
    {
        // Arrange
        var owner = await SeedUser();
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "同步測試帳單",
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
                        ServiceFeePercent = 10,
                        IsItemized = false,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1", "m2" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await BillService.SyncBillAsync(request, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RemoteId.Should().NotBeEmpty();
        result.Value.ShareCode.Should().NotBeNullOrEmpty();
        result.Value.IdMappings.Members.Should().ContainKey("m1");
        result.Value.IdMappings.Members.Should().ContainKey("m2");
        result.Value.IdMappings.Expenses.Should().ContainKey("e1");

        // 驗證資料庫
        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        bill.Should().NotBeNull();
        bill!.Members.Should().HaveCount(2);
        bill.Expenses.Should().HaveCount(1);
        bill.Expenses.First().Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncBillAsync_更新現有帳單_應正確更新資料()
    {
        // Arrange - 先建立帳單（透過 SyncBillAsync 首次同步建立）
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "原始帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "原始成員", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        var createResult = await BillService.SyncBillAsync(createRequest, null);
        createResult.IsSuccess.Should().BeTrue();
        var billId = createResult.Value.RemoteId;
        var memberId = createResult.Value.IdMappings.Members["m1"];
        var currentVersion = createResult.Value.Version;

        // 驗證首次同步返回的版本號 (新帳單 Version 默認為 1，遞增後為 2)
        currentVersion.Should().Be(2, "首次同步應返回遞增後的版本號 2");

        // 立即檢查資料庫中的版本號
        ClearChangeTracker(); // 強制從 DB 重新載入
        var billAfterFirstSync = await ReloadBillFromDb(billId);
        billAfterFirstSync.Should().NotBeNull("帳單應該存在於資料庫");
        billAfterFirstSync!.Version.Should().Be(2, "資料庫中的版本號應該是 2");
        billAfterFirstSync.Members.Should().HaveCount(1, "資料庫中應該有 1 個成員");

        // 驗證成員 ID 正確
        var firstMember = billAfterFirstSync.Members.First();
        firstMember.Id.Should().Be(memberId, "成員 ID 應該匹配");
        firstMember.Name.Should().Be("原始成員", "成員名稱應該正確");

        // 暫時使用同一個 scope 測試（排除 scope 相關問題）
        ClearChangeTracker(); // 清除追蹤狀態避免衝突

        // Act - 更新帳單
        // 確認傳入的 BaseVersion
        currentVersion.Should().Be(2, "更新請求的 BaseVersion 應為 2");

        // 測試只更新帳單名稱（不修改成員以避免 EF Core 追蹤問題）
        var updateRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = billId,
            BaseVersion = currentVersion,
            Name = "更新後帳單",
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

        // 驗證 updateRequest 的 BaseVersion 正確設置
        updateRequest.BaseVersion.Should().Be(2, "updateRequest.BaseVersion 應為 2");

        var result = await BillService.SyncBillAsync(updateRequest, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 如果有衝突，輸出詳細資訊以便除錯
        if (result.Value.LatestBill != null)
        {
            throw new Xunit.Sdk.XunitException(
                $"版本衝突偵測！\n" +
                $"請求 BaseVersion: {updateRequest.BaseVersion}\n" +
                $"回傳版本: {result.Value.Version}\n" +
                $"LatestBill 版本: {result.Value.LatestBill.Version}\n" +
                $"LatestBill 名稱: {result.Value.LatestBill.Name}");
        }

        result.Value.LatestBill.Should().BeNull("應該沒有版本衝突（使用正確的 BaseVersion）");

        var billInDb = await ReloadBillFromDb(billId);
        billInDb!.Name.Should().Be("更新後帳單");
        billInDb.Members.Should().HaveCount(1); // 成員數量不變
    }

    [Fact]
    public async Task SyncBillAsync_版本衝突_應回傳最新帳單資料()
    {
        // Arrange
        var bill = await SeedBill("測試帳單");

        // 模擬版本衝突：使用過期的 BaseVersion
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = bill.Id,
            BaseVersion = bill.Version - 1, // 過期版本
            Name = "衝突更新",
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
        var result = await BillService.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LatestBill.Should().NotBeNull(); // 回傳最新資料供 rebase
    }

    [Fact]
    public async Task SyncBillAsync_含費用細項_應正確建立細項與參與者()
    {
        // Arrange
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = null,
            BaseVersion = 0,
            Name = "細項測試帳單",
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
                        Name = "聚餐",
                        Amount = 1000,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>
                            {
                                new()
                                {
                                    LocalId = "i1",
                                    Name = "牛排",
                                    Amount = 600,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1" }
                                },
                                new()
                                {
                                    LocalId = "i2",
                                    Name = "沙拉",
                                    Amount = 400,
                                    PaidByLocalId = "m2",
                                    ParticipantLocalIds = new List<string> { "m1", "m2" }
                                }
                            },
                            DeletedIds = new List<string>()
                        }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await BillService.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings.ExpenseItems.Should().ContainKey("i1");
        result.Value.IdMappings.ExpenseItems.Should().ContainKey("i2");

        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        var expense = bill!.Expenses.First();
        expense.Items.Should().HaveCount(2);
        expense.Items.First(i => i.Name == "牛排").Participants.Should().HaveCount(1);
        expense.Items.First(i => i.Name == "沙拉").Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncBillAsync_刪除成員_應從資料庫移除()
    {
        // Arrange - 先建立帳單（透過 SyncBillAsync 首次同步建立）
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m-keep", Name = "保留成員", DisplayOrder = 0 },
                    new() { LocalId = "m-delete", Name = "刪除成員", DisplayOrder = 1 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        var createResult = await BillService.SyncBillAsync(createRequest, null);
        createResult.IsSuccess.Should().BeTrue();
        var billId = createResult.Value.RemoteId;
        var memberToKeepId = createResult.Value.IdMappings.Members["m-keep"];
        var memberToDeleteId = createResult.Value.IdMappings.Members["m-delete"];
        var currentVersion = createResult.Value.Version;

        // Act - 刪除成員
        var updateRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = billId,
            BaseVersion = currentVersion,
            Name = "測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new()
                    {
                        LocalId = "m-keep",
                        RemoteId = memberToKeepId,
                        Name = "保留成員",
                        DisplayOrder = 0
                    }
                },
                DeletedIds = new List<string> { memberToDeleteId.ToString() }
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        var result = await BillService.SyncBillAsync(updateRequest, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LatestBill.Should().BeNull("應該沒有版本衝突");

        var reloaded = await ReloadBillFromDb(billId);
        reloaded!.Members.Should().HaveCount(1);
        reloaded.Members.First().Name.Should().Be("保留成員");
    }

    #endregion
    #region DeltaSync 整合測試

    [Fact]
    public async Task DeltaSyncAsync_新增成員_應正確加入資料庫()
    {
        // Arrange - 建立帳單並預先添加一個成員（確保 Bill 有初始資料）
        var bill = await SeedBill("Delta測試帳單");
        await SeedMember(bill, "初始成員", displayOrder: 0);
        var billId = bill.Id;

        // 清除追蹤，確保從 DB 取得最新版本
        ClearChangeTracker();

        // 重新取得帳單版本（因為 SeedMember 不會更新 bill 物件的 Version）
        var reloadedBill = await ReloadBillFromDb(billId);
        var billVersion = reloadedBill!.Version;

        var request = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "m1", Name = "新成員1", DisplayOrder = 1 },
                    new() { LocalId = "m2", Name = "新成員2", DisplayOrder = 2 }
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(billId, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 除錯資訊
        if (!result.Value.Success)
        {
            var conflicts = result.Value.Conflicts ?? new List<ConflictInfo>();
            var conflictDetails = conflicts.Count > 0
                ? string.Join(", ", conflicts.Select(c => $"{c.Type}:{c.EntityId}:{c.Resolution}"))
                : "無衝突";
            throw new Xunit.Sdk.XunitException(
                $"DeltaSync Success = False!\n" +
                $"Conflicts: {conflictDetails}\n" +
                $"MergedBill: {(result.Value.MergedBill != null ? "有" : "無")}\n" +
                $"BaseVersion: {billVersion}, NewVersion: {result.Value.NewVersion}\n" +
                $"IdMappings.Members 數量: {result.Value.IdMappings?.Members?.Count ?? 0}");
        }

        result.Value.Success.Should().BeTrue();
        result.Value.IdMappings.Members.Should().ContainKey("m1");
        result.Value.IdMappings.Members.Should().ContainKey("m2");

        // 應有 3 個成員（1 初始 + 2 新增）
        await AssertMemberCount(billId, 3);
    }

    [Fact]
    public async Task DeltaSyncAsync_更新費用_應修改資料庫記錄()
    {
        // Arrange
        var bill = await SeedBill("Delta測試帳單");
        var member = await SeedMember(bill, "付款人");
        var expense = await SeedExpense(bill, "原始費用", amount: 100, paidBy: member, participants: new[] { member });
        var billId = bill.Id;
        var billVersion = bill.Version;
        var expenseId = expense.Id;

        // 清除追蹤，避免與 BillService 的追蹤衝突
        ClearChangeTracker();

        var request = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        Name = "更新費用",
                        Amount = 200
                    }
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(billId, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var reloaded = await ReloadBillFromDb(billId);
        var updatedExpense = reloaded!.Expenses.First();
        updatedExpense.Name.Should().Be("更新費用");
        updatedExpense.Amount.Should().Be(200);
    }

    [Fact]
    public async Task DeltaSyncAsync_刪除費用_應從資料庫移除()
    {
        // Arrange
        var bill = await SeedBill("Delta測試帳單");
        var member = await SeedMember(bill, "成員");
        var expense = await SeedExpense(bill, "待刪除費用", paidBy: member, participants: new[] { member });
        var billId = bill.Id;
        var billVersion = bill.Version;
        var expenseId = expense.Id;

        // 清除追蹤，避免與 BillService 的追蹤衝突
        ClearChangeTracker();

        var request = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { expenseId }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(billId, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseCount(billId, 0);
    }

    [Fact]
    public async Task DeltaSyncAsync_新增費用含參與者_應正確建立關聯()
    {
        // Arrange
        var bill = await SeedBill("Delta測試帳單");
        var member1 = await SeedMember(bill, "Alice");
        var member2 = await SeedMember(bill, "Bob");
        var billId = bill.Id;
        var billVersion = bill.Version;
        var member1Id = member1.Id;
        var member2Id = member2.Id;

        // 清除追蹤，避免與 BillService 的追蹤衝突
        ClearChangeTracker();

        var request = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "e1",
                        Name = "新費用",
                        Amount = 500,
                        PaidByMemberId = member1Id.ToString(),
                        ParticipantIds = new List<string> { member1Id.ToString(), member2Id.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(billId, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var reloaded = await ReloadBillFromDb(billId);
        var expense = reloaded!.Expenses.First();
        expense.Participants.Should().HaveCount(2);
    }

    #endregion
    #region DeltaSync 刪除整合測試

    [Fact]
    public async Task DeltaSyncAsync_刪除成員_應從資料庫移除()
    {
        // Arrange
        var bill = await SeedBill("DeltaSync刪除成員測試");
        var member1 = await SeedMember(bill, "保留成員", displayOrder: 0);
        var member2 = await SeedMember(bill, "刪除成員", displayOrder: 1);

        ClearChangeTracker();
        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member2.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertMemberCount(bill.Id, 1);

        var remainingMember = await Db.Members.FirstOrDefaultAsync(m => m.BillId == bill.Id);
        remainingMember!.Name.Should().Be("保留成員");
    }

    [Fact]
    public async Task DeltaSyncAsync_刪除費用細項_應從資料庫移除()
    {
        // Arrange
        var bill = await SeedBill("DeltaSync刪除細項測試");
        var member = await SeedMember(bill, "Alice");
        var expense = await SeedExpense(bill, "晚餐", 1000, paidBy: member, isItemized: true);
        var item1 = await SeedExpenseItem(expense, "牛排", 600, paidBy: member);
        var item2 = await SeedExpenseItem(expense, "沙拉", 400, paidBy: member);

        ClearChangeTracker();
        await AssertExpenseItemCount(expense.Id, 2);

        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Delete = new List<Guid> { item1.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseItemCount(expense.Id, 1);

        var remainingItem = await Db.ExpenseItems.FirstOrDefaultAsync(i => i.ExpenseId == expense.Id);
        remainingItem!.Name.Should().Be("沙拉");
    }

    #endregion
    #region SyncBill 刪除整合測試

    [Fact]
    public async Task SyncBillAsync_刪除費用_應從資料庫移除()
    {
        // Arrange - 先建立帳單
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = null,
            BaseVersion = 0,
            Name = "SyncBill刪除費用測試",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 }
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
                        ParticipantLocalIds = new List<string> { "m1" },
                        IsItemized = false
                    },
                    new()
                    {
                        LocalId = "e2",
                        Name = "晚餐",
                        Amount = 500,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1" },
                        IsItemized = false
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var createResult = await BillService.SyncBillAsync(createRequest, null);
        createResult.IsSuccess.Should().BeTrue();
        var billId = createResult.Value.RemoteId;
        var expenseIdToDelete = createResult.Value.IdMappings.Expenses["e1"];
        var currentVersion = createResult.Value.Version;

        await AssertExpenseCount(billId, 2);

        // Act - 刪除一個費用
        var deleteRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = currentVersion,
            Name = "SyncBill刪除費用測試",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = createResult.Value.IdMappings.Members["m1"], Name = "Alice", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>
                {
                    new()
                    {
                        LocalId = "e2",
                        RemoteId = createResult.Value.IdMappings.Expenses["e2"],
                        Name = "晚餐",
                        Amount = 500,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1" },
                        IsItemized = false
                    }
                },
                DeletedIds = new List<string> { expenseIdToDelete.ToString() }
            }
        };

        var result = await BillService.SyncBillAsync(deleteRequest, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseCount(billId, 1);

        ClearChangeTracker();
        var remainingExpense = await Db.Expenses.FirstOrDefaultAsync(e => e.BillId == billId);
        remainingExpense!.Name.Should().Be("晚餐");
    }

    [Fact]
    public async Task SyncBillAsync_刪除費用細項_應從資料庫移除()
    {
        // Arrange - 建立含細項的帳單
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = null,
            BaseVersion = 0,
            Name = "SyncBill刪除細項測試",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 }
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
                        Name = "晚餐",
                        Amount = 1000,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>
                            {
                                new()
                                {
                                    LocalId = "i1",
                                    Name = "牛排",
                                    Amount = 600,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1" }
                                },
                                new()
                                {
                                    LocalId = "i2",
                                    Name = "沙拉",
                                    Amount = 400,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1" }
                                }
                            },
                            DeletedIds = new List<string>()
                        }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var createResult = await BillService.SyncBillAsync(createRequest, null);
        createResult.IsSuccess.Should().BeTrue();
        var billId = createResult.Value.RemoteId;
        var expenseId = createResult.Value.IdMappings.Expenses["e1"];
        var itemIdToDelete = createResult.Value.IdMappings.ExpenseItems["i1"];
        var currentVersion = createResult.Value.Version;

        await AssertExpenseItemCount(expenseId, 2);

        // Act - 刪除一個細項
        var deleteRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = currentVersion,
            Name = "SyncBill刪除細項測試",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = createResult.Value.IdMappings.Members["m1"], Name = "Alice", DisplayOrder = 0 }
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
                        RemoteId = expenseId,
                        Name = "晚餐",
                        Amount = 1000,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>
                            {
                                new()
                                {
                                    LocalId = "i2",
                                    RemoteId = createResult.Value.IdMappings.ExpenseItems["i2"],
                                    Name = "沙拉",
                                    Amount = 400,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1" }
                                }
                            },
                            DeletedIds = new List<string> { itemIdToDelete.ToString() }
                        }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var result = await BillService.SyncBillAsync(deleteRequest, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseItemCount(expenseId, 1);

        ClearChangeTracker();
        var remainingItem = await Db.ExpenseItems.FirstOrDefaultAsync(i => i.ExpenseId == expenseId);
        remainingItem!.Name.Should().Be("沙拉");
    }

    #endregion
}
