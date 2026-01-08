using FluentAssertions;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit.BillServiceIntegration;

/// <summary>
/// BillService 計算與其他整合測試
/// 軟刪除、Penny Allocation、MergedBill
/// </summary>
[Trait("Category", "Integration")]
public class BillServiceCalculationIntegrationTests : DatabaseTestBase
{
    private IBillService BillService => GetService<IBillService>();

    #region 軟刪除 QueryFilter 測試

    [Fact]
    [Trait("Category", "SoftDelete")]
    public async Task GetByIdAsync_軟刪除成員_不應出現在Members列表()
    {
        // Arrange: 建立帳單與成員
        var bill = await SeedBill();
        var member1 = await SeedMember(bill, "保留成員");
        var member2 = await SeedMember(bill, "刪除成員");

        // 軟刪除 member2
        var memberToDelete = await Db.Members.FindAsync(member2.Id);
        memberToDelete!.IsDeleted = true;
        memberToDelete.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        ClearChangeTracker();

        // Act
        var result = await BillService.GetByIdAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Members.Should().HaveCount(1, "軟刪除的成員應被 QueryFilter 過濾");
        result.Value.Members.Should().ContainSingle(m => m.Name == "保留成員");
        result.Value.Members.Should().NotContain(m => m.Name == "刪除成員");

        // 驗證資料庫確實存在（只是被過濾）
        var allMembers = await Db.Members.IgnoreQueryFilters()
            .Where(m => m.BillId == bill.Id).ToListAsync();
        allMembers.Should().HaveCount(2, "軟刪除的資料應仍存在於資料庫");
    }

    [Fact]
    [Trait("Category", "SoftDelete")]
    public async Task GetByIdAsync_軟刪除費用_不應出現在Expenses列表()
    {
        // Arrange
        var bill = await SeedBill();
        var member = await SeedMember(bill);
        var expense1 = await SeedExpense(bill, "保留費用", 100m, member);
        var expense2 = await SeedExpense(bill, "刪除費用", 200m, member);

        // 軟刪除 expense2
        var expenseToDelete = await Db.Expenses.FindAsync(expense2.Id);
        expenseToDelete!.IsDeleted = true;
        expenseToDelete.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        ClearChangeTracker();

        // Act
        var result = await BillService.GetByIdAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Expenses.Should().HaveCount(1, "軟刪除的費用應被 QueryFilter 過濾");
        result.Value.Expenses.Should().ContainSingle(e => e.Name == "保留費用");

        // 驗證資料庫
        var allExpenses = await Db.Expenses.IgnoreQueryFilters()
            .Where(e => e.BillId == bill.Id).ToListAsync();
        allExpenses.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "SoftDelete")]
    public async Task GetByIdAsync_軟刪除費用細項_不應出現在Items列表()
    {
        // Arrange
        var bill = await SeedBill();
        var member = await SeedMember(bill);
        var expense = await SeedExpense(bill, "細項費用", 300m, member, isItemized: true);
        var item1 = await SeedExpenseItem(expense, "保留細項", 100m, member);
        var item2 = await SeedExpenseItem(expense, "刪除細項", 200m, member);

        // 軟刪除 item2
        var itemToDelete = await Db.ExpenseItems.FindAsync(item2.Id);
        itemToDelete!.IsDeleted = true;
        itemToDelete.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        ClearChangeTracker();

        // Act
        var result = await BillService.GetByIdAsync(bill.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expenseDto = result.Value.Expenses.First();
        expenseDto.Items.Should().HaveCount(1, "軟刪除的細項應被 QueryFilter 過濾");
        expenseDto.Items.Should().ContainSingle(i => i.Name == "保留細項");

        // 驗證資料庫
        var allItems = await Db.ExpenseItems.IgnoreQueryFilters()
            .Where(i => i.ExpenseId == expense.Id).ToListAsync();
        allItems.Should().HaveCount(2);
    }

    #endregion
    #region Participant.Amount 計算測試 (Penny Allocation)

    [Fact]
    [Trait("Category", "PennyAllocation")]
    public async Task SyncBillAsync_新增費用與參與者_應計算分攤金額()
    {
        // Arrange: 100 ÷ 3 = 33.34 + 33.33 + 33.33
        var owner = await SeedUser();
        var request = new SyncBillRequestDto
        {
            LocalId = "bill-penny-1",
            Name = "Penny Allocation 測試",
            BaseVersion = 0,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", Name = "Bob", DisplayOrder = 1 },
                    new() { LocalId = "m3", Name = "Charlie", DisplayOrder = 2 }
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
                        Amount = 100m,
                        ServiceFeePercent = 0,
                        IsItemized = false,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1", "m2", "m3" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await BillService.SyncBillAsync(request, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ClearChangeTracker();

        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        var participants = bill!.Expenses.First().Participants
            .OrderByDescending(p => p.Amount).ToList();

        // Penny Allocation: 100 ÷ 3 = 33.33... 餘 1 分
        // 前 1 人得 33.34，後 2 人得 33.33
        participants.Sum(p => p.Amount).Should().Be(100m, "總和應精確等於原金額");
        participants[0].Amount.Should().Be(33.34m, "第一人應多得 1 分");
        participants[1].Amount.Should().Be(33.33m);
        participants[2].Amount.Should().Be(33.33m);
    }

    [Fact]
    [Trait("Category", "PennyAllocation")]
    public async Task SyncBillAsync_含服務費_應計算含服務費分攤金額()
    {
        // Arrange: 100 + 10% = 110, 110 ÷ 2 = 55 + 55
        var owner = await SeedUser();
        var request = new SyncBillRequestDto
        {
            LocalId = "bill-penny-2",
            Name = "服務費測試",
            BaseVersion = 0,
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
                        Name = "餐廳",
                        Amount = 100m,
                        ServiceFeePercent = 10m, // 10% 服務費
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
        ClearChangeTracker();

        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        var participants = bill!.Expenses.First().Participants.ToList();

        // 100 * 1.10 = 110, 110 ÷ 2 = 55 + 55
        participants.Sum(p => p.Amount).Should().Be(110m, "總和應等於含服務費金額");
        participants.Should().AllSatisfy(p => p.Amount.Should().Be(55m));
    }

    [Fact]
    [Trait("Category", "PennyAllocation")]
    public async Task SyncBillAsync_費用細項_應計算細項分攤金額()
    {
        // Arrange: Item 70 ÷ 3 = 23.34 + 23.33 + 23.33
        var owner = await SeedUser();
        var request = new SyncBillRequestDto
        {
            LocalId = "bill-penny-3",
            Name = "細項測試",
            BaseVersion = 0,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", Name = "Bob", DisplayOrder = 1 },
                    new() { LocalId = "m3", Name = "Charlie", DisplayOrder = 2 }
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
                        Name = "細項費用",
                        Amount = 100m,
                        ServiceFeePercent = 0,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>
                            {
                                new()
                                {
                                    LocalId = "i1",
                                    Name = "披薩",
                                    Amount = 70m,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1", "m2", "m3" }
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
        var result = await BillService.SyncBillAsync(request, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ClearChangeTracker();

        var bill = await ReloadBillFromDb(result.Value.RemoteId);
        var itemParticipants = bill!.Expenses.First().Items.First().Participants
            .OrderByDescending(p => p.Amount).ToList();

        // 70 ÷ 3 = 23.33... 餘 1 分
        itemParticipants.Sum(p => p.Amount).Should().Be(70m, "總和應精確等於細項金額");
        itemParticipants[0].Amount.Should().Be(23.34m, "第一人應多得 1 分");
        itemParticipants[1].Amount.Should().Be(23.33m);
        itemParticipants[2].Amount.Should().Be(23.33m);
    }

    [Fact]
    [Trait("Category", "PennyAllocation")]
    public async Task DeltaSyncAsync_新增費用_應計算分攤金額()
    {
        // Arrange
        var bill = await SeedCompleteBill(memberCount: 4, expenseCount: 0);
        var memberIds = bill.Members.Select(m => m.Id.ToString()).ToList();

        // 100 ÷ 4 = 25 + 25 + 25 + 25 (整除)
        var request = new DeltaSyncRequest
        {
            BaseVersion = bill.Version,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "new-exp-1",
                        Name = "四人均分",
                        Amount = 100m,
                        ServiceFeePercent = 0,
                        PaidByMemberId = memberIds[0],
                        ParticipantIds = memberIds
                    }
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ClearChangeTracker();

        var updatedBill = await ReloadBillFromDb(bill.Id);
        var newExpense = updatedBill!.Expenses.First(e => e.Name == "四人均分");
        var participants = newExpense.Participants.ToList();

        participants.Sum(p => p.Amount).Should().Be(100m);
        participants.Should().AllSatisfy(p => p.Amount.Should().Be(25m), "整除時應平均分配");
    }

    [Fact]
    [Trait("Category", "PennyAllocation")]
    public async Task DeltaSyncAsync_更新參與者_應重新計算分攤金額()
    {
        // Arrange: 先建立帳單與費用
        var owner = await SeedUser();
        var createRequest = new SyncBillRequestDto
        {
            LocalId = "bill-update-amount",
            Name = "更新測試",
            BaseVersion = 0,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", Name = "Bob", DisplayOrder = 1 },
                    new() { LocalId = "m3", Name = "Charlie", DisplayOrder = 2 }
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
                        Name = "費用",
                        Amount = 100m,
                        ServiceFeePercent = 0,
                        IsItemized = false,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1", "m2", "m3" } // 3 人
                    }
                },
                DeletedIds = new List<string>()
            }
        };
        var createResult = await BillService.SyncBillAsync(createRequest, owner.Id);
        ClearChangeTracker();

        var bill = await ReloadBillFromDb(createResult.Value.RemoteId);
        var expenseId = bill!.Expenses.First().Id;
        var memberIds = bill.Members.Select(m => m.Id.ToString()).ToList();

        // Act: 更新為只有 2 人參與
        var updateRequest = new DeltaSyncRequest
        {
            BaseVersion = bill.Version,
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        ParticipantIds = new List<string> { memberIds[0], memberIds[1] } // 只有 2 人
                    }
                }
            }
        };
        var updateResult = await BillService.DeltaSyncAsync(bill.Id, updateRequest, owner.Id);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        ClearChangeTracker();

        var updatedBill = await ReloadBillFromDb(bill.Id);
        var participants = updatedBill!.Expenses.First().Participants.ToList();

        // 100 ÷ 2 = 50 + 50
        participants.Should().HaveCount(2);
        participants.Sum(p => p.Amount).Should().Be(100m);
        participants.Should().AllSatisfy(p => p.Amount.Should().Be(50m));
    }

    #endregion
    #region MergedBill 完整性測試

    [Fact]
    [Trait("Category", "Conflict")]
    public async Task DeltaSyncAsync_版本衝突_MergedBill應包含完整資料()
    {
        // Arrange
        var bill = await SeedCompleteBill(memberCount: 2, expenseCount: 1);
        var member = bill.Members.First();

        // 模擬版本衝突：使用過期的 BaseVersion
        var request = new DeltaSyncRequest
        {
            BaseVersion = bill.Version - 1, // 過期版本
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new() { RemoteId = member.Id, Name = "新名字" }
                }
            }
        };

        // Act
        var result = await BillService.DeltaSyncAsync(bill.Id, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue(); // 衝突但仍成功（server_wins）
        result.Value.MergedBill.Should().NotBeNull("版本衝突時應回傳 MergedBill");

        var mergedBill = result.Value.MergedBill!;
        mergedBill.Members.Should().NotBeEmpty("MergedBill 應包含成員");
        mergedBill.Expenses.Should().NotBeEmpty("MergedBill 應包含費用");
        mergedBill.Version.Should().BeGreaterThan(bill.Version, "版本應已遞增");
    }

    #endregion
}
