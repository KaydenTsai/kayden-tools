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
/// BillService SyncBillAsync 方法測試
/// </summary>
public class BillServiceSyncTests
{
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public BillServiceSyncTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();

        // 模擬事務執行：直接執行傳入的 Action
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.Arg<Func<Task<Result<SyncBillResponseDto>>>>();
                return await action();
            });

        _sut = new BillService(_unitOfWork, _notificationService);
    }

    #region 首次同步 (新建帳單)

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_首次同步_應建立新帳單並產生分享碼()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "新帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new()
                    {
                        LocalId = "local-member-1",
                        RemoteId = null,
                        Name = "Alice",
                        DisplayOrder = 0
                    },
                    new()
                    {
                        LocalId = "local-member-2",
                        RemoteId = null,
                        Name = "Bob",
                        DisplayOrder = 1
                    }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SyncBillAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShareCode.Should().NotBeNullOrEmpty();
        result.Value.Version.Should().Be(2); // Bill 預設 Version=1，同步後遞增為 2

        // 驗證 ID 映射
        result.Value.IdMappings.Members.Should().ContainKey("local-member-1");
        result.Value.IdMappings.Members.Should().ContainKey("local-member-2");

        // 驗證帳單資料
        capturedBill.Should().NotBeNull();
        capturedBill!.OwnerId.Should().Be(ownerId);
        capturedBill.Members.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_首次同步含費用_應建立帳單與費用並映射參與者()
    {
        // Arrange
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "聚餐帳單",
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
                        Name = "午餐",
                        Amount = 300,
                        ServiceFeePercent = 10,
                        IsItemized = false,
                        PaidByLocalId = "m1",
                        ParticipantLocalIds = new List<string> { "m1", "m2" }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // 捕獲費用（因為使用 Repository.AddAsync 而非導航屬性）
        Expense? capturedExpense = null;
        _unitOfWork.Expenses.AddAsync(Arg.Do<Expense>(e => capturedExpense = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings.Expenses.Should().ContainKey("e1");

        // 驗證費用通過 Repository 加入（非導航屬性）
        capturedExpense.Should().NotBeNull("費用應透過 Repository.AddAsync 加入");
        capturedExpense!.Name.Should().Be("午餐");
        capturedExpense.Amount.Should().Be(300);
        capturedExpense.ServiceFeePercent.Should().Be(10);
        capturedExpense.Participants.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_首次同步含費用細項_應正確映射細項與參與者()
    {
        // Arrange
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-1",
            RemoteId = null,
            BaseVersion = 0,
            Name = "聚餐帳單",
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
                        Name = "晚餐",
                        Amount = 1000,
                        IsItemized = true,
                        ParticipantLocalIds = new List<string>(),
                        Items = new SyncExpenseItemCollectionDto
                        {
                            Upsert = new List<SyncExpenseItemDto>
                            {
                                new()
                                {
                                    LocalId = "i1",
                                    Name = "牛排",
                                    Amount = 600,
                                    PaidByLocalId = "m1",
                                    ParticipantLocalIds = new List<string> { "m1" }
                                },
                                new()
                                {
                                    LocalId = "i2",
                                    Name = "沙拉",
                                    Amount = 400,
                                    PaidByLocalId = "m2",
                                    ParticipantLocalIds = new List<string> { "m1", "m2" }
                                }
                            },
                            DeletedIds = new List<string>()
                        }
                    }
                },
                DeletedIds = new List<string>()
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // 捕獲費用（因為使用 Repository.AddAsync 而非導航屬性）
        Expense? capturedExpense = null;
        _unitOfWork.Expenses.AddAsync(Arg.Do<Expense>(e => capturedExpense = e), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IdMappings.ExpenseItems.Should().ContainKey("i1");
        result.Value.IdMappings.ExpenseItems.Should().ContainKey("i2");

        // 驗證費用通過 Repository 加入（非導航屬性）
        capturedExpense.Should().NotBeNull("費用應透過 Repository.AddAsync 加入");
        capturedExpense!.Items.Should().HaveCount(2);

        var item1 = capturedExpense.Items.First(i => i.Name == "牛排");
        item1.Amount.Should().Be(600);
        item1.Participants.Should().HaveCount(1);

        var item2 = capturedExpense.Items.First(i => i.Name == "沙拉");
        item2.Amount.Should().Be(400);
        item2.Participants.Should().HaveCount(2);
    }

    #endregion

    #region 更新現有帳單

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_更新帳單_版本一致_應成功更新()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "舊名稱",
            Version = 2,
            ShareCode = "ABC12345",
            Members = new List<Member>
            {
                new() { Id = memberId, Name = "Alice", DisplayOrder = 0 }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 2, // 版本一致
            Name = "新名稱",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new()
                    {
                        LocalId = "m1",
                        RemoteId = memberId,
                        Name = "Alice (Updated)",
                        DisplayOrder = 0
                    }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(3);
        existingBill.Name.Should().Be("新名稱");
        existingBill.Members.First().Name.Should().Be("Alice (Updated)");
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_更新帳單_版本衝突_應回傳最新帳單資料並遞增版本()
    {
        // Arrange
        var billId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "伺服器版本名稱",
            Version = 5, // 伺服器版本較新
            ShareCode = "ABC12345",
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 3, // 過期版本
            Name = "用戶端名稱", // 嘗試更新名稱
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // 新行為：衝突時仍會處理同步（跳過 UPDATE/DELETE，但處理 ADD），版本遞增
        result.Value.Version.Should().Be(6);
        result.Value.LatestBill.Should().NotBeNull("衝突時應回傳 LatestBill 讓前端 rebase");
        result.Value.LatestBill!.Name.Should().Be("伺服器版本名稱", "衝突時名稱更新應被跳過");

        // 帳單名稱不應該被修改（UPDATE 操作在衝突時被跳過）
        existingBill.Name.Should().Be("伺服器版本名稱");
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_更新帳單不存在_應回傳找不到帳單錯誤()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Name = "帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.BillNotFound);
    }

    #endregion

    #region 刪除操作

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_刪除成員_應從帳單移除成員()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            ShareCode = "ABC12345",
            Members = new List<Member>
            {
                new() { Id = memberId1, Name = "Alice" },
                new() { Id = memberId2, Name = "Bob" }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = memberId1, Name = "Alice", DisplayOrder = 0 }
                },
                DeletedIds = new List<string> { memberId2.ToString() } // 刪除 Bob
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBill.Members.Should().HaveCount(1);
        existingBill.Members.First().Name.Should().Be("Alice");
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_刪除費用_應從帳單移除費用()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            ShareCode = "ABC12345",
            Members = new List<Member>(),
            Expenses = new List<Expense>
            {
                new()
                {
                    Id = expenseId,
                    Name = "待刪除費用",
                    Amount = 100,
                    Participants = new List<ExpenseParticipant>(),
                    Items = new List<ExpenseItem>()
                }
            },
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string> { expenseId.ToString() }
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBill.Expenses.Should().BeEmpty();
    }

    #endregion

    #region 已結清轉帳

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_新增已結清轉帳_舊格式_應建立轉帳記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            ShareCode = "ABC12345",
            Members = new List<Member>
            {
                new() { Id = memberId1, Name = "Alice" },
                new() { Id = memberId2, Name = "Bob" }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = memberId1, Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", RemoteId = memberId2, Name = "Bob", DisplayOrder = 1 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            },
            SettledTransfers = new List<string>
            {
                "m1-m2" // 舊格式，使用 LocalId（GUID 包含 '-' 無法直接解析）
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBill.SettledTransfers.Should().HaveCount(1);
        existingBill.SettledTransfers.First().FromMemberId.Should().Be(memberId1);
        existingBill.SettledTransfers.First().ToMemberId.Should().Be(memberId2);
        existingBill.SettledTransfers.First().Amount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_新增已結清轉帳_新格式_應建立含金額轉帳記錄()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 1,
            ShareCode = "ABC12345",
            Members = new List<Member>
            {
                new() { Id = memberId1, Name = "Alice" },
                new() { Id = memberId2, Name = "Bob" }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", RemoteId = memberId1, Name = "Alice", DisplayOrder = 0 },
                    new() { LocalId = "m2", RemoteId = memberId2, Name = "Bob", DisplayOrder = 1 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            },
            SettledTransfers = new List<string>
            {
                "m1-m2:150.50" // 新格式，使用 LocalId 含金額
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingBill.SettledTransfers.Should().HaveCount(1);
        existingBill.SettledTransfers.First().Amount.Should().Be(150.50m);
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_已結清轉帳使用LocalId_應正確解析()
    {
        // Arrange
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = null,
            BaseVersion = 0,
            Name = "帳單",
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
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            },
            SettledTransfers = new List<string>
            {
                "m1-m2:100.00" // 使用 LocalId
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedBill!.SettledTransfers.Should().HaveCount(1);
        capturedBill.SettledTransfers.First().Amount.Should().Be(100.00m);
    }

    #endregion

    #region 並發異常

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_資料庫並發異常_有RemoteId_應回傳最新帳單()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var existingBill = new Bill
        {
            Id = billId,
            Name = "原始名稱",
            Version = 1,
            ShareCode = "ABC12345",
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        var latestBill = new Bill
        {
            Id = billId,
            Name = "他人修改後的名稱",
            Version = 2,
            ShareCode = "ABC12345",
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        // 模擬事務執行時拋出並發異常
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .Throws(new DbUpdateConcurrencyException());

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(latestBill);

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = billId,
            BaseVersion = 1,
            Name = "我的修改",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(2);
        result.Value.LatestBill.Should().NotBeNull();
        result.Value.LatestBill!.Name.Should().Be("他人修改後的名稱");

        _unitOfWork.Received(1).ClearChangeTracker();
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    public async Task SyncBillAsync_資料庫並發異常_無RemoteId_應回傳錯誤()
    {
        // Arrange
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .Throws(new DbUpdateConcurrencyException());

        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill",
            RemoteId = null, // 新建帳單
            BaseVersion = 0,
            Name = "新帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Conflict);
    }

    #endregion

    #region 冪等性測試

    [Fact]
    [Trait("Category", "SyncBill")]
    [Trait("Category", "Idempotency")]
    public async Task SyncBillAsync_重複首次同步_已存在帳單_應回傳現有帳單與LatestBill()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var existingBillId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();

        // 模擬已存在的帳單（之前的請求已建立）
        var existingBill = new Bill
        {
            Id = existingBillId,
            Name = "已存在的帳單",
            LocalClientId = "local-bill-123",
            OwnerId = ownerId,
            Version = 2,
            ShareCode = "ABC12345",
            Members = new List<Member>
            {
                new() { Id = existingMemberId, Name = "Alice", DisplayOrder = 0 }
            },
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        // 模擬 GetByLocalClientIdAndOwnerAsync 回傳已存在的帳單
        _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync("local-bill-123", ownerId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        // 模擬「重複」的首次同步請求（LocalId 相同，但沒有 RemoteId）
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-123",
            RemoteId = null, // 前端不知道已建立，所以沒有 RemoteId
            BaseVersion = 0,
            Name = "已存在的帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new()
                    {
                        LocalId = "m1",
                        RemoteId = null,
                        Name = "Alice",
                        DisplayOrder = 0
                    }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RemoteId.Should().Be(existingBillId);
        result.Value.Version.Should().Be(2); // 版本不變（直接回傳現有帳單）

        // 關鍵：應該回傳 LatestBill 讓前端重建狀態
        result.Value.LatestBill.Should().NotBeNull();
        result.Value.LatestBill!.Id.Should().Be(existingBillId);
        result.Value.LatestBill!.Members.Should().HaveCount(1);
        result.Value.LatestBill!.Members.First().Id.Should().Be(existingMemberId);

        // 映射為空（因為成員未儲存 LocalId，無法重建映射）
        // 前端應使用 LatestBill 來匹配成員（例如按名稱）
        result.Value.IdMappings.Members.Should().BeEmpty();

        // 驗證沒有重複建立帳單（AddAsync 不應被呼叫）
        await _unitOfWork.Bills.DidNotReceive().AddAsync(Arg.Any<Bill>(), Arg.Any<CancellationToken>());

        // 驗證沒有觸發 SaveChanges（直接回傳，不修改資料庫）
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    [Trait("Category", "Idempotency")]
    public async Task SyncBillAsync_首次同步_無OwnerId_不檢查冪等性_應建立新帳單()
    {
        // Arrange
        var request = new SyncBillRequestDto
        {
            LocalId = "local-bill-123",
            RemoteId = null,
            BaseVersion = 0,
            Name = "新帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act - 無 ownerId
        var result = await _sut.SyncBillAsync(request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 驗證有建立新帳單（因為無 ownerId，不會檢查冪等性）
        capturedBill.Should().NotBeNull();

        // 匿名帳單也應該儲存 LocalClientId
        capturedBill!.LocalClientId.Should().Be("local-bill-123");
    }

    [Fact]
    [Trait("Category", "SyncBill")]
    [Trait("Category", "Idempotency")]
    public async Task SyncBillAsync_首次同步_LocalId為空_不檢查冪等性_應建立新帳單()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new SyncBillRequestDto
        {
            LocalId = "", // 空 LocalId
            RemoteId = null,
            BaseVersion = 0,
            Name = "新帳單",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>(),
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto
            {
                Upsert = new List<SyncExpenseDto>(),
                DeletedIds = new List<string>()
            }
        };

        Bill? capturedBill = null;
        _unitOfWork.Bills.AddAsync(Arg.Do<Bill>(b => capturedBill = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SyncBillAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedBill.Should().NotBeNull();

        // 不應該呼叫 GetByLocalClientIdAndOwnerAsync（因為 LocalId 為空）
        await _unitOfWork.Bills.DidNotReceive()
            .GetByLocalClientIdAndOwnerAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
