using System.Text.Json;
using FluentAssertions;
using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// OperationService 單元測試
/// 測試 CRDT 操作處理與版本衝突檢測
/// </summary>
public class OperationServiceTests
{
    private readonly OperationService _sut;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeService _dateTimeService;

    public OperationServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _dateTimeService = Substitute.For<IDateTimeService>();
        _dateTimeService.UtcNow.Returns(DateTime.UtcNow);

        // 模擬 ExecuteInTransactionAsync 直接執行傳入的函數
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<OperationDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var func = callInfo.Arg<Func<Task<Result<OperationDto>>>>();
                return await func();
            });

        _sut = new OperationService(_unitOfWork, _dateTimeService);
    }

    #region ProcessOperationAsync - 基本測試

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_帳單不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var request = CreateRequest(billId, "MEMBER_ADD", null, new { name = "新成員" }, 1);

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.ProcessOperationAsync(request, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_版本衝突_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Version = 5, // 伺服器版本
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // BaseVersion = 3，但伺服器已經是 5
        var request = CreateRequest(billId, "MEMBER_ADD", null, new { name = "新成員" }, 3);

        _unitOfWork.Operations.FindAsync(
            Arg.Any<System.Linq.Expressions.Expression<Func<Operation, bool>>>(),
            Arg.Any<Func<IQueryable<Operation>, IOrderedQueryable<Operation>>>(),
            Arg.Any<Func<IQueryable<Operation>, IQueryable<Operation>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<Operation>());

        // Act
        var result = await _sut.ProcessOperationAsync(request, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Conflict);
        result.Error.Message.Should().Contain("Version mismatch");
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_版本匹配_應成功處理()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Version = 1,
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_ADD", null, new { name = "新成員" }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(2);
        result.Value.OpType.Should().Be("MEMBER_ADD");
        result.Value.CreatedByUserId.Should().Be(userId);

        // 帳單版本應更新
        bill.Version.Should().Be(2);

        await _unitOfWork.Operations.Received(1).AddAsync(Arg.Any<Operation>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region ProcessOperationAsync - Member 操作

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_ADD_應新增成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        var memberId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_ADD", memberId, new { name = "新成員", displayOrder = 0 }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Members.Should().HaveCount(1);
        bill.Members.First().Name.Should().Be("新成員");
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_UPDATE_應更新成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Members.Add(new Member { Id = memberId, BillId = billId, Name = "原名稱" });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_UPDATE", memberId, new { name = "新名稱" }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Members.First().Name.Should().Be("新名稱");
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_REMOVE_應移除成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Members.Add(new Member { Id = memberId, BillId = billId, Name = "待刪除成員" });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_REMOVE", memberId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Members.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_CLAIM_應認領成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Members.Add(new Member { Id = memberId, BillId = billId, Name = "原名稱" });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_CLAIM", memberId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var member = bill.Members.First();
        member.LinkedUserId.Should().Be(userId);
        member.OriginalName.Should().Be("原名稱");
        member.ClaimedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_UNCLAIM_應取消認領()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Members.Add(new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "認領後名稱",
            OriginalName = "原始名稱",
            LinkedUserId = userId,
            ClaimedAt = DateTime.UtcNow
        });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "MEMBER_UNCLAIM", memberId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var member = bill.Members.First();
        member.Name.Should().Be("原始名稱");
        member.OriginalName.Should().BeNull();
        member.LinkedUserId.Should().BeNull();
        member.ClaimedAt.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_MEMBER_REORDER_應重新排序成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var member3Id = Guid.NewGuid();

        var bill = CreateBillWithVersion(billId, 1);
        bill.Members.Add(new Member { Id = member1Id, BillId = billId, Name = "成員1", DisplayOrder = 0 });
        bill.Members.Add(new Member { Id = member2Id, BillId = billId, Name = "成員2", DisplayOrder = 1 });
        bill.Members.Add(new Member { Id = member3Id, BillId = billId, Name = "成員3", DisplayOrder = 2 });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // 新順序：3, 1, 2
        var request = CreateRequest(billId, "MEMBER_REORDER", null,
            new { order = new[] { member3Id, member1Id, member2Id } }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Members.First(m => m.Id == member3Id).DisplayOrder.Should().Be(0);
        bill.Members.First(m => m.Id == member1Id).DisplayOrder.Should().Be(1);
        bill.Members.First(m => m.Id == member2Id).DisplayOrder.Should().Be(2);
    }

    #endregion

    #region ProcessOperationAsync - Expense 操作

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_EXPENSE_ADD_應新增費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "EXPENSE_ADD", expenseId,
            new { name = "晚餐", amount = 1000m, serviceFeePercent = 10m, paidById = payerId }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Expenses.Should().HaveCount(1);
        var expense = bill.Expenses.First();
        expense.Name.Should().Be("晚餐");
        expense.Amount.Should().Be(1000m);
        expense.ServiceFeePercent.Should().Be(10m);
        expense.PaidById.Should().Be(payerId);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_EXPENSE_UPDATE_應更新費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Expenses.Add(new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "原名稱",
            Amount = 500m
        });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "EXPENSE_UPDATE", expenseId,
            new { name = "新名稱", amount = 1000m }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expense = bill.Expenses.First();
        expense.Name.Should().Be("新名稱");
        expense.Amount.Should().Be(1000m);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_EXPENSE_DELETE_應刪除費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Expenses.Add(new Expense { Id = expenseId, BillId = billId, Name = "待刪除" });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "EXPENSE_DELETE", expenseId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Expenses.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_EXPENSE_SET_PARTICIPANTS_應設定參與者()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Expenses.Add(new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "晚餐",
            Participants = new List<ExpenseParticipant>()
        });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "EXPENSE_SET_PARTICIPANTS", expenseId,
            new { participantIds = new[] { member1Id, member2Id } }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expense = bill.Expenses.First();
        expense.Participants.Should().HaveCount(2);
        expense.Participants.Select(p => p.MemberId).Should().Contain(new[] { member1Id, member2Id });
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_EXPENSE_TOGGLE_ITEMIZED_應切換細項模式()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Expenses.Add(new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "晚餐",
            IsItemized = false
        });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "EXPENSE_TOGGLE_ITEMIZED", expenseId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Expenses.First().IsItemized.Should().BeTrue();
    }

    #endregion

    #region ProcessOperationAsync - Item 操作

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_ITEM_ADD_應新增細項()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Expenses.Add(new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "火鍋",
            IsItemized = true,
            Items = new List<ExpenseItem>()
        });

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "ITEM_ADD", itemId,
            new { expenseId = expenseId, name = "肉盤", amount = 500m, paidById = payerId }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expense = bill.Expenses.First();
        expense.Items.Should().HaveCount(1);
        var item = expense.Items.First();
        item.Name.Should().Be("肉盤");
        item.Amount.Should().Be(500m);
        item.PaidById.Should().Be(payerId);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_ITEM_UPDATE_應更新細項()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "火鍋",
            IsItemized = true,
            Items = new List<ExpenseItem>
            {
                new() { Id = itemId, ExpenseId = expenseId, Name = "原名稱", Amount = 300m }
            }
        };
        bill.Expenses.Add(expense);

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "ITEM_UPDATE", itemId,
            new { name = "新名稱", amount = 500m }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = bill.Expenses.First().Items.First();
        item.Name.Should().Be("新名稱");
        item.Amount.Should().Be(500m);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_ITEM_DELETE_應刪除細項()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "火鍋",
            IsItemized = true,
            Items = new List<ExpenseItem>
            {
                new() { Id = itemId, ExpenseId = expenseId, Name = "待刪除", Amount = 300m }
            }
        };
        bill.Expenses.Add(expense);

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "ITEM_DELETE", itemId, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Expenses.First().Items.Should().BeEmpty();
    }

    #endregion

    #region ProcessOperationAsync - Settlement 操作

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_SETTLEMENT_MARK_應標記結清()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.SettledTransfers = new List<SettledTransfer>();

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "SETTLEMENT_MARK", null,
            new { fromMemberId = fromMemberId, toMemberId = toMemberId, amount = 500m }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.SettledTransfers.Should().HaveCount(1);
        var transfer = bill.SettledTransfers.First();
        transfer.FromMemberId.Should().Be(fromMemberId);
        transfer.ToMemberId.Should().Be(toMemberId);
        transfer.Amount.Should().Be(500m);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_SETTLEMENT_UNMARK_應取消結清()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.SettledTransfers = new List<SettledTransfer>
        {
            new()
            {
                BillId = billId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                Amount = 500m
            }
        };

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "SETTLEMENT_UNMARK", null,
            new { fromMemberId = fromMemberId, toMemberId = toMemberId }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.SettledTransfers.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_SETTLEMENT_CLEAR_ALL_應清除所有結清()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.SettledTransfers = new List<SettledTransfer>
        {
            new() { BillId = billId, FromMemberId = Guid.NewGuid(), ToMemberId = Guid.NewGuid(), Amount = 100m },
            new() { BillId = billId, FromMemberId = Guid.NewGuid(), ToMemberId = Guid.NewGuid(), Amount = 200m }
        };

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "SETTLEMENT_CLEAR_ALL", null, new { }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.SettledTransfers.Should().BeEmpty();
    }

    #endregion

    #region ProcessOperationAsync - Bill 操作

    [Fact]
    [Trait("Category", "Operation")]
    public async Task ProcessOperationAsync_BILL_UPDATE_META_應更新帳單名稱()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = CreateBillWithVersion(billId, 1);
        bill.Name = "原名稱";

        _unitOfWork.Bills.GetByIdWithLockAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var request = CreateRequest(billId, "BILL_UPDATE_META", null, new { name = "新帳單名稱" }, 1);

        // Act
        var result = await _sut.ProcessOperationAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Name.Should().Be("新帳單名稱");
    }

    #endregion

    #region GetOperationsAsync 測試

    [Fact]
    [Trait("Category", "Operation")]
    public async Task GetOperationsAsync_應回傳指定版本之後的操作()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var operations = new List<Operation>
        {
            CreateOperation(billId, 3, "MEMBER_ADD"),
            CreateOperation(billId, 4, "MEMBER_UPDATE"),
            CreateOperation(billId, 5, "EXPENSE_ADD")
        };

        _unitOfWork.Operations.FindAsync(
            Arg.Any<System.Linq.Expressions.Expression<Func<Operation, bool>>>(),
            Arg.Any<Func<IQueryable<Operation>, IOrderedQueryable<Operation>>>(),
            Arg.Any<Func<IQueryable<Operation>, IQueryable<Operation>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()
        ).Returns(operations);

        // Act
        var result = await _sut.GetOperationsAsync(billId, 2);

        // Assert
        result.Should().HaveCount(3);
        result[0].Version.Should().Be(3);
        result[1].Version.Should().Be(4);
        result[2].Version.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Operation")]
    public async Task GetOperationsAsync_無遺漏操作_應回傳空列表()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Operations.FindAsync(
            Arg.Any<System.Linq.Expressions.Expression<Func<Operation, bool>>>(),
            Arg.Any<Func<IQueryable<Operation>, IOrderedQueryable<Operation>>>(),
            Arg.Any<Func<IQueryable<Operation>, IQueryable<Operation>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<Operation>());

        // Act
        var result = await _sut.GetOperationsAsync(billId, 10);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static Bill CreateBillWithVersion(Guid billId, long version)
    {
        return new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Version = version,
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };
    }

    private static OperationRequestDto CreateRequest(Guid billId, string opType, Guid? targetId, object payload,
        long baseVersion)
    {
        var json = JsonSerializer.Serialize(payload);
        var jsonElement = JsonDocument.Parse(json).RootElement;

        return new OperationRequestDto(
            "test-client",
            billId,
            opType,
            targetId,
            jsonElement,
            baseVersion
        );
    }

    private static Operation CreateOperation(Guid billId, long version, string opType)
    {
        return new Operation
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Version = version,
            OpType = opType,
            Payload = JsonDocument.Parse("{}"),
            ClientId = "test-client",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
