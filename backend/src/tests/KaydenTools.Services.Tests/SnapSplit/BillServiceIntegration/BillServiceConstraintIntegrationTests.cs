using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 約束與刪除整合測試
/// FK 約束、級聯刪除、邊界情況
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceConstraintIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region FK 約束測試

    [Fact]
    public async Task 費用參與者_成員被刪除_費用參與者應被級聯刪除()
    {
        // Arrange - 先建立帳單（透過 SyncBillAsync 首次同步建立）
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "FK測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "測試成員", DisplayOrder = 0 }
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
                        Name = "測試費用",
                        Amount = 100,
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
        var memberId = createResult.Value.IdMappings.Members["m1"];
        var expenseId = createResult.Value.IdMappings.Expenses["e1"];
        var currentVersion = createResult.Value.Version;

        // Act - 透過 SyncBill 刪除成員和費用
        var deleteRequest = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = billId,
            BaseVersion = currentVersion,
            Name = "FK測試帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string> { memberId.ToString() }
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string> { expenseId.ToString() }
            }
        };

        var result = await BillService.SyncBillAsync(deleteRequest, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LatestBill.Should().BeNull("應該沒有版本衝突");
        await AssertMemberCount(billId, 0);
        await AssertExpenseCount(billId, 0);
    }

    #endregion
    #region 級聯刪除測試

    [Fact]
    public async Task 刪除成員_費用參與者應被級聯刪除_費用保留()
    {
        // Arrange - 建立帳單含兩個成員和一個費用
        var bill = await SeedBill("級聯刪除測試");
        var member1 = await SeedMember(bill, "Alice", displayOrder: 0);
        var member2 = await SeedMember(bill, "Bob", displayOrder: 1);
        var expense = await SeedExpense(bill, "午餐", 300, paidBy: member1, participants: new[] { member1, member2 });

        ClearChangeTracker();

        // 驗證初始狀態
        await AssertExpenseParticipantCount(expense.Id, 2);

        // 取得帳單當前版本
        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act - 只刪除 member2（透過 DeltaSync）
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
        await AssertMemberCount(bill.Id, 1); // 只剩 Alice
        await AssertExpenseCount(bill.Id, 1); // 費用保留
        await AssertExpenseParticipantCount(expense.Id, 1); // 只剩 Alice 的參與記錄
    }

    [Fact]
    public async Task 刪除費用_費用參與者應被級聯刪除()
    {
        // Arrange
        var bill = await SeedBill("級聯刪除測試");
        var member = await SeedMember(bill, "Alice");
        var expense = await SeedExpense(bill, "午餐", 300, paidBy: member, participants: new[] { member });

        ClearChangeTracker();
        await AssertExpenseParticipantCount(expense.Id, 1);

        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act - 刪除費用
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { expense.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseCount(bill.Id, 0);
        // ExpenseParticipant 應隨費用一起被刪除
        var participantCount = await Db.ExpenseParticipants.CountAsync(p => p.ExpenseId == expense.Id);
        participantCount.Should().Be(0);
    }

    [Fact]
    public async Task 刪除費用_費用細項應被級聯刪除()
    {
        // Arrange - 建立含細項的費用
        var bill = await SeedBill("級聯刪除測試");
        var member = await SeedMember(bill, "Alice");
        var expense = await SeedExpense(bill, "晚餐", 1000, paidBy: member, isItemized: true);
        var item1 = await SeedExpenseItem(expense, "牛排", 600, paidBy: member, participants: new[] { member });
        var item2 = await SeedExpenseItem(expense, "沙拉", 400, paidBy: member, participants: new[] { member });

        ClearChangeTracker();
        await AssertExpenseItemCount(expense.Id, 2);

        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act - 刪除費用
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { expense.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseCount(bill.Id, 0);
        await AssertExpenseItemCount(expense.Id, 0); // 細項應隨費用刪除
    }

    [Fact]
    public async Task 刪除費用細項_細項參與者應被級聯刪除()
    {
        // Arrange
        var bill = await SeedBill("級聯刪除測試");
        var member1 = await SeedMember(bill, "Alice", displayOrder: 0);
        var member2 = await SeedMember(bill, "Bob", displayOrder: 1);
        var expense = await SeedExpense(bill, "晚餐", 1000, paidBy: member1, isItemized: true);
        var item = await SeedExpenseItem(expense, "牛排", 600, paidBy: member1, participants: new[] { member1, member2 });

        ClearChangeTracker();

        // 驗證初始狀態
        var initialParticipants = await Db.ExpenseItemParticipants.CountAsync(p => p.ExpenseItemId == item.Id);
        initialParticipants.Should().Be(2);

        // Act - 透過 SyncBill 刪除細項
        var reloadedBill = await ReloadBillFromDb(bill.Id);
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = bill.Id,
            BaseVersion = reloadedBill!.Version,
            Name = bill.Name,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = member1.Id, Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", RemoteId = member2.Id, Name = "Bob", DisplayOrder = 1 }
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
                        RemoteId = expense.Id,
                        Name = "晚餐",
                        Amount = 1000,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>(), // 不包含原細項 = 刪除
                            DeletedIds = new List<string> { item.Id.ToString() }
                        }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var result = await BillService.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertExpenseItemCount(expense.Id, 0);
        var remainingParticipants = await Db.ExpenseItemParticipants.CountAsync(p => p.ExpenseItemId == item.Id);
        remainingParticipants.Should().Be(0, "細項參與者應隨細項刪除");
    }

    #endregion
    #region 邊界情況刪除測試

    [Fact]
    public async Task 刪除已認領成員_應成功刪除並清除認領關聯()
    {
        // Arrange - 建立已認領的成員
        var user = await SeedUser();
        var bill = await SeedBill("認領測試帳單");
        var member = await SeedMember(bill, "已認領成員", linkedUserId: user.Id, customize: m =>
        {
            m.ClaimedAt = DateTime.UtcNow;
        });

        ClearChangeTracker();
        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertMemberCount(bill.Id, 0);
    }

    [Fact]
    public async Task 刪除付款者成員_費用付款者應被清空()
    {
        // Arrange
        var bill = await SeedBill("付款者刪除測試");
        var payer = await SeedMember(bill, "付款者", displayOrder: 0);
        var participant = await SeedMember(bill, "參與者", displayOrder: 1);
        var expense = await SeedExpense(bill, "午餐", 300, paidBy: payer, participants: new[] { payer, participant });

        ClearChangeTracker();

        // 驗證初始狀態
        var initialExpense = await Db.Expenses.FindAsync(expense.Id);
        initialExpense!.PaidById.Should().Be(payer.Id);

        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act - 刪除付款者
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { payer.Id }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await AssertMemberCount(bill.Id, 1);

        ClearChangeTracker();
        var updatedExpense = await Db.Expenses.FindAsync(expense.Id);
        updatedExpense!.PaidById.Should().BeNull("付款者被刪除後，費用的 PaidById 應為空");
    }

    [Fact]
    public async Task 重複刪除同一成員_第二次應忽略()
    {
        // Arrange
        var bill = await SeedBill("重複刪除測試");
        var member = await SeedMember(bill, "待刪除");

        ClearChangeTracker();
        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act - 第一次刪除
        var request1 = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member.Id }
            }
        };
        var result1 = await BillService.DeltaSyncAsync(bill.Id, request1, null);
        result1.IsSuccess.Should().BeTrue();

        // Act - 第二次刪除同一成員
        reloadedBill = await ReloadBillFromDb(bill.Id);
        var request2 = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member.Id }
            }
        };
        var result2 = await BillService.DeltaSyncAsync(bill.Id, request2, null);

        // Assert
        result2.IsSuccess.Should().BeTrue();
        await AssertMemberCount(bill.Id, 0);
    }

    [Fact]
    public async Task 刪除不存在的實體_應成功忽略()
    {
        // Arrange
        var bill = await SeedBill("不存在實體測試");
        var nonExistentId = Guid.NewGuid();

        ClearChangeTracker();
        var reloadedBill = await ReloadBillFromDb(bill.Id);

        // Act
        var request = new DeltaSyncRequest
        {
            BaseVersion = reloadedBill!.Version,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { nonExistentId }
            },
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { nonExistentId }
            }
        };
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region 幽靈參照測試 (Ghost References)

    /// <summary>
    /// 測試場景：刪除成員後，另一用戶嘗試新增費用並將該成員設為參與者
    /// - 用戶A: 刪除成員 Bob
    /// - 用戶B: 新增費用，參與者包含 Bob（使用過期版本）
    /// - 預期：應拒絕寫入，回傳錯誤
    /// </summary>
    [Fact]
    [Trait("Category", "GhostReference")]
    public async Task DeltaSyncAsync_刪除成員後新增費用使用該成員_應拒絕寫入()
    {
        // Arrange
        var bill = await SeedBill("幽靈參照測試");
        var alice = await SeedMember(bill, "Alice", displayOrder: 0);
        var bob = await SeedMember(bill, "Bob", displayOrder: 1);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var aliceId = billAfterSetup.Members.First(m => m.Name == "Alice").Id;
        var bobId = billAfterSetup.Members.First(m => m.Name == "Bob").Id;

        // 用戶A: 刪除成員 Bob
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { bobId }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除成員應成功");

        // 用戶B: 使用過期版本新增費用，參與者包含已刪除的 Bob
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-ghost",
                        Name = "幽靈費用",
                        Amount = 300,
                        PaidByMemberId = aliceId.ToString(),
                        ParticipantIds = new List<string> { aliceId.ToString(), bobId.ToString() } // Bob 已刪除
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert: 應該拒絕寫入或回傳明確錯誤
        // 目前預期行為：應該失敗並提示成員無效
        resultB.IsFailure.Should().BeTrue("應拒絕包含已刪除成員的費用");
    }

    /// <summary>
    /// 測試場景：刪除成員後，另一用戶嘗試更新費用並將該成員設為付款者
    /// </summary>
    [Fact]
    [Trait("Category", "GhostReference")]
    public async Task DeltaSyncAsync_刪除成員後更新費用使用該成員為付款者_應拒絕寫入()
    {
        // Arrange
        var bill = await SeedBill("幽靈付款者測試");
        var alice = await SeedMember(bill, "Alice", displayOrder: 0);
        var bob = await SeedMember(bill, "Bob", displayOrder: 1);
        var expense = await SeedExpense(bill, "原始費用", 100, paidBy: alice, participants: new[] { alice });
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var bobId = billAfterSetup.Members.First(m => m.Name == "Bob").Id;
        var expenseId = billAfterSetup.Expenses.First().Id;

        // 用戶A: 刪除成員 Bob
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { bobId }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除成員應成功");

        // 用戶B: 使用過期版本更新費用，將已刪除的 Bob 設為付款者
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        PaidByMemberId = bobId.ToString() // Bob 已刪除
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert: 應該拒絕寫入
        resultB.IsFailure.Should().BeTrue("應拒絕使用已刪除成員為付款者");
    }

    /// <summary>
    /// 測試場景：刪除費用後，另一用戶嘗試在該費用下新增細項
    /// </summary>
    [Fact]
    [Trait("Category", "GhostReference")]
    public async Task DeltaSyncAsync_刪除費用後新增細項_應拒絕寫入()
    {
        // Arrange
        var bill = await SeedBill("幽靈費用測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedExpense(bill, "待刪除費用", 100, paidBy: member, isItemized: true);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var memberId = billAfterSetup.Members.First().Id;
        var expenseId = billAfterSetup.Expenses.First().Id;

        // 用戶A: 刪除費用
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { expenseId }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除費用應成功");

        // 用戶B: 使用過期版本在已刪除的費用下新增細項
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-ghost",
                        ExpenseId = expenseId.ToString(), // 費用已刪除
                        Name = "幽靈細項",
                        Amount = 50,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert: 細項不應該被建立（費用已不存在）
        // 目前實作會 continue 跳過，所以 Success = true 但細項不會建立
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.Should().BeEmpty("費用應已被刪除");
        // 驗證沒有孤立的細項
        var orphanItems = await Db.ExpenseItems.CountAsync(i => i.ExpenseId == expenseId);
        orphanItems.Should().Be(0, "不應建立孤立的細項");
    }

    /// <summary>
    /// 測試場景：細項參與者使用已刪除的成員
    /// </summary>
    [Fact]
    [Trait("Category", "GhostReference")]
    public async Task DeltaSyncAsync_新增細項使用已刪除成員為參與者_應拒絕寫入()
    {
        // Arrange
        var bill = await SeedBill("細項幽靈參照測試");
        var alice = await SeedMember(bill, "Alice", displayOrder: 0);
        var bob = await SeedMember(bill, "Bob", displayOrder: 1);
        var expense = await SeedExpense(bill, "細項費用", 500, paidBy: alice, isItemized: true);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var aliceId = billAfterSetup.Members.First(m => m.Name == "Alice").Id;
        var bobId = billAfterSetup.Members.First(m => m.Name == "Bob").Id;
        var expenseId = billAfterSetup.Expenses.First().Id;

        // 用戶A: 刪除成員 Bob
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { bobId }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除成員應成功");

        // 用戶B: 使用過期版本新增細項，參與者包含已刪除的 Bob
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-with-ghost",
                        ExpenseId = expenseId.ToString(),
                        Name = "含幽靈參與者的細項",
                        Amount = 100,
                        PaidByMemberId = aliceId.ToString(),
                        ParticipantIds = new List<string> { aliceId.ToString(), bobId.ToString() } // Bob 已刪除
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert: 應該拒絕寫入
        resultB.IsFailure.Should().BeTrue("應拒絕包含已刪除成員的細項");
    }

    #endregion
}
