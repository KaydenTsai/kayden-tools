using FluentAssertions;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// BillAuthService 單元測試
/// </summary>
public class BillAuthServiceTests
{
    private readonly BillAuthService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public BillAuthServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new BillAuthService(_unitOfWork);
    }

    #region IsOwnerAsync

    [Fact]
    public async Task IsOwnerAsync_使用者是帳單擁有者_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = userId });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerAsync(billId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerAsync_使用者非帳單擁有者_應回傳False()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = Guid.NewGuid() });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerAsync(billId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOwnerAsync_帳單不存在_應回傳False()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.IsOwnerAsync(billId, Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsParticipantAsync

    [Fact]
    public async Task IsParticipantAsync_使用者已認領成員_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = Guid.NewGuid() });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { new() { Id = Guid.NewGuid(), BillId = billId, LinkedUserId = userId } });

        // Act
        var result = await _sut.IsParticipantAsync(billId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsParticipantAsync_使用者未認領成員_應回傳False()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = Guid.NewGuid() });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { new() { Id = Guid.NewGuid(), BillId = billId, LinkedUserId = Guid.NewGuid() } });

        // Act
        var result = await _sut.IsParticipantAsync(billId, userId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsOwnerOrParticipantAsync

    [Fact]
    public async Task IsOwnerOrParticipantAsync_是擁有者_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = userId });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerOrParticipantAsync(billId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerOrParticipantAsync_是參與者_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = Guid.NewGuid() });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { new() { Id = Guid.NewGuid(), BillId = billId, LinkedUserId = userId } });

        // Act
        var result = await _sut.IsOwnerOrParticipantAsync(billId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerOrParticipantAsync_非擁有者也非參與者_應回傳False()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = Guid.NewGuid() });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerOrParticipantAsync(billId, userId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsOwnerOrParticipantByMemberIdAsync

    [Fact]
    public async Task IsOwnerOrParticipantByMemberIdAsync_成員存在且使用者有權限_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new Member { Id = memberId, BillId = billId });
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = userId });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerOrParticipantByMemberIdAsync(memberId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerOrParticipantByMemberIdAsync_成員不存在_應回傳False()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);

        // Act
        var result = await _sut.IsOwnerOrParticipantByMemberIdAsync(memberId, Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsOwnerOrParticipantByExpenseIdAsync

    [Fact]
    public async Task IsOwnerOrParticipantByExpenseIdAsync_費用存在且使用者有權限_應回傳True()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.Expenses.GetByIdAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns(new Expense { Id = expenseId, BillId = billId });
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = userId });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        var result = await _sut.IsOwnerOrParticipantByExpenseIdAsync(expenseId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerOrParticipantByExpenseIdAsync_費用不存在_應回傳False()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        _unitOfWork.Expenses.GetByIdAsync(expenseId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        // Act
        var result = await _sut.IsOwnerOrParticipantByExpenseIdAsync(expenseId, Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task 同一BillId重複查詢_應使用快取不重複查資料庫()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new Bill { Id = billId, OwnerId = userId });
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        // Act
        await _sut.IsOwnerAsync(billId, userId);
        await _sut.IsParticipantAsync(billId, userId);
        await _sut.IsOwnerOrParticipantAsync(billId, userId);

        // Assert — Bills.GetByIdAsync should only be called once due to caching
        await _unitOfWork.Bills.Received(1).GetByIdAsync(billId, Arg.Any<CancellationToken>());
        await _unitOfWork.Members.Received(1).GetByBillIdAsync(billId, Arg.Any<CancellationToken>());
    }

    #endregion
}
