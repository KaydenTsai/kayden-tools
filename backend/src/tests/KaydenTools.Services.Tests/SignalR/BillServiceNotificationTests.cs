using FluentAssertions;
using Kayden.Commons.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KaydenTools.Services.Tests.SignalR;

/// <summary>
/// BillService 同步時通知服務呼叫測試
/// 驗證同步操作完成後是否正確發送 SignalR 通知
/// </summary>
public class BillServiceNotificationTests
{
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public BillServiceNotificationTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();

        // Mock DeltaSync transaction
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Kayden.Commons.Common.Result<DeltaSyncResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.Arg<Func<Task<Kayden.Commons.Common.Result<DeltaSyncResponse>>>>();
                return await action();
            });

        // Mock SyncBill transaction
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Kayden.Commons.Common.Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.Arg<Func<Task<Kayden.Commons.Common.Result<SyncBillResponseDto>>>>();
                return await action();
            });

        _sut = new BillService(_unitOfWork, _notificationService);
    }

    #region DeltaSync 通知測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_同步成功_應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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
                    new() { LocalId = "m1", Name = "新成員" }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();

        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            2, // 版本從 1 遞增到 2
            userId);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_版本衝突_仍應發送通知讓其他用戶端知道新版本()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 5, // 伺服器版本較新
            Members = new List<Member>
            {
                new() { Id = Guid.NewGuid(), Name = "現有成員" }
            }
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 3, // 過期版本
            Members = new MemberChangesDto
            {
                Update = new List<MemberUpdateDto>
                {
                    new()
                    {
                        RemoteId = existingBill.Members.First().Id,
                        Name = "更新名稱"
                    }
                }
            }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse(); // 衝突，但版本仍遞增

        // 即使有衝突，仍應發送通知讓其他用戶端知道新版本
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            6, // 版本從 5 遞增到 6
            userId);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_匿名用戶同步_應發送通知並帶null_userId()
    {
        // Arrange
        var billId = Guid.NewGuid();

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
            BillMeta = new BillMetaChangesDto { Name = "更新帳單名稱" }
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, null);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            2,
            null); // 匿名用戶
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_多次同步_應發送正確版本號()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Version = 10,
            Members = new List<Member>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new DeltaSyncRequest
        {
            BaseVersion = 10,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "m1", Name = "新成員" }
                }
            }
        };

        // Act
        await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            11, // 版本從 10 遞增到 11
            userId);
    }

    #endregion

    #region 帳單不存在測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_帳單不存在_不應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var request = new DeltaSyncRequest { BaseVersion = 1 };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();

        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    #endregion

    #region 無變更測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task DeltaSyncAsync_空請求_仍應遞增版本並發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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
            BaseVersion = 1
            // 無任何變更
        };

        // Act
        var result = await _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 即使無變更，版本仍會遞增（保證冪等性）
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            2,
            userId);
    }

    #endregion

    #region SyncBillAsync 通知測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SyncBillAsync_更新現有帳單_應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var localId = Guid.NewGuid().ToString();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "原帳單",
            Version = 1,
            OwnerId = userId,
            Members = new List<Member>(),
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = billId,
            BaseVersion = 1,
            Name = "更新後名稱",
            Members = new SyncMemberCollectionDto { Upsert = new List<SyncMemberDto>(), DeletedIds = new List<string>() },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            2, // 版本從 1 遞增到 2
            userId);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SyncBillAsync_新建帳單_應發送通知()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var localId = Guid.NewGuid().ToString();

        // 模擬冪等性檢查：沒有找到已存在的帳單
        _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync(localId, userId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = null, // 新帳單沒有 RemoteId
            BaseVersion = 0,
            Name = "新帳單",
            Members = new SyncMemberCollectionDto { Upsert = new List<SyncMemberDto>(), DeletedIds = new List<string>() },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // 新建帳單也應發送通知（讓同用戶其他瀏覽器知道）
        // 注意：Bill.Version 預設為 1，同步後遞增為 2
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            2, // Bill.Version 預設 1，遞增後為 2
            userId);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SyncBillAsync_版本衝突但ADD合併_仍應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var localId = Guid.NewGuid().ToString();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "原帳單",
            Version = 5, // 伺服器版本較新
            OwnerId = userId,
            Members = new List<Member>(),
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = billId,
            BaseVersion = 3, // 過期版本
            Name = "更新名稱",
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "新成員" } // ADD 操作應被合併
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // 即使有版本衝突，ADD 操作合併後仍應發送通知
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            6, // 版本從 5 遞增到 6
            userId);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SyncBillAsync_帳單不存在_不應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        var request = new SyncBillRequestDto
        {
            LocalId = Guid.NewGuid().ToString(),
            RemoteId = billId,
            BaseVersion = 1,
            Name = "帳單",
            Members = new SyncMemberCollectionDto { Upsert = new List<SyncMemberDto>(), DeletedIds = new List<string>() },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        var result = await _sut.SyncBillAsync(request, userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    #endregion

    #region UpdateAsync 通知測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task UpdateAsync_更新帳單名稱_應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "原名稱",
            Version = 1
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);
        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var dto = new UpdateBillDto("新名稱");

        // Act
        var result = await _sut.UpdateAsync(billId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            2, // 版本從 1 遞增到 2
            null); // UpdateAsync 沒有 userId 參數
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task UpdateAsync_帳單不存在_不應發送通知()
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
        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task UpdateAsync_多次更新_應發送正確版本號()
    {
        // Arrange
        var billId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "原名稱",
            Version = 10 // 已有多次更新
        };

        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);
        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        var dto = new UpdateBillDto("新名稱");

        // Act
        await _sut.UpdateAsync(billId, dto);

        // Assert
        await _notificationService.Received(1).NotifyBillUpdatedAsync(
            billId,
            11, // 版本從 10 遞增到 11
            null);
    }

    #endregion

    #region Transaction 失敗測試

    [Fact]
    [Trait("Category", "SignalR")]
    [Trait("Category", "Transaction")]
    public async Task SyncBillAsync_Transaction失敗_不應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var localId = Guid.NewGuid().ToString();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Version = 1,
            Members = new List<Member>(),
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);
        _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync(localId, userId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // 模擬 Transaction 執行時拋出異常
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("Database connection failed"));

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            RemoteId = billId,
            BaseVersion = 1,
            Name = "更新名稱",
            Members = new SyncMemberCollectionDto { Upsert = new List<SyncMemberDto>(), DeletedIds = new List<string>() },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        Func<Task> action = () => _sut.SyncBillAsync(request, userId);

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();

        // 關鍵：Transaction 失敗時，通知不應該被發送
        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    [Trait("Category", "Transaction")]
    public async Task DeltaSyncAsync_Transaction失敗_不應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingBill = new Bill
        {
            Id = billId,
            Name = "測試帳單",
            Version = 1,
            Members = new List<Member>(),
            Expenses = new List<Expense>()
        };

        _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, Arg.Any<CancellationToken>())
            .Returns(existingBill);

        // 模擬 Transaction 執行時拋出異常
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<DeltaSyncResponse>>>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Transaction rollback"));

        var request = new DeltaSyncRequest
        {
            BaseVersion = 1,
            Members = new MemberChangesDto
            {
                Add = new List<MemberAddDto>
                {
                    new() { LocalId = "m1", Name = "新成員" }
                }
            }
        };

        // Act
        Func<Task> action = () => _sut.DeltaSyncAsync(billId, request, userId);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();

        // 關鍵：Transaction 失敗時，通知不應該被發送
        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    [Trait("Category", "Transaction")]
    public async Task SyncBillAsync_SaveChanges失敗_不應發送通知()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var localId = Guid.NewGuid().ToString();

        // 模擬 GetByLocalClientIdAndOwnerAsync 返回 null（表示新帳單）
        _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync(localId, userId, Arg.Any<CancellationToken>())
            .Returns((Bill?)null);

        // 模擬 Transaction 內部的 SaveChangesAsync 失敗
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<SyncBillResponseDto>>>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("Unique constraint violation"));

        var request = new SyncBillRequestDto
        {
            LocalId = localId,
            Name = "新帳單",
            BaseVersion = 0,
            Members = new SyncMemberCollectionDto
            {
                Upsert = new List<SyncMemberDto>
                {
                    new() { LocalId = "m1", Name = "成員1", DisplayOrder = 0 }
                },
                DeletedIds = new List<string>()
            },
            Expenses = new SyncExpenseCollectionDto { Upsert = new List<SyncExpenseDto>(), DeletedIds = new List<string>() }
        };

        // Act
        Func<Task> action = () => _sut.SyncBillAsync(request, userId);

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();

        // 關鍵：SaveChanges 失敗時，通知不應該被發送
        await _notificationService.DidNotReceive().NotifyBillUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<long>(),
            Arg.Any<Guid?>());
    }

    #endregion
}
