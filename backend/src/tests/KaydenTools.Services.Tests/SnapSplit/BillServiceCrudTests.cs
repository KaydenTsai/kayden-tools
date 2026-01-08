using FluentAssertions;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// BillService CRUD 方法測試
/// </summary>
public class BillServiceCrudTests
{
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public BillServiceCrudTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();
        _sut = new BillService(_unitOfWork, _notificationService);
    }

    #region CreateAsync Tests

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task CreateAsync_有效資料_應建立帳單並回傳DTO()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var dto = new CreateBillDto("新帳單");
        Bill? capturedBill = null;

        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.Bills.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var id = callInfo.Arg<Guid>();
                if (capturedBill != null && capturedBill.Id == id)
                {
                    capturedBill.Members = new List<Member>();
                    capturedBill.Expenses = new List<Expense>();
                    capturedBill.SettledTransfers = new List<SettledTransfer>();
                    return capturedBill;
                }
                return null;
            });

        // Act
        var result = await _sut.CreateAsync(dto, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("新帳單");

        await _unitOfWork.Bills.Received(1).AddAsync(Arg.Any<Bill>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        capturedBill.Should().NotBeNull();
        capturedBill!.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task CreateAsync_無擁有者_應建立無擁有者帳單()
    {
        // Arrange
        var dto = new CreateBillDto("匿名帳單");
        Bill? capturedBill = null;

        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.Bills.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var id = callInfo.Arg<Guid>();
                if (capturedBill != null && capturedBill.Id == id)
                {
                    capturedBill.Members = new List<Member>();
                    capturedBill.Expenses = new List<Expense>();
                    capturedBill.SettledTransfers = new List<SettledTransfer>();
                    return capturedBill;
                }
                return null;
            });

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedBill!.OwnerId.Should().BeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task UpdateAsync_帳單存在_應更新帳單名稱()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            Name = "舊名稱",
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var dto = new UpdateBillDto("新名稱");

        // Act
        var result = await _sut.UpdateAsync(billId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bill.Name.Should().Be("新名稱");

        _unitOfWork.Bills.Received(1).Update(bill);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task UpdateAsync_帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var dto = new UpdateBillDto("新名稱");

        // Act
        var result = await _sut.UpdateAsync(billId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);

        _unitOfWork.Bills.DidNotReceive().Update(Arg.Any<Bill>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task DeleteAsync_帳單存在_應刪除帳單()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = new Bill { Id = billId, Name = "待刪除帳單" };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.DeleteAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _unitOfWork.Bills.Received(1).Remove(bill);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task DeleteAsync_帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.DeleteAsync(billId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);

        _unitOfWork.Bills.DidNotReceive().Remove(Arg.Any<Bill>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GenerateShareCodeAsync Tests

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task GenerateShareCodeAsync_帳單無分享碼_應產生新分享碼()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            ShareCode = null
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GenerateShareCodeAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().HaveLength(8);
        result.Value.Should().MatchRegex("^[A-Z0-9]{8}$");

        bill.ShareCode.Should().Be(result.Value);
        _unitOfWork.Bills.Received(1).Update(bill);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task GenerateShareCodeAsync_帳單已有分享碼_應回傳現有分享碼()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var existingCode = "EXIST123";
        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            ShareCode = existingCode
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GenerateShareCodeAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingCode);

        _unitOfWork.Bills.DidNotReceive().Update(Arg.Any<Bill>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task GenerateShareCodeAsync_帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.GenerateShareCodeAsync(billId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "CRUD")]
    public async Task GenerateShareCodeAsync_帳單有空白分享碼_應產生新分享碼()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            ShareCode = ""
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GenerateShareCodeAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().HaveLength(8);

        _unitOfWork.Bills.Received(1).Update(bill);
    }

    #endregion
}
