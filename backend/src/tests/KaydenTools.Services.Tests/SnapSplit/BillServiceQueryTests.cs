using FluentAssertions;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// BillService 查詢方法測試
/// </summary>
public class BillServiceQueryTests
{
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public BillServiceQueryTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();
        _sut = new BillService(_unitOfWork, _notificationService);
    }

    #region GetByIdAsync Tests

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByIdAsync_帳單存在_應回傳帳單DTO()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            ShareCode = "ABC12345",
            Version = 3,
            IsSettled = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            Members = new List<Member>
            {
                new()
                {
                    Id = memberId,
                    Name = "Alice",
                    DisplayOrder = 0
                }
            },
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "午餐",
                    Amount = 300,
                    ServiceFeePercent = 10,
                    IsItemized = false,
                    PaidById = memberId,
                    Participants = new List<ExpenseParticipant>
                    {
                        new() { MemberId = memberId }
                    },
                    CreatedAt = DateTime.UtcNow
                }
            },
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GetByIdAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(billId);
        result.Value.Name.Should().Be("測試帳單");
        result.Value.ShareCode.Should().Be("ABC12345");
        result.Value.Version.Should().Be(3);
        result.Value.Members.Should().HaveCount(1);
        result.Value.Members[0].Name.Should().Be("Alice");
        result.Value.Expenses.Should().HaveCount(1);
        result.Value.Expenses[0].Name.Should().Be("午餐");
        result.Value.Expenses[0].Amount.Should().Be(300);
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByIdAsync_帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();
        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.GetByIdAsync(billId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByIdAsync_帳單含費用細項_應正確映射細項資料()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
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
                    Amount = 1000,
                    IsItemized = true,
                    Participants = new List<ExpenseParticipant>(),
                    Items = new List<ExpenseItem>
                    {
                        new()
                        {
                            Id = itemId,
                            Name = "主餐",
                            Amount = 500,
                            PaidById = memberId,
                            Participants = new List<ExpenseItemParticipant>
                            {
                                new() { MemberId = memberId }
                            }
                        }
                    },
                    CreatedAt = DateTime.UtcNow
                }
            },
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GetByIdAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Expenses[0].Items.Should().NotBeNull();
        result.Value.Expenses[0].Items.Should().HaveCount(1);
        result.Value.Expenses[0].Items![0].Name.Should().Be("主餐");
        result.Value.Expenses[0].Items![0].Amount.Should().Be(500);
        result.Value.Expenses[0].Items![0].ParticipantIds.Should().Contain(memberId);
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByIdAsync_帳單含已結清轉帳_應正確格式化轉帳字串()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var fromMemberId = Guid.NewGuid();
        var toMemberId = Guid.NewGuid();

        var bill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Members = new List<Member>
            {
                new() { Id = fromMemberId, Name = "Alice" },
                new() { Id = toMemberId, Name = "Bob" }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>
            {
                new()
                {
                    FromMemberId = fromMemberId,
                    ToMemberId = toMemberId,
                    Amount = 150.50m
                }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GetByIdAsync(billId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SettledTransfers.Should().HaveCount(1);
        result.Value.SettledTransfers[0].Should().Be($"{fromMemberId}-{toMemberId}:150.50");
    }

    #endregion

    #region GetByShareCodeAsync Tests

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByShareCodeAsync_分享碼存在_應回傳帳單DTO()
    {
        // Arrange
        var shareCode = "ABC12345";
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            Name = "分享帳單",
            ShareCode = shareCode,
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByShareCodeAsync(shareCode, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.GetByShareCodeAsync(shareCode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShareCode.Should().Be(shareCode);
        result.Value.Name.Should().Be("分享帳單");
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByShareCodeAsync_分享碼不存在_應回傳無效分享碼錯誤()
    {
        // Arrange
        var shareCode = "INVALID1";
        _unitOfWork.Bills.GetByShareCodeAsync(shareCode, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // Act
        var result = await _sut.GetByShareCodeAsync(shareCode);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidShareCode);
    }

    #endregion

    #region GetByOwnerIdAsync Tests

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByOwnerIdAsync_用戶有多個帳單_應回傳所有帳單摘要()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var bills = new List<Bill>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "帳單1",
                OwnerId = ownerId,
                IsSettled = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Members = new List<Member> { new(), new() },
                Expenses = new List<Expense>
                {
                    new() { Amount = 100, ServiceFeePercent = 0 },
                    new() { Amount = 200, ServiceFeePercent = 10 }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "帳單2",
                OwnerId = ownerId,
                IsSettled = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow,
                Members = new List<Member> { new() },
                Expenses = new List<Expense>
                {
                    new() { Amount = 500, ServiceFeePercent = 0 }
                }
            }
        };

        _unitOfWork.Bills.GetByOwnerIdAsync(ownerId, Arg.Any<CancellationToken>())
            .Returns(bills);

        // Act
        var result = await _sut.GetByOwnerIdAsync(ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var summary1 = result.Value.First(b => b.Name == "帳單1");
        summary1.MemberCount.Should().Be(2);
        summary1.ExpenseCount.Should().Be(2);
        summary1.TotalAmount.Should().Be(100 + 200 * 1.1m); // 320
        summary1.IsSettled.Should().BeFalse();

        var summary2 = result.Value.First(b => b.Name == "帳單2");
        summary2.MemberCount.Should().Be(1);
        summary2.ExpenseCount.Should().Be(1);
        summary2.TotalAmount.Should().Be(500);
        summary2.IsSettled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByOwnerIdAsync_用戶沒有帳單_應回傳空列表()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        _unitOfWork.Bills.GetByOwnerIdAsync(ownerId, Arg.Any<CancellationToken>())
            .Returns(new List<Bill>());

        // Act
        var result = await _sut.GetByOwnerIdAsync(ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GetByLinkedUserIdAsync Tests

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByLinkedUserIdAsync_用戶有關聯帳單_應回傳所有帳單DTO()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bills = new List<Bill>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "關聯帳單1",
                Members = new List<Member>
                {
                    new()
                    {
                        Name = "我",
                        LinkedUserId = userId,
                        ClaimedAt = DateTime.UtcNow
                    }
                },
                Expenses = new List<Expense>(),
                SettledTransfers = new List<SettledTransfer>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "關聯帳單2",
                Members = new List<Member>
                {
                    new()
                    {
                        Name = "本人",
                        LinkedUserId = userId,
                        ClaimedAt = DateTime.UtcNow
                    }
                },
                Expenses = new List<Expense>(),
                SettledTransfers = new List<SettledTransfer>()
            }
        };

        _unitOfWork.Bills.GetByLinkedUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(bills);

        // Act
        var result = await _sut.GetByLinkedUserIdAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(b => b.Name).Should().Contain(new[] { "關聯帳單1", "關聯帳單2" });
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetByLinkedUserIdAsync_用戶沒有關聯帳單_應回傳空列表()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _unitOfWork.Bills.GetByLinkedUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Bill>());

        // Act
        var result = await _sut.GetByLinkedUserIdAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion
}
