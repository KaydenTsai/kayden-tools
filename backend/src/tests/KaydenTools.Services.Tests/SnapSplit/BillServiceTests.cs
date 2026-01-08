using FluentAssertions;
using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// 帳單服務 Delta Sync 邏輯測試
/// </summary>
public class BillServiceTests
{
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut; // System Under Test
    private readonly IUnitOfWork _unitOfWork;

    public BillServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();

        // 模擬事務執行：直接執行傳入的 Action
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<DeltaSyncResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.Arg<Func<Task<Result<DeltaSyncResponse>>>>();
                return await action();
            });

        _sut = new BillService(_unitOfWork, _notificationService);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_新增成員_應成功建立成員並回傳ID映射()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var localId = "temp-member-1";
        var memberName = "新成員";

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = localId, Name = memberName }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.NewVersion.Should().Be(2);

        // 驗證成員是否已加入帳單
        existingBill.Members.Should().HaveCount(1);
        var addedMember = existingBill.Members.First();
        addedMember.Name.Should().Be(memberName);

        // 驗證 ID 映射是否正確
        result.Value.IdMappings.Should().NotBeNull();
        result.Value.IdMappings!.Members.Should().ContainKey(localId);
        result.Value.IdMappings.Members![localId].Should().Be(addedMember.Id);

        // 驗證是否發送通知
        await _notificationService.Received(1).NotifyBillUpdatedAsync(billId, 2, userId);

        // 驗證是否呼叫存檔
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_版本衝突_應回傳衝突資訊與伺服器版本()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 5, // 伺服器版本較新
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "伺服器名稱", DisplayOrder = 1 }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 4, // 用戶端版本過期
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new()
                    {
                        RemoteId = memberId,
                        Name = "用戶端名稱"
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();

        var conflict = result.Value.Conflicts!.First();
        conflict.Type.Should().Be("member");
        conflict.EntityId.Should().Be(memberId.ToString());
        conflict.Field.Should().Be("name");
        conflict.Resolution.Should().Be("server_wins");

        // 驗證資料未被修改
        existingBill.Members.First().Name.Should().Be("伺服器名稱");
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_新增費用_應正確解析ID映射並建立費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "成員A" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "成員B" };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2 },
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        // 情境：同時新增成員並新增一筆由該成員支付的費用
        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "new-mem", Name = "新朋友" }
                }
            },
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = "new-exp",
                        Name = "午餐",
                        Amount = 100,
                        PaidByMemberId = "new-mem", // 應解析為新建立的成員 ID
                        ParticipantIds = new List<string> { "new-mem", member2.Id.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();

        var newMember = existingBill.Members.First(m => m.Name == "新朋友");
        var newExpense = existingBill.Expenses.First();

        newExpense.PaidById.Should().Be(newMember.Id); // 驗證 LocalId 解析
        newExpense.Participants.Select(p => p.MemberId).Should().Contain(new[] { newMember.Id, member2.Id });
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新費用_應正確修改費用欄位()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            Name = "舊名稱",
            Amount = 50
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        Name = "新名稱",
                        Amount = 100
                    }
                }
            }
        };

        // Act
        await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        expense.Name.Should().Be("新名稱");
        expense.Amount.Should().Be(100);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除費用_應將費用從帳單中移除()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var expense = new Expense { Id = expenseId };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Expenses = new ExpenseChangesDto
            {
                Delete = new List<Guid> { expenseId }
            }
        };

        // Act
        await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        existingBill.Expenses.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_標記結清_應新增結清轉帳記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid() };
        var member2 = new Member { Id = Guid.NewGuid() };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2 },
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Settlements = new SettlementChangesDto
            {
                Mark = new List<DeltaSettlementDto>
                {
                    new()
                    {
                        FromMemberId = member1.Id.ToString(),
                        ToMemberId = member2.Id.ToString(),
                        Amount = 50
                    }
                }
            }
        };

        // Act
        await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        existingBill.SettledTransfers.Should().HaveCount(1);
        existingBill.SettledTransfers.First().Amount.Should().Be(50);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_取消結清_應移除結清轉帳記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid() };
        var member2 = new Member { Id = Guid.NewGuid() };

        var transfer = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id,
            ToMemberId = member2.Id
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            SettledTransfers = new List<SettledTransfer> { transfer }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Settlements = new SettlementChangesDto
            {
                Unmark = new List<DeltaSettlementDto>
                {
                    new()
                    {
                        FromMemberId = member1.Id.ToString(),
                        ToMemberId = member2.Id.ToString()
                    }
                }
            }
        };

        // Act
        await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        existingBill.SettledTransfers.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.DeltaSyncAsync(billId, new DeltaSyncRequest(), Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_資料庫並發異常_應捕獲例外並回傳最新合併帳單()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingBill = new Bill { Id = billId, Version = 1, Name = "原始名稱" };
        var updatedBill = new Bill { Id = billId, Version = 2, Name = "他人修改後的名稱" };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill, updatedBill);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new DbUpdateConcurrencyException());

        var request = new DeltaSyncRequest { BaseVersion = 1 };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.NewVersion.Should().Be(2);
        result.Value.MergedBill.Should().NotBeNull();
        result.Value.MergedBill!.Name.Should().Be("他人修改後的名稱");

        _unitOfWork.Received(1).ClearChangeTracker();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_新增已認領成員_應保留認領資訊()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var linkedUserId = Guid.NewGuid(); // 認領的用戶 ID
        var localId = "temp-claimed-member";
        var memberName = "已認領成員";
        var claimedAt = new DateTime(2026, 1, 8, 12, 0, 0, DateTimeKind.Utc);

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new()
                    {
                        LocalId = localId,
                        Name = memberName,
                        LinkedUserId = linkedUserId,
                        ClaimedAt = claimedAt
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();

        // 驗證成員是否已加入帳單
        existingBill.Members.Should().HaveCount(1);
        var addedMember = existingBill.Members.First();
        addedMember.Name.Should().Be(memberName);

        // 驗證認領資訊是否正確保留
        addedMember.LinkedUserId.Should().Be(linkedUserId);
        addedMember.ClaimedAt.Should().Be(claimedAt);

        // 驗證 ID 映射是否正確
        result.Value.IdMappings.Should().NotBeNull();
        result.Value.IdMappings!.Members.Should().ContainKey(localId);
    }

    #region 成員操作補充測試

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新成員無衝突_應成功更新成員資料()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "舊名稱", DisplayOrder = 0 }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1, // 版本一致
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new()
                    {
                        RemoteId = memberId,
                        Name = "新名稱",
                        DisplayOrder = 5
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.Conflicts.Should().BeNullOrEmpty();

        var member = existingBill.Members.First();
        member.Name.Should().Be("新名稱");
        member.DisplayOrder.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新不存在的成員_應回傳衝突()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var nonExistentMemberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new()
                    {
                        RemoteId = nonExistentMemberId,
                        Name = "不存在"
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();
        result.Value.Conflicts!.First().Type.Should().Be("member");
        result.Value.Conflicts!.First().ServerValue.Should().Be("deleted");
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員無衝突_應成功移除成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "待刪除" }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { memberId }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員有衝突_應回傳需手動處理()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 5, // 伺服器版本較新
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "已被修改的成員" }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 3, // 過期版本
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { memberId }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();
        result.Value.Conflicts!.First().Resolution.Should().Be("manual_required");

        // 成員應該還在
        existingBill.Members.Should().HaveCount(1);
    }

    #endregion

    #region 費用細項操作測試

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_新增費用細項_應正確建立細項與參與者()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Alice" }
            },
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "聚餐",
                    IsItemized = true,
                    Items = new List<ExpenseItem>(),
                    Participants = new List<ExpenseParticipant>()
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-1",
                        ExpenseId = expenseId.ToString(),
                        Name = "牛排",
                        Amount = 500,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-1");

        var expense = existingBill.Expenses.First();
        expense.Items.Should().HaveCount(1);
        expense.Items.First().Name.Should().Be("牛排");
        expense.Items.First().Amount.Should().Be(500);
        expense.Items.First().PaidById.Should().Be(memberId);
        expense.Items.First().Participants.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_同時新增費用與細項_應透過LocalId正確關聯()
    {
        // Arrange - 這個測試驗證當 expense 和 items 在同一個 delta sync 請求中新增時，
        // 後端能正確使用 expenseIdMappings 解析 item 的 ExpenseId (localId)
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expenseLocalId = "expense-local-123"; // 前端產生的本地 ID
        var itemLocalId = "item-local-456";

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Alice" }
            },
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        // 同時新增 expense 和 item，item 使用 expense 的 localId 作為 ExpenseId
        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = expenseLocalId,
                        Name = "細項聚餐",
                        Amount = 1000,
                        IsItemized = true,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            },
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = itemLocalId,
                        ExpenseId = expenseLocalId, // 使用 expense 的 localId，不是 remoteId
                        Name = "牛排",
                        Amount = 500,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 驗證 ID 映射
        result.Value.IdMappings!.Expenses.Should().ContainKey(expenseLocalId);
        result.Value.IdMappings!.ExpenseItems.Should().ContainKey(itemLocalId);

        // 驗證 expense 和 item 都正確建立
        existingBill.Expenses.Should().HaveCount(1);
        var expense = existingBill.Expenses.First();
        expense.Name.Should().Be("細項聚餐");
        expense.IsItemized.Should().BeTrue();

        // 重要：驗證 item 正確關聯到 expense
        expense.Items.Should().HaveCount(1);
        var item = expense.Items.First();
        item.Name.Should().Be("牛排");
        item.Amount.Should().Be(500);
        item.ExpenseId.Should().Be(expense.Id); // item 應該關聯到正確的 expense
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_同時新增費用與多個細項_應全部正確建立()
    {
        // Arrange - 驗證多個 items 都能正確關聯
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expenseLocalId = "expense-local-789";

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Bob" }
            },
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new()
                    {
                        LocalId = expenseLocalId,
                        Name = "火鍋",
                        Amount = 2000,
                        IsItemized = true,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            },
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "item-1",
                        ExpenseId = expenseLocalId,
                        Name = "肉盤",
                        Amount = 800,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    },
                    new()
                    {
                        LocalId = "item-2",
                        ExpenseId = expenseLocalId,
                        Name = "菜盤",
                        Amount = 400,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    },
                    new()
                    {
                        LocalId = "item-3",
                        ExpenseId = expenseLocalId,
                        Name = "飲料",
                        Amount = 200,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings!.ExpenseItems.Should().HaveCount(3);
        result.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-1");
        result.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-2");
        result.Value.IdMappings!.ExpenseItems.Should().ContainKey("item-3");

        var expense = existingBill.Expenses.First();
        expense.Items.Should().HaveCount(3);
        expense.Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "肉盤", "菜盤", "飲料" });
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_細項使用無效ExpenseId_應忽略該細項()
    {
        // Arrange - 驗證當 ExpenseId 無法解析時，細項會被安全跳過
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Charlie" }
            },
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = new List<ExpenseItemAddDto>
                {
                    new()
                    {
                        LocalId = "orphan-item",
                        ExpenseId = "invalid-not-a-guid", // 無效的 ID
                        Name = "孤兒細項",
                        Amount = 100,
                        PaidByMemberId = memberId.ToString(),
                        ParticipantIds = new List<string> { memberId.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert - 應該成功但沒有建立任何細項
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings!.ExpenseItems.Should().BeEmpty();
        existingBill.Expenses.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新費用細項_應正確修改細項資料()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Alice" }
            },
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "聚餐",
                    IsItemized = true,
                    Items = new List<ExpenseItem>
                    {
                        new()
                        {
                            Id = itemId,
                            ExpenseId = expenseId,
                            Name = "舊名稱",
                            Amount = 100,
                            Participants = new List<ExpenseItemParticipant>()
                        }
                    },
                    Participants = new List<ExpenseParticipant>()
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Update = new List<ExpenseItemUpdateDto>
                {
                    new()
                    {
                        RemoteId = itemId,
                        Name = "新名稱",
                        Amount = 200
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();

        var item = existingBill.Expenses.First().Items.First();
        item.Name.Should().Be("新名稱");
        item.Amount.Should().Be(200);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除費用細項_應正確移除細項()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>(),
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "聚餐",
                    IsItemized = true,
                    Items = new List<ExpenseItem>
                    {
                        new()
                        {
                            Id = itemId,
                            ExpenseId = expenseId,
                            Name = "待刪除",
                            Participants = new List<ExpenseItemParticipant>()
                        }
                    },
                    Participants = new List<ExpenseParticipant>()
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Delete = new List<Guid> { itemId }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBill.Expenses.First().Items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新不存在的費用細項_應回傳衝突()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var nonExistentItemId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>(),
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Items = new List<ExpenseItem>(),
                    Participants = new List<ExpenseParticipant>()
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            ExpenseItems = new ExpenseItemChangesDto
            {
                Update = new List<ExpenseItemUpdateDto>
                {
                    new()
                    {
                        RemoteId = nonExistentItemId,
                        Name = "不存在"
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();
        result.Value.Conflicts!.First().Type.Should().Be("expenseItem");
    }

    #endregion

    #region 費用參與者 Diff-based 更新測試

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新費用參與者_應正確執行差異更新()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member>
            {
                new() { Id = member1, Name = "Alice" },
                new() { Id = member2, Name = "Bob" },
                new() { Id = member3, Name = "Charlie" }
            },
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "費用",
                    Amount = 300,
                    Participants = new List<ExpenseParticipant>
                    {
                        new() { ExpenseId = expenseId, MemberId = member1 },
                        new() { ExpenseId = expenseId, MemberId = member2 }
                    },
                    Items = new List<ExpenseItem>()
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        // 移除 member1，保留 member2，新增 member3
                        ParticipantIds = new List<string> { member2.ToString(), member3.ToString() }
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();

        var expense = existingBill.Expenses.First();
        expense.Participants.Should().HaveCount(2);
        expense.Participants.Select(p => p.MemberId).Should().Contain(new[] { member2, member3 });
        expense.Participants.Select(p => p.MemberId).Should().NotContain(member1);
    }

    #endregion

    #region 版本衝突下的帳單元資料更新

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_更新費用時版本衝突_應回傳衝突並附帶合併帳單()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 5, // 伺服器版本較新
            Name = "伺服器帳單",
            Members = new List<Member>(),
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "伺服器費用名稱",
                    Amount = 999,
                    Participants = new List<ExpenseParticipant>(),
                    Items = new List<ExpenseItem>()
                }
            },
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 3, // 過期版本
            Expenses = new ExpenseChangesDto
            {
                Update = new List<ExpenseUpdateDto>
                {
                    new()
                    {
                        RemoteId = expenseId,
                        Name = "用戶端費用名稱",
                        Amount = 100
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();
        result.Value.MergedBill.Should().NotBeNull();

        // 費用不應該被修改
        existingBill.Expenses.First().Name.Should().Be("伺服器費用名稱");
        existingBill.Expenses.First().Amount.Should().Be(999);
    }

    #endregion

    #region 成員刪除與 SettledTransfer 級聯刪除測試

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員_該成員為結清付款方_應同時移除結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "付款方" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "收款方" };

        var transfer = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id, // member1 是付款方
            ToMemberId = member2.Id,
            Amount = 100
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2 },
            SettledTransfers = new List<SettledTransfer> { transfer }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member1.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().HaveCount(1);
        existingBill.Members.First().Id.Should().Be(member2.Id);
        existingBill.SettledTransfers.Should().BeEmpty();

        // 驗證 Repository.Remove 被呼叫
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員_該成員為結清收款方_應同時移除結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "付款方" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "收款方" };

        var transfer = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id,
            ToMemberId = member2.Id, // member2 是收款方
            Amount = 100
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2 },
            SettledTransfers = new List<SettledTransfer> { transfer }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member2.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().HaveCount(1);
        existingBill.Members.First().Id.Should().Be(member1.Id);
        existingBill.SettledTransfers.Should().BeEmpty();

        // 驗證 Repository.Remove 被呼叫
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員_該成員同時為付款方和收款方_應移除所有相關結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "成員A" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "成員B" };
        var member3 = new Member { Id = Guid.NewGuid(), Name = "成員C" };

        // member1 既是付款方也是收款方（在不同的結清記錄中）
        var transfer1 = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id, // member1 付給 member2
            ToMemberId = member2.Id,
            Amount = 50
        };
        var transfer2 = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member3.Id,
            ToMemberId = member1.Id, // member3 付給 member1
            Amount = 30
        };
        // 這筆不涉及 member1，應該保留
        var transfer3 = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member2.Id,
            ToMemberId = member3.Id,
            Amount = 20
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2, member3 },
            SettledTransfers = new List<SettledTransfer> { transfer1, transfer2, transfer3 }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member1.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().HaveCount(2);
        existingBill.Members.Select(m => m.Id).Should().Contain(new[] { member2.Id, member3.Id });

        // transfer3 應該保留（不涉及 member1）
        existingBill.SettledTransfers.Should().HaveCount(1);
        existingBill.SettledTransfers.First().Should().Be(transfer3);

        // 驗證 Repository.Remove 被呼叫兩次（transfer1 和 transfer2）
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer1);
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer2);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除成員_無相關結清記錄_應正常刪除成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "待刪除" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "保留" };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2 },
            SettledTransfers = new List<SettledTransfer>() // 無結清記錄
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member1.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().HaveCount(1);
        existingBill.Members.First().Id.Should().Be(member2.Id);

        // 驗證 Repository.Remove 未被呼叫
        _unitOfWork.SettledTransfers.DidNotReceive().Remove(Arg.Any<SettledTransfer>());
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_刪除多個成員_應移除所有相關結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "成員A" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "成員B" };
        var member3 = new Member { Id = Guid.NewGuid(), Name = "成員C" };

        var transfer1 = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id,
            ToMemberId = member2.Id,
            Amount = 50
        };
        var transfer2 = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member2.Id,
            ToMemberId = member3.Id,
            Amount = 30
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            Members = new List<Member> { member1, member2, member3 },
            SettledTransfers = new List<SettledTransfer> { transfer1, transfer2 }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        // 刪除 member1 和 member2
        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member1.Id, member2.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        existingBill.Members.Should().HaveCount(1);
        existingBill.Members.First().Id.Should().Be(member3.Id);
        existingBill.SettledTransfers.Should().BeEmpty();

        // 驗證兩筆 transfer 都被移除
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer1);
        _unitOfWork.SettledTransfers.Received(1).Remove(transfer2);
    }

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_版本衝突時刪除成員_不應移除成員和結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = new Member { Id = Guid.NewGuid(), Name = "成員A" };
        var member2 = new Member { Id = Guid.NewGuid(), Name = "成員B" };

        var transfer = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = member1.Id,
            ToMemberId = member2.Id,
            Amount = 100
        };

        var existingBill = new Bill
        {
            Id = billId,
            Version = 5, // 伺服器版本較新
            Members = new List<Member> { member1, member2 },
            SettledTransfers = new List<SettledTransfer> { transfer }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 3, // 過期版本
            Members = new MemberChangesDto
            {
                Delete = new List<Guid> { member1.Id }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Conflicts.Should().NotBeNullOrEmpty();
        result.Value.Conflicts!.First().Resolution.Should().Be("manual_required");

        // 成員和結清記錄都應該保留
        existingBill.Members.Should().HaveCount(2);
        existingBill.SettledTransfers.Should().HaveCount(1);

        // 驗證 Repository.Remove 未被呼叫
        _unitOfWork.SettledTransfers.DidNotReceive().Remove(Arg.Any<SettledTransfer>());
    }

    #endregion

    #region 並發異常帳單不存在

    [Fact]
    [Trait("Category", "DeltaSync")]
    public async Task DeltaSyncAsync_資料庫並發異常_帳單不存在_應回傳失敗()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<DeltaSyncResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.Arg<Func<Task<Result<DeltaSyncResponse>>>>();
                try
                {
                    return await action();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw;
                }
            });

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new DbUpdateConcurrencyException());

        var request = new DeltaSyncRequest { BaseVersion = 1 };

        // Act - 先檢查帳單不存在的情況
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    #endregion
}
