using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.Extensions.DependencyInjection;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 併發與協作整合測試
/// 併發更新、雙向編輯衝突
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceConcurrencyIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region 併發測試

    [Fact]
    public async Task DeltaSyncAsync_併發更新_應偵測版本衝突()
    {
        // Arrange
        var bill = await SeedBill("併發測試帳單");
        await SeedMember(bill, "成員");
        var billId = bill.Id;
        var initialVersion = bill.Version;

        // 清除追蹤，避免與 BillService 的追蹤衝突
        ClearChangeTracker();

        // 第一個更新成功
        var request1 = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            BillMeta = new BillMetaChangesDto { Name = "更新1" }
        };
        var result1 = await BillService.DeltaSyncAsync(billId, request1, null);
        result1.IsSuccess.Should().BeTrue();

        // 第二個更新使用舊版本，應偵測衝突
        var request2 = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 使用過期版本
            BillMeta = new BillMetaChangesDto { Name = "更新2" }
        };

        // Act
        var result2 = await BillService.DeltaSyncAsync(billId, request2, null);

        // Assert
        result2.IsSuccess.Should().BeTrue();
        // 衝突時應回傳最新資料
        result2.Value.MergedBill.Should().NotBeNull();
    }

    [Fact]
    public async Task DeltaSyncAsync_使用獨立Scope_模擬不同用戶併發()
    {
        // Arrange
        var bill = await SeedBill("併發Scope測試");
        await SeedMember(bill, "成員");
        var billId = bill.Id;
        var billVersion = bill.Version;

        // 清除追蹤，避免與獨立 Scope 的追蹤衝突
        ClearChangeTracker();

        // 使用獨立 Scope 模擬不同用戶
        using var scope1 = CreateNewScope();
        using var scope2 = CreateNewScope();

        var service1 = scope1.ServiceProvider.GetRequiredService<IBillService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IBillService>();

        var request1 = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            BillMeta = new BillMetaChangesDto { Name = "用戶1更新" }
        };

        var request2 = new DeltaSyncRequest
        {
            BaseVersion = billVersion,
            BillMeta = new BillMetaChangesDto { Name = "用戶2更新" }
        };

        // Act - 同時發送請求
        var task1 = service1.DeltaSyncAsync(billId, request1, Guid.NewGuid());
        var task2 = service2.DeltaSyncAsync(billId, request2, Guid.NewGuid());

        var results = await Task.WhenAll(task1, task2);

        // Assert - 至少有一個成功
        results.Should().Contain(r => r.IsSuccess);
    }

    #endregion
    #region 雙向並發編輯衝突

    /// <summary>
    /// 重現用戶報告的問題：
    /// - 瀏覽器1 新增費用 "333" → 同步成功，版本 5
    /// - 瀏覽器2 新增費用 "321" → 同步成功，版本 6
    /// - 瀏覽器1 再次同步 → 應該拿到包含 "333" 和 "321" 的合併結果
    /// </summary>
    [Fact]
    public async Task SyncBillAsync_雙向並發新增費用_版本衝突時應合併雙方變更()
    {
        // Arrange: 建立帳單和成員
        var bill = await SeedBill("共同帳單");
        var member = await SeedMember(bill, "付款者");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 瀏覽器1: 新增費用 "333"
        var browser1Request = new SyncBillRequestDto
        {
            LocalId = "browser1-local",
            RemoteId = bill.Id,
            BaseVersion = initialVersion,
            Name = "共同帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = member.Id, Name = "付款者", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>
                {
                    new()
                    {
                        LocalId = "expense-333",
                        Name = "333",
                        Amount = 333,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var result1 = await BillService.SyncBillAsync(browser1Request, null);
        result1.IsSuccess.Should().BeTrue("瀏覽器1 首次同步應成功");
        result1.Value.LatestBill.Should().BeNull("瀏覽器1 沒有衝突");
        var versionAfterBrowser1 = result1.Value.Version;

        // 瀏覽器2: 使用相同的 baseVersion（模擬尚未收到瀏覽器1的更新），新增費用 "321"
        var browser2Request = new SyncBillRequestDto
        {
            LocalId = "browser2-local",
            RemoteId = bill.Id,
            BaseVersion = initialVersion, // 過期版本，不知道瀏覽器1的變更
            Name = "共同帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = member.Id, Name = "付款者", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>
                {
                    new()
                    {
                        LocalId = "expense-321",
                        Name = "321",
                        Amount = 321,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        var result2 = await BillService.SyncBillAsync(browser2Request, null);

        // Assert: 驗證瀏覽器2的結果
        result2.IsSuccess.Should().BeTrue();

        // 關鍵斷言：瀏覽器2 應該收到版本衝突的處理結果
        // 目前的實作：只返回 LatestBill，不合併客戶端的 "321"
        // 預期行為：應該合併，返回同時包含 "333" 和 "321" 的帳單

        // 驗證資料庫中的最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        var expenseNames = finalBill!.Expenses.Select(e => e.Name).OrderBy(n => n).ToList();

        // 這個斷言目前會失敗 - 證明問題存在
        expenseNames.Should().Contain("333", "瀏覽器1 的費用應該存在");
        expenseNames.Should().Contain("321", "瀏覽器2 的費用也應該被合併（目前會失敗！）");
        expenseNames.Should().HaveCount(2, "應該有兩筆費用");
    }

    #endregion
    #region 雙向並發編輯細項紀錄

    /// <summary>
    /// 測試場景：兩個用戶同時對同一個細項紀錄新增品項
    /// - 用戶A: 新增品項 "牛排"
    /// - 用戶B: 新增品項 "沙拉"（使用過期版本）
    /// - 預期：兩個品項都應該被保存
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發新增細項品項_版本衝突時應合併雙方變更()
    {
        // Arrange: 建立帳單、成員、細項紀錄
        var bill = await SeedBill("聚餐帳單");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedItemizedExpense(bill, "細項聚餐", 1000m, member);
        ClearChangeTracker();

        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 新增品項 "牛排"
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-steak",
                        ExpenseId = expense.Id.ToString(),
                        Name = "牛排",
                        Amount = 500,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 新增品項應成功");
        resultA.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-steak");
        var versionAfterUserA = resultA.Value.NewVersion;

        // 用戶B: 使用過期版本新增品項 "沙拉"
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-salad",
                        ExpenseId = expense.Id.ToString(),
                        Name = "沙拉",
                        Amount = 200,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue("用戶B 新增品項應成功（ADD 操作會自動合併）");
        resultB.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-salad");
        resultB.Value.NewVersion.Should().BeGreaterThan(versionAfterUserA);

        // 驗證資料庫最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        var finalExpense = finalBill!.Expenses.First(e => e.Id == expense.Id);

        finalExpense.Items.Should().HaveCount(2, "兩個品項都應該存在");
        finalExpense.Items.Select(i => i.Name).Should().Contain("牛排");
        finalExpense.Items.Select(i => i.Name).Should().Contain("沙拉");
    }

    /// <summary>
    /// 測試場景：用戶A建立細項紀錄同時新增品項，用戶B後續新增品項
    /// - 用戶A: 同時建立 expense + item（同一個 delta sync 請求）
    /// - 用戶B: 對該 expense 新增另一個 item
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_用戶A建立細項紀錄與品項_用戶B新增品項_資料應正確同步()
    {
        // Arrange: 建立帳單和成員
        var bill = await SeedBill("協作帳單");
        var member = await SeedMember(bill, "共同付款者");
        ClearChangeTracker();

        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 同時建立 expense 和 item（模擬前端同時新增細項紀錄和品項）
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-local-A",
                        Name = "火鍋",
                        Amount = 2000,
                        IsItemized = true,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            },
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-local-A",
                        ExpenseId = "expense-local-A", // 使用 expense 的 localId
                        Name = "肉盤",
                        Amount = 800,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 建立細項紀錄應成功");
        resultA.Value.IdMappings!.Expenses.Should().ContainKey("expense-local-A");
        resultA.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-local-A");

        var expenseRemoteId = resultA.Value.IdMappings!.Expenses["expense-local-A"];
        var versionAfterUserA = resultA.Value.NewVersion;

        // 用戶B: 使用 remoteId 對同一個 expense 新增品項
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = versionAfterUserA,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-local-B",
                        ExpenseId = expenseRemoteId.ToString(), // 使用 expense 的 remoteId
                        Name = "菜盤",
                        Amount = 400,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue("用戶B 新增品項應成功");
        resultB.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-local-B");

        // 驗證資料庫最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        var finalExpense = finalBill!.Expenses.First(e => e.Id == expenseRemoteId);

        finalExpense.Name.Should().Be("火鍋");
        finalExpense.IsItemized.Should().BeTrue();
        finalExpense.Items.Should().HaveCount(2, "應有兩個品項");
        finalExpense.Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "肉盤", "菜盤" });
    }

    /// <summary>
    /// 測試場景：用戶A和用戶B同時建立細項紀錄（各自帶品項）
    /// 模擬兩個瀏覽器同時操作的真實場景
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向同時建立細項紀錄與品項_應全部正確合併()
    {
        // Arrange
        var bill = await SeedBill("雙向編輯帳單");
        var member = await SeedMember(bill, "成員");
        ClearChangeTracker();

        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 建立細項紀錄 "午餐" 帶品項 "漢堡"
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-A",
                        Name = "午餐",
                        Amount = 500,
                        IsItemized = true,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            },
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-A",
                        ExpenseId = "expense-A",
                        Name = "漢堡",
                        Amount = 300,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue();

        // 用戶B: 使用過期版本建立細項紀錄 "晚餐" 帶品項 "披薩"
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-B",
                        Name = "晚餐",
                        Amount = 800,
                        IsItemized = true,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            },
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-B",
                        ExpenseId = "expense-B",
                        Name = "披薩",
                        Amount = 500,
                        PaidByMemberId = member.Id.ToString(),
                        ParticipantIds = new List<string> { member.Id.ToString() }
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue("ADD 操作在版本衝突時應自動合併");
        resultB.Value.IdMappings!.Expenses.Should().ContainKey("expense-B");
        resultB.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-B");

        // 驗證最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);

        finalBill!.Expenses.Should().HaveCount(2, "應有兩個細項紀錄");
        var expenseNames = finalBill.Expenses.Select(e => e.Name).ToList();
        expenseNames.Should().Contain("午餐");
        expenseNames.Should().Contain("晚餐");

        var lunchExpense = finalBill.Expenses.First(e => e.Name == "午餐");
        lunchExpense.Items.Should().ContainSingle(i => i.Name == "漢堡");

        var dinnerExpense = finalBill.Expenses.First(e => e.Name == "晚餐");
        dinnerExpense.Items.Should().ContainSingle(i => i.Name == "披薩");
    }

    /// <summary>
    /// 輔助方法：建立細項紀錄（IsItemized = true）
    /// </summary>
    private async Task<Expense> SeedItemizedExpense(Bill bill, string name, decimal amount, Member paidBy)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            BillId = bill.Id,
            Name = name,
            Amount = amount,
            IsItemized = true,
            PaidById = paidBy.Id,
            Items = new List<ExpenseItem>(),
            Participants = new List<ExpenseParticipant>()
        };
        Db.Expenses.Add(expense);
        await Db.SaveChangesAsync();
        return expense;
    }

    #endregion

    #region Member 雙向協作測試

    /// <summary>
    /// 測試場景：兩個用戶同時新增不同成員
    /// - 用戶A: 新增成員 "Alice"
    /// - 用戶B: 新增成員 "Bob"（使用過期版本）
    /// - 預期：兩個成員都應該被保存（ADD 操作自動合併）
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發新增成員_版本衝突時應合併雙方變更()
    {
        // Arrange
        var bill = await SeedBill("協作測試帳單");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 新增成員 "Alice"
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "member-alice", Name = "Alice", DisplayOrder = 0 }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 新增成員應成功");
        resultA.Value.IdMappings!.Members.Should().ContainKey("member-alice");

        // 用戶B: 使用過期版本新增成員 "Bob"
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "member-bob", Name = "Bob", DisplayOrder = 1 }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue("用戶B 新增成員應成功（ADD 操作會自動合併）");
        resultB.Value.IdMappings!.Members.Should().ContainKey("member-bob");

        // 驗證資料庫最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Members.Should().HaveCount(2, "兩個成員都應該存在");
        finalBill.Members.Select(m => m.Name).Should().Contain("Alice");
        finalBill.Members.Select(m => m.Name).Should().Contain("Bob");
    }

    /// <summary>
    /// 測試場景：兩個用戶同時更新同一成員
    /// - 用戶A: 更新成員名稱為 "Alice Updated"
    /// - 用戶B: 更新同一成員名稱為 "Bob's Version"（使用過期版本）
    /// - 預期：用戶A 成功，用戶B 應收到衝突（server_wins）
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發更新同一成員_應偵測衝突並採用伺服器版本()
    {
        // Arrange
        var bill = await SeedBill("協作更新測試");
        var member = await SeedMember(bill, "Original Name");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 更新成員名稱
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new() { RemoteId = member.Id, Name = "Alice Updated" }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 更新應成功");

        // 用戶B: 使用過期版本更新同一成員
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new() { RemoteId = member.Id, Name = "Bob's Version" }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");
        resultB.Value.Conflicts!.First().Resolution.Should().Be("server_wins");

        // 驗證資料庫保留用戶A的變更
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Members.First().Name.Should().Be("Alice Updated", "伺服器版本（用戶A）應被保留");
    }

    /// <summary>
    /// 測試場景：用戶A刪除成員，用戶B嘗試更新同一成員
    /// - 用戶A: 刪除成員
    /// - 用戶B: 更新同一成員（使用過期版本）
    /// - 預期：用戶A 成功，用戶B 應收到衝突（entity deleted）
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_刪除與更新同一成員衝突_應回傳實體已刪除()
    {
        // Arrange
        var bill = await SeedBill("刪除衝突測試");
        var member = await SeedMember(bill, "Target Member");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;

        // 用戶A: 刪除成員
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member.Id }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除應成功");

        // 用戶B: 使用過期版本嘗試更新已刪除的成員
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new() { RemoteId = member.Id, Name = "Updated Name" }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");
        resultB.Value.Conflicts!.First().ServerValue.Should().Be("deleted", "伺服器應標記實體已刪除");

        // 驗證成員確實被刪除
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Members.Should().BeEmpty("成員應已被刪除");
    }

    #endregion

    #region Expense 雙向協作測試

    /// <summary>
    /// 測試場景：兩個用戶同時新增不同費用
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發新增費用_版本衝突時應合併雙方變更()
    {
        // Arrange
        var bill = await SeedBill("費用協作測試");
        var member = await SeedMember(bill, "付款者");
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var memberId = billAfterSetup.Members.First().Id;

        // 用戶A: 新增費用 "午餐"
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-lunch",
                        Name = "午餐",
                        Amount = 300,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 新增費用應成功");

        // 用戶B: 使用過期版本新增費用 "晚餐"
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "expense-dinner",
                        Name = "晚餐",
                        Amount = 500,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue("用戶B 新增費用應成功（ADD 操作會自動合併）");

        // 驗證資料庫最終狀態
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.Should().HaveCount(2, "兩筆費用都應該存在");
        finalBill.Expenses.Select(e => e.Name).Should().Contain("午餐");
        finalBill.Expenses.Select(e => e.Name).Should().Contain("晚餐");
    }

    /// <summary>
    /// 測試場景：兩個用戶同時更新同一費用
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發更新同一費用_應偵測衝突()
    {
        // Arrange
        var bill = await SeedBill("費用更新衝突測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedExpense(bill, "原始費用", 100, paidBy: member, participants: new[] { member });
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var expenseId = billAfterSetup.Expenses.First().Id;

        // 用戶A: 更新費用金額
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new() { RemoteId = expenseId, Amount = 200 }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 更新應成功");

        // 用戶B: 使用過期版本更新同一費用
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new() { RemoteId = expenseId, Amount = 500 }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");

        // 驗證資料庫保留用戶A的變更
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.First().Amount.Should().Be(200, "伺服器版本（用戶A）應被保留");
    }

    /// <summary>
    /// 測試場景：用戶A刪除費用，用戶B嘗試更新同一費用
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_刪除與更新同一費用衝突_應回傳實體已刪除()
    {
        // Arrange
        var bill = await SeedBill("費用刪除衝突測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedExpense(bill, "待刪除費用", 100, paidBy: member, participants: new[] { member });
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
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
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除應成功");

        // 用戶B: 使用過期版本嘗試更新已刪除的費用
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new() { RemoteId = expenseId, Amount = 999 }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");
        resultB.Value.Conflicts!.First().ServerValue.Should().Be("deleted");

        // 驗證費用確實被刪除
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.Should().BeEmpty("費用應已被刪除");
    }

    #endregion

    #region ExpenseItem 雙向協作測試

    /// <summary>
    /// 測試場景：兩個用戶同時更新同一細項
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_雙向並發更新同一細項_應偵測衝突()
    {
        // Arrange
        var bill = await SeedBill("細項更新衝突測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedItemizedExpense(bill, "細項費用", 1000, member);
        var item = await SeedExpenseItem(expense, "原始細項", 500, paidBy: member);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var itemId = billAfterSetup.Expenses.First().Items.First().Id;

        // 用戶A: 更新細項金額
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Update = new List<ExpenseItemUpdateDto>
                {
                    new() { RemoteId = itemId, Amount = 600 }
                }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 更新應成功");

        // 用戶B: 使用過期版本更新同一細項
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            ExpenseItems = new ExpenseItemChangesDto
            {
                Update = new List<ExpenseItemUpdateDto>
                {
                    new() { RemoteId = itemId, Amount = 800 }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");

        // 驗證資料庫保留用戶A的變更
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.First().Items.First().Amount.Should().Be(600, "伺服器版本（用戶A）應被保留");
    }

    /// <summary>
    /// 測試場景：用戶A刪除細項，用戶B嘗試更新同一細項
    /// </summary>
    [Fact]
    [Trait("Category", "DeltaSync")]
    [Trait("Category", "Collaboration")]
    public async Task DeltaSyncAsync_刪除與更新同一細項衝突_應回傳實體已刪除()
    {
        // Arrange
        var bill = await SeedBill("細項刪除衝突測試");
        var member = await SeedMember(bill, "付款者");
        var expense = await SeedItemizedExpense(bill, "細項費用", 1000, member);
        var item = await SeedExpenseItem(expense, "待刪除細項", 500, paidBy: member);
        ClearChangeTracker();
        var billAfterSetup = await ReloadBillFromDb(bill.Id);
        var initialVersion = billAfterSetup!.Version;
        var itemId = billAfterSetup.Expenses.First().Items.First().Id;

        // 用戶A: 刪除細項
        var userARequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Delete = new List<Guid> { itemId }
            }
        };

        var resultA = await BillService.DeltaSyncAsync(bill.Id, userARequest, null);
        resultA.IsSuccess.Should().BeTrue("用戶A 刪除應成功");

        // 用戶B: 使用過期版本嘗試更新已刪除的細項
        var userBRequest = new DeltaSyncRequest
        {
            BaseVersion = initialVersion, // 過期版本
            ExpenseItems = new ExpenseItemChangesDto
            {
                Update = new List<ExpenseItemUpdateDto>
                {
                    new() { RemoteId = itemId, Amount = 999 }
                }
            }
        };

        var resultB = await BillService.DeltaSyncAsync(bill.Id, userBRequest, null);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Conflicts.Should().NotBeNullOrEmpty("應偵測到衝突");
        resultB.Value.Conflicts!.First().ServerValue.Should().Be("deleted");

        // 驗證細項確實被刪除
        ClearChangeTracker();
        var finalBill = await ReloadBillFromDb(bill.Id);
        finalBill!.Expenses.First().Items.Should().BeEmpty("細項應已被刪除");
    }

    #endregion
}
