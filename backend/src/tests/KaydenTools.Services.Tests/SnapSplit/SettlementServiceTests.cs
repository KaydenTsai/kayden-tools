using FluentAssertions;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// SettlementService 單元測試
/// </summary>
public class SettlementServiceTests
{
    private readonly SettlementService _sut;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeService _dateTimeService;

    public SettlementServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _dateTimeService = Substitute.For<IDateTimeService>();
        _dateTimeService.UtcNow.Returns(DateTime.UtcNow);
        _sut = new SettlementService(_unitOfWork, _dateTimeService);
    }

    #region CalculateAsync 基本測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_帳單不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_空帳單_應回傳零金額()
    {
        // Arrange
        var billId = Guid.NewGuid();

        var bill = new Bill
        {
            Id = billId,
            Name = "空帳單",
            Members = new List<Member>
            {
                new() { Id = Guid.NewGuid(), BillId = billId, Name = "成員1" }
            },
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(0m);
        result.Value.TotalWithServiceFee.Should().Be(0m);
        result.Value.Transfers.Should().BeEmpty();
    }

    #endregion

    #region CalculateAsync 簡單模式費用測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_簡單模式_兩人平分_應正確計算餘額()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var expense = CreateSimpleExpense(billId, "晚餐", 1000m, member1Id, new[] { member1Id, member2Id });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1000m);
        result.Value.TotalWithServiceFee.Should().Be(1000m);

        // 成員1 付了 1000，應付 500，餘額 +500
        var member1Balance = result.Value.MemberBalances.First(b => b.MemberId == member1Id);
        member1Balance.TotalPaid.Should().Be(1000m);
        member1Balance.TotalOwed.Should().Be(500m);
        member1Balance.Balance.Should().Be(500m);

        // 成員2 付了 0，應付 500，餘額 -500
        var member2Balance = result.Value.MemberBalances.First(b => b.MemberId == member2Id);
        member2Balance.TotalPaid.Should().Be(0m);
        member2Balance.TotalOwed.Should().Be(500m);
        member2Balance.Balance.Should().Be(-500m);

        // 應有一筆轉帳：成員2 -> 成員1 500元
        result.Value.Transfers.Should().HaveCount(1);
        var transfer = result.Value.Transfers.First();
        transfer.FromMemberId.Should().Be(member2Id);
        transfer.ToMemberId.Should().Be(member1Id);
        transfer.Amount.Should().Be(500m);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_簡單模式_含服務費_應正確計算含服務費金額()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var expense = CreateSimpleExpense(billId, "晚餐", 1000m, member1Id, new[] { member1Id, member2Id }, 10m);

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1000m);
        result.Value.TotalWithServiceFee.Should().Be(1100m); // 1000 * 1.1

        // 成員1 付了 1100，應付 550，餘額 +550
        var member1Balance = result.Value.MemberBalances.First(b => b.MemberId == member1Id);
        member1Balance.TotalPaid.Should().Be(1100m);
        member1Balance.TotalOwed.Should().Be(550m);
        member1Balance.Balance.Should().Be(550m);

        // 轉帳金額應為 550
        result.Value.Transfers.First().Amount.Should().Be(550m);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_簡單模式_三人不均分_應正確計算轉帳()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var member3Id = Guid.NewGuid();

        // 成員1 付了 900（只有成員1和成員2參與）
        var expense1 = CreateSimpleExpense(billId, "晚餐", 900m, member1Id, new[] { member1Id, member2Id });
        // 成員2 付了 300（三人參與）
        var expense2 = CreateSimpleExpense(billId, "飲料", 300m, member2Id, new[] { member1Id, member2Id, member3Id });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" },
                new() { Id = member3Id, BillId = billId, Name = "成員3" }
            },
            Expenses = new List<Expense> { expense1, expense2 }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1200m);

        // 成員1: 付 900，應付 450+100=550，餘額 +350
        var member1Balance = result.Value.MemberBalances.First(b => b.MemberId == member1Id);
        member1Balance.TotalPaid.Should().Be(900m);
        member1Balance.TotalOwed.Should().Be(550m);
        member1Balance.Balance.Should().Be(350m);

        // 成員2: 付 300，應付 450+100=550，餘額 -250
        var member2Balance = result.Value.MemberBalances.First(b => b.MemberId == member2Id);
        member2Balance.TotalPaid.Should().Be(300m);
        member2Balance.TotalOwed.Should().Be(550m);
        member2Balance.Balance.Should().Be(-250m);

        // 成員3: 付 0，應付 100，餘額 -100
        var member3Balance = result.Value.MemberBalances.First(b => b.MemberId == member3Id);
        member3Balance.TotalPaid.Should().Be(0m);
        member3Balance.TotalOwed.Should().Be(100m);
        member3Balance.Balance.Should().Be(-100m);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_簡單模式_無付款者_應跳過該費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var expenseWithPayer = CreateSimpleExpense(billId, "晚餐", 1000m, member1Id, new[] { member1Id, member2Id });
        var expenseWithoutPayer = new Expense
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Name = "無付款者費用",
            Amount = 500m,
            PaidById = null,
            IsItemized = false,
            Participants = new List<ExpenseParticipant>
            {
                new() { MemberId = member1Id },
                new() { MemberId = member2Id }
            }
        };

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expenseWithPayer, expenseWithoutPayer }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // 只計算有付款者的費用
        result.Value.TotalAmount.Should().Be(1000m);
    }

    #endregion

    #region CalculateAsync 細項模式測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_細項模式_應正確計算各細項分攤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        // 細項1: 肉盤 600元，成員1付，兩人分
        // 細項2: 蔬菜 200元，成員2付，只有成員2
        var expense = CreateItemizedExpense(billId, "火鍋", new[]
        {
            (name: "肉盤", amount: 600m, paidById: member1Id, participants: new[] { member1Id, member2Id }),
            (name: "蔬菜", amount: 200m, paidById: member2Id, participants: new[] { member2Id })
        });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(800m);

        // 成員1: 付 600，應付 300（肉盤的一半），餘額 +300
        var member1Balance = result.Value.MemberBalances.First(b => b.MemberId == member1Id);
        member1Balance.TotalPaid.Should().Be(600m);
        member1Balance.TotalOwed.Should().Be(300m);
        member1Balance.Balance.Should().Be(300m);

        // 成員2: 付 200，應付 300+200=500，餘額 -300
        var member2Balance = result.Value.MemberBalances.First(b => b.MemberId == member2Id);
        member2Balance.TotalPaid.Should().Be(200m);
        member2Balance.TotalOwed.Should().Be(500m);
        member2Balance.Balance.Should().Be(-300m);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_細項模式_含服務費_應正確計算()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var expense = CreateItemizedExpense(billId, "火鍋", new[]
        {
            (name: "肉盤", amount: 1000m, paidById: member1Id, participants: new[] { member1Id, member2Id })
        }, serviceFeePercent: 10m);

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1000m);
        result.Value.TotalWithServiceFee.Should().Be(1100m);

        // 轉帳應為 550 (1100 / 2)
        result.Value.Transfers.First().Amount.Should().Be(550m);
    }

    #endregion

    #region CalculateAsync 已結清狀態測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_有已結清轉帳_應標記為已結清()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var expense = CreateSimpleExpense(billId, "晚餐", 1000m, member1Id, new[] { member1Id, member2Id });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense }
        };

        var settledTransfers = new List<SettledTransfer>
        {
            new()
            {
                BillId = billId,
                FromMemberId = member2Id,
                ToMemberId = member1Id,
                Amount = 500m,
                SettledAt = DateTime.UtcNow
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(settledTransfers);

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transfers.Should().HaveCount(1);
        result.Value.Transfers.First().IsSettled.Should().BeTrue();
    }

    #endregion

    #region ToggleSettledAsync 測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_帳單不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_FromMember不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();
        var invalidFromMemberId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var toMember = new Member { Id = toMemberId, BillId = billId, Name = "收款方" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(invalidFromMemberId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);
        _unitOfWork.Members.GetByIdAsync(toMemberId, Arg.Any<CancellationToken>())
            .Returns(toMember);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, invalidFromMemberId, toMemberId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
        result.Error.Message.Should().Contain("From member");
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_ToMember不存在_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var invalidToMemberId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var fromMember = new Member { Id = fromMemberId, BillId = billId, Name = "付款方" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(fromMemberId, Arg.Any<CancellationToken>())
            .Returns(fromMember);
        _unitOfWork.Members.GetByIdAsync(invalidToMemberId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, fromMemberId, invalidToMemberId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
        result.Error.Message.Should().Contain("To member");
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_FromMember不屬於此帳單_應回傳錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var otherBillId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var fromMember = new Member { Id = fromMemberId, BillId = otherBillId, Name = "其他帳單成員" };
        var toMember = new Member { Id = toMemberId, BillId = billId, Name = "收款方" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(fromMemberId, Arg.Any<CancellationToken>())
            .Returns(fromMember);
        _unitOfWork.Members.GetByIdAsync(toMemberId, Arg.Any<CancellationToken>())
            .Returns(toMember);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, fromMemberId, toMemberId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_尚未結清_應建立結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();
        var now = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var fromMember = new Member { Id = fromMemberId, BillId = billId, Name = "付款方" };
        var toMember = new Member { Id = toMemberId, BillId = billId, Name = "收款方" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(fromMemberId, Arg.Any<CancellationToken>())
            .Returns(fromMember);
        _unitOfWork.Members.GetByIdAsync(toMemberId, Arg.Any<CancellationToken>())
            .Returns(toMember);
        _unitOfWork.SettledTransfers.GetByKeyAsync(billId, fromMemberId, toMemberId, Arg.Any<CancellationToken>())
            .Returns((SettledTransfer?)null);
        _dateTimeService.UtcNow.Returns(now);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, fromMemberId, toMemberId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.SettledTransfers.Received(1).AddAsync(
            Arg.Is<SettledTransfer>(s =>
                s.BillId == billId &&
                s.FromMemberId == fromMemberId &&
                s.ToMemberId == toMemberId &&
                s.SettledAt == now));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task ToggleSettledAsync_已結清_應移除結清記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();

        var bill = new Bill { Id = billId, Name = "測試帳單" };
        var fromMember = new Member { Id = fromMemberId, BillId = billId, Name = "付款方" };
        var toMember = new Member { Id = toMemberId, BillId = billId, Name = "收款方" };
        var existingSettled = new SettledTransfer
        {
            BillId = billId,
            FromMemberId = fromMemberId,
            ToMemberId = toMemberId,
            Amount = 500m,
            SettledAt = DateTime.UtcNow
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.Members.GetByIdAsync(fromMemberId, Arg.Any<CancellationToken>())
            .Returns(fromMember);
        _unitOfWork.Members.GetByIdAsync(toMemberId, Arg.Any<CancellationToken>())
            .Returns(toMember);
        _unitOfWork.SettledTransfers.GetByKeyAsync(billId, fromMemberId, toMemberId, Arg.Any<CancellationToken>())
            .Returns(existingSettled);

        // Act
        var result = await _sut.ToggleSettledAsync(billId, fromMemberId, toMemberId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.SettledTransfers.Received(1).Remove(existingSettled);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region 轉帳最小化測試

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_複雜情況_應使用貪婪法最小化轉帳數量()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var member3Id = Guid.NewGuid();
        var member4Id = Guid.NewGuid();

        // 設計一個需要轉帳最小化的情境
        // 成員1 付 1000（四人分）-> 應收 750
        // 成員2 付 400（四人分）-> 應收 100
        // 成員3 付 0 -> 應付 350
        // 成員4 付 0 -> 應付 500
        var expense1 = CreateSimpleExpense(billId, "晚餐", 1000m, member1Id,
            new[] { member1Id, member2Id, member3Id, member4Id });
        var expense2 = CreateSimpleExpense(billId, "飲料", 400m, member2Id,
            new[] { member1Id, member2Id, member3Id, member4Id });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" },
                new() { Id = member3Id, BillId = billId, Name = "成員3" },
                new() { Id = member4Id, BillId = billId, Name = "成員4" }
            },
            Expenses = new List<Expense> { expense1, expense2 }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1400m);

        // 總轉帳金額應等於欠款總額
        var totalTransferAmount = result.Value.Transfers.Sum(t => t.Amount);
        var totalDebt = result.Value.MemberBalances.Where(b => b.Balance < 0).Sum(b => Math.Abs(b.Balance));
        totalTransferAmount.Should().Be(totalDebt);

        // 驗證所有轉帳都是從負餘額成員到正餘額成員
        foreach (var transfer in result.Value.Transfers)
        {
            var fromBalance = result.Value.MemberBalances.First(b => b.MemberId == transfer.FromMemberId).Balance;
            var toBalance = result.Value.MemberBalances.First(b => b.MemberId == transfer.ToMemberId).Balance;

            fromBalance.Should().BeLessThan(0);
            toBalance.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    [Trait("Category", "Settlement")]
    public async Task CalculateAsync_所有人餘額為零_應無轉帳()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        // 兩人各付自己的份
        var expense1 = CreateSimpleExpense(billId, "成員1的餐", 500m, member1Id, new[] { member1Id });
        var expense2 = CreateSimpleExpense(billId, "成員2的餐", 500m, member2Id, new[] { member2Id });

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = member1Id, BillId = billId, Name = "成員1" },
                new() { Id = member2Id, BillId = billId, Name = "成員2" }
            },
            Expenses = new List<Expense> { expense1, expense2 }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);
        _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<SettledTransfer>());

        // Act
        var result = await _sut.CalculateAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transfers.Should().BeEmpty();

        // 所有人餘額應為 0
        foreach (var balance in result.Value.MemberBalances)
        {
            balance.Balance.Should().Be(0m);
        }
    }

    #endregion

    #region Helper Methods

    private static Expense CreateSimpleExpense(
        Guid billId,
        string name,
        decimal amount,
        Guid paidById,
        Guid[] participantIds,
        decimal serviceFeePercent = 0m)
    {
        var expenseId = Guid.NewGuid();
        return new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = name,
            Amount = amount,
            ServiceFeePercent = serviceFeePercent,
            IsItemized = false,
            PaidById = paidById,
            Participants = participantIds.Select(p => new ExpenseParticipant
            {
                ExpenseId = expenseId,
                MemberId = p
            }).ToList(),
            Items = new List<ExpenseItem>()
        };
    }

    private static Expense CreateItemizedExpense(
        Guid billId,
        string name,
        (string name, decimal amount, Guid paidById, Guid[] participants)[] items,
        decimal serviceFeePercent = 0m)
    {
        var expenseId = Guid.NewGuid();
        var expense = new Expense
        {
            Id = expenseId,
            BillId = billId,
            Name = name,
            Amount = items.Sum(i => i.amount),
            ServiceFeePercent = serviceFeePercent,
            IsItemized = true,
            PaidById = null,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>()
        };

        foreach (var item in items)
        {
            var itemId = Guid.NewGuid();
            expense.Items.Add(new ExpenseItem
            {
                Id = itemId,
                ExpenseId = expenseId,
                Name = item.name,
                Amount = item.amount,
                PaidById = item.paidById,
                Participants = item.participants.Select(p => new ExpenseItemParticipant
                {
                    ExpenseItemId = itemId,
                    MemberId = p
                }).ToList()
            });
        }

        return expense;
    }

    #endregion
}
