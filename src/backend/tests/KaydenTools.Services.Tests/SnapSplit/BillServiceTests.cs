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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBillNotificationService _notificationService;
    private readonly BillService _sut; // System Under Test

    public BillServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notificationService = Substitute.For<IBillNotificationService>();
        
        // 模擬事務執行：直接執行傳入的 Action
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<Task<Result<DeltaSyncResponse>>>>(), Arg.Any<CancellationToken>())
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
                    new MemberAddDto { LocalId = localId, Name = memberName }
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
                new Member { Id = memberId, Name = "伺服器名稱", DisplayOrder = 1 }
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
                    new MemberUpdateDto 
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
                    new MemberAddDto { LocalId = "new-mem", Name = "新朋友" } 
                }
            },
            Expenses = new ExpenseChangesDto
            {
                Add = new List<ExpenseAddDto>
                {
                    new ExpenseAddDto
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
                    new ExpenseUpdateDto
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
                    new DeltaSettlementDto
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
                    new DeltaSettlementDto
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
}
