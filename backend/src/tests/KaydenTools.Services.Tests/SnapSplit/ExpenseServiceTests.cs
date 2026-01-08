using FluentAssertions;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// ExpenseService 單元測試
/// </summary>
public class ExpenseServiceTests
{
    private readonly ExpenseService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public ExpenseServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new ExpenseService(_unitOfWork);
    }

    #region GetByIdAsync 測試

    [Fact]
    [Trait("Category", "Expense")]
    public async Task GetByIdAsync_費用存在_應回傳費用資料()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var participant1 = Guid.NewGuid();
        var participant2 = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "晚餐",
            Amount = 1000m,
            ServiceFeePercent = 10m,
            IsItemized = false,
            PaidById = payerId,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<ExpenseParticipant>
            {
                new() { ExpenseId = expenseId, MemberId = participant1 },
                new() { ExpenseId = expenseId, MemberId = participant2 }
            }
        };

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);

        // Act
        var result = await _sut.GetByIdAsync(expenseId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(expenseId);
        result.Value.Name.Should().Be("晚餐");
        result.Value.Amount.Should().Be(1000m);
        result.Value.ServiceFeePercent.Should().Be(10m);
        result.Value.IsItemized.Should().BeFalse();
        result.Value.PaidById.Should().Be(payerId);
        result.Value.ParticipantIds.Should().HaveCount(2);
        result.Value.ParticipantIds.Should().Contain(new[] { participant1, participant2 });
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task GetByIdAsync_費用不存在_應回傳錯誤()
    {
        // Arrange
        var expenseId = Guid.NewGuid();

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        // Act
        var result = await _sut.GetByIdAsync(expenseId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ExpenseNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task GetByIdAsync_細項模式費用_應包含細項資料()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var participant1 = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            BillId = Guid.NewGuid(),
            Name = "火鍋",
            Amount = 2000m,
            ServiceFeePercent = 10m,
            IsItemized = true,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>
            {
                new()
                {
                    Id = itemId,
                    ExpenseId = expenseId,
                    Name = "肉盤",
                    Amount = 500m,
                    PaidById = participant1,
                    Participants = new List<ExpenseItemParticipant>
                    {
                        new() { ExpenseItemId = itemId, MemberId = participant1 }
                    }
                }
            }
        };

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);

        // Act
        var result = await _sut.GetByIdAsync(expenseId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsItemized.Should().BeTrue();
        result.Value.Items.Should().NotBeNull();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items![0].Name.Should().Be("肉盤");
        result.Value.Items[0].Amount.Should().Be(500m);
    }

    #endregion

    #region CreateAsync 測試

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_有效資料_應成功建立費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var participant1 = Guid.NewGuid();
        var participant2 = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var payer = new Member { Id = payerId, BillId = billId, Name = "付款者" };
        var members = new List<Member>
        {
            payer,
            new() { Id = participant1, BillId = billId, Name = "參與者1" },
            new() { Id = participant2, BillId = billId, Name = "參與者2" }
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(payerId, Arg.Any<CancellationToken>())
            .Returns(payer);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new CreateExpenseDto(
            "晚餐",
            1000m,
            10m,
            false,
            payerId,
            new List<Guid> { participant1, participant2 },
            null
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("晚餐");
        result.Value.Amount.Should().Be(1000m);
        result.Value.ServiceFeePercent.Should().Be(10m);
        result.Value.PaidById.Should().Be(payerId);
        result.Value.ParticipantIds.Should().HaveCount(2);

        await _unitOfWork.Expenses.Received(1).AddAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_帳單不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var dto = new CreateExpenseDto(
            "晚餐",
            1000m,
            0m,
            false,
            null,
            new List<Guid>(),
            null
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_付款者不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var invalidPayerId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(invalidPayerId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);

        var dto = new CreateExpenseDto(
            "晚餐",
            1000m,
            0m,
            false,
            invalidPayerId,
            new List<Guid>(),
            null
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_付款者不屬於此帳單_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var otherBillId = Guid.NewGuid();
        var payerId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var payer = new Member { Id = payerId, BillId = otherBillId, Name = "其他帳單的成員" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(payerId, Arg.Any<CancellationToken>())
            .Returns(payer);

        var dto = new CreateExpenseDto(
            "晚餐",
            1000m,
            0m,
            false,
            payerId,
            new List<Guid>(),
            null
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_參與者不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var validMemberId = Guid.NewGuid();
        var invalidMemberId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var members = new List<Member>
        {
            new() { Id = validMemberId, BillId = billId, Name = "有效成員" }
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new CreateExpenseDto(
            "晚餐",
            1000m,
            0m,
            false,
            null,
            new List<Guid> { validMemberId, invalidMemberId },
            null
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
        result.Error.Message.Should().Contain(invalidMemberId.ToString());
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_細項模式_應成功建立含細項的費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var members = new List<Member>
        {
            new() { Id = member1, BillId = billId, Name = "成員1" },
            new() { Id = member2, BillId = billId, Name = "成員2" }
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new CreateExpenseDto(
            "火鍋",
            2000m,
            10m,
            true,
            null,
            new List<Guid> { member1, member2 },
            new List<CreateExpenseItemDto>
            {
                new("肉盤", 500m, member1, new List<Guid> { member1, member2 }),
                new("蔬菜盤", 300m, member2, new List<Guid> { member1 })
            }
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsItemized.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);

        await _unitOfWork.Expenses.Received(1).AddAsync(
            Arg.Is<Expense>(e => e.Items.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_細項付款者不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var invalidPayer = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var members = new List<Member>
        {
            new() { Id = member1, BillId = billId, Name = "成員1" }
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new CreateExpenseDto(
            "火鍋",
            1000m,
            0m,
            true,
            null,
            new List<Guid> { member1 },
            new List<CreateExpenseItemDto>
            {
                new("肉盤", 500m, invalidPayer, new List<Guid> { member1 })
            }
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
        result.Error.Message.Should().Contain("Item payer");
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task CreateAsync_細項參與者不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var invalidParticipant = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var members = new List<Member>
        {
            new() { Id = member1, BillId = billId, Name = "成員1" }
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new CreateExpenseDto(
            "火鍋",
            1000m,
            0m,
            true,
            null,
            new List<Guid> { member1 },
            new List<CreateExpenseItemDto>
            {
                new("肉盤", 500m, member1, new List<Guid> { member1, invalidParticipant })
            }
        );

        // Act
        var result = await _sut.CreateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
        result.Error.Message.Should().Contain("Item participant");
    }

    #endregion

    #region UpdateAsync 測試

    [Fact]
    [Trait("Category", "Expense")]
    public async Task UpdateAsync_有效資料_應成功更新費用()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var participant1 = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "原始名稱",
            Amount = 500m,
            ServiceFeePercent = 0m,
            IsItemized = false,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>()
        };

        var members = new List<Member>
        {
            new() { Id = payerId, BillId = billId, Name = "付款者" },
            new() { Id = participant1, BillId = billId, Name = "參與者1" }
        };

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new UpdateExpenseDto(
            "更新後名稱",
            1000m,
            10m,
            payerId,
            new List<Guid> { participant1 },
            null
        );

        // Act
        var result = await _sut.UpdateAsync(expenseId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        expense.Name.Should().Be("更新後名稱");
        expense.Amount.Should().Be(1000m);
        expense.ServiceFeePercent.Should().Be(10m);
        expense.PaidById.Should().Be(payerId);

        _unitOfWork.Expenses.Received(1).Update(expense);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task UpdateAsync_費用不存在_應回傳錯誤()
    {
        // Arrange
        var expenseId = Guid.NewGuid();

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        var dto = new UpdateExpenseDto(
            "更新名稱",
            1000m,
            0m,
            null,
            new List<Guid>(),
            null
        );

        // Act
        var result = await _sut.UpdateAsync(expenseId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ExpenseNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task UpdateAsync_付款者不存在_應回傳錯誤()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var invalidPayerId = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "原始名稱",
            Amount = 500m,
            IsItemized = false,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>()
        };

        var members = new List<Member>();

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new UpdateExpenseDto(
            "更新名稱",
            1000m,
            0m,
            invalidPayerId,
            new List<Guid>(),
            null
        );

        // Act
        var result = await _sut.UpdateAsync(expenseId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task UpdateAsync_細項模式更新_應成功更新細項()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = "火鍋",
            Amount = 1000m,
            IsItemized = true,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>
            {
                new() { Id = Guid.NewGuid(), ExpenseId = expenseId, Name = "舊細項", Amount = 200m }
            }
        };

        var members = new List<Member>
        {
            new() { Id = member1, BillId = billId, Name = "成員1" },
            new() { Id = member2, BillId = billId, Name = "成員2" }
        };

        _unitOfWork.Expenses.GetByIdWithDetailsAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(members);

        var dto = new UpdateExpenseDto(
            "火鍋更新",
            2000m,
            10m,
            member1,
            new List<Guid> { member1, member2 },
            new List<CreateExpenseItemDto>
            {
                new("新肉盤", 800m, member1, new List<Guid> { member1, member2 }),
                new("新蔬菜", 400m, member2, new List<Guid> { member2 })
            }
        );

        // Act
        var result = await _sut.UpdateAsync(expenseId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        expense.Items.Should().HaveCount(2);
        expense.Items.Should().Contain(i => i.Name == "新肉盤");
        expense.Items.Should().Contain(i => i.Name == "新蔬菜");
    }

    #endregion

    #region DeleteAsync 測試

    [Fact]
    [Trait("Category", "Expense")]
    public async Task DeleteAsync_費用存在_應成功刪除()
    {
        // Arrange
        var expenseId = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            Name = "待刪除費用"
        };

        _unitOfWork.Expenses.GetByIdAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(expense);

        // Act
        var result = await _sut.DeleteAsync(expenseId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Expenses.Received(1).Remove(expense);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Expense")]
    public async Task DeleteAsync_費用不存在_應回傳錯誤()
    {
        // Arrange
        var expenseId = Guid.NewGuid();

        _unitOfWork.Expenses.GetByIdAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        // Act
        var result = await _sut.DeleteAsync(expenseId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ExpenseNotFound);
        _unitOfWork.Expenses.DidNotReceive().Remove(Arg.Any<Expense>());
    }

    #endregion
}
