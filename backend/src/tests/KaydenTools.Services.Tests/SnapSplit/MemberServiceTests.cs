using FluentAssertions;
using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.SnapSplit;
using NSubstitute;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// MemberService 認領成員測試
/// </summary>
public class MemberServiceTests
{
    private readonly MemberService _sut;
    private readonly IUnitOfWork _unitOfWork;

    public MemberServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new MemberService(_unitOfWork);
    }

    #region ClaimAsync 認領成員測試

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_成員未被認領_應成功認領並更新資料()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "原始名稱",
            LinkedUserId = null
        };

        var user = new User
        {
            Id = userId,
            DisplayName = "使用者顯示名稱",
            AvatarUrl = "https://example.com/avatar.png"
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MemberId.Should().Be(memberId);
        result.Value.LinkedUserId.Should().Be(userId);
        result.Value.LinkedUserDisplayName.Should().Be("使用者顯示名稱");

        member.LinkedUserId.Should().Be(userId);
        member.OriginalName.Should().Be("原始名稱");
        member.Name.Should().Be("使用者顯示名稱");
        member.ClaimedAt.Should().NotBeNull();

        _unitOfWork.Members.Received(1).Update(member);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_指定顯示名稱_應使用指定名稱()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "原始名稱",
            LinkedUserId = null
        };

        var user = new User
        {
            Id = userId,
            DisplayName = "使用者預設名稱"
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var dto = new ClaimMemberDto("自訂顯示名稱");

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        member.Name.Should().Be("自訂顯示名稱");
        member.OriginalName.Should().Be("原始名稱");
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_成員已被其他人認領_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            Name = "已認領成員",
            LinkedUserId = otherUserId // 已被其他人認領
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberAlreadyClaimed);
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_使用者已認領此帳單其他成員_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var targetMember = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "目標成員",
            LinkedUserId = null
        };

        var alreadyClaimedMember = new Member
        {
            Id = otherMemberId,
            BillId = billId,
            Name = "已認領成員",
            LinkedUserId = userId // 此使用者已認領這個成員
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(targetMember);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { targetMember, alreadyClaimedMember });

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.UserAlreadyClaimedOther);
        result.Error.Message.Should().Contain("已認領成員");
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_成員不存在_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_使用者不存在_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "成員",
            LinkedUserId = null
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task ClaimAsync_重複認領同一成員_應保留原始名稱()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "第一次認領後名稱",
            OriginalName = "最初的原始名稱", // 已有原始名稱
            LinkedUserId = null
        };

        var user = new User
        {
            Id = userId,
            DisplayName = "新使用者名稱"
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Members.GetByBillIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await _sut.ClaimAsync(memberId, userId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        member.OriginalName.Should().Be("最初的原始名稱"); // 應保留原始名稱
        member.Name.Should().Be("新使用者名稱");
    }

    #endregion

    #region UnclaimAsync 取消認領測試

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task UnclaimAsync_認領者本人取消_應成功取消並還原名稱()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "認領後名稱",
            OriginalName = "原始名稱",
            LinkedUserId = userId,
            ClaimedAt = DateTime.UtcNow
        };

        var bill = new Bill
        {
            Id = billId,
            OwnerId = Guid.NewGuid() // 不是帳單擁有者
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act
        var result = await _sut.UnclaimAsync(memberId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        member.Name.Should().Be("原始名稱");
        member.OriginalName.Should().BeNull();
        member.LinkedUserId.Should().BeNull();
        member.ClaimedAt.Should().BeNull();

        _unitOfWork.Members.Received(1).Update(member);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task UnclaimAsync_帳單擁有者取消他人認領_應成功取消()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var claimerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "認領後名稱",
            OriginalName = "原始名稱",
            LinkedUserId = claimerId,
            ClaimedAt = DateTime.UtcNow
        };

        var bill = new Bill
        {
            Id = billId,
            OwnerId = ownerId // 帳單擁有者
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act - 使用帳單擁有者的 userId
        var result = await _sut.UnclaimAsync(memberId, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        member.LinkedUserId.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task UnclaimAsync_非認領者且非擁有者_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var claimerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var randomUserId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            BillId = billId,
            Name = "認領後名稱",
            LinkedUserId = claimerId
        };

        var bill = new Bill
        {
            Id = billId,
            OwnerId = ownerId
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _unitOfWork.Bills.GetByIdAsync(billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Act - 使用隨機使用者的 userId
        var result = await _sut.UnclaimAsync(memberId, randomUserId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.UnauthorizedUnclaim);
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task UnclaimAsync_成員未被認領_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = new Member
        {
            Id = memberId,
            Name = "未認領成員",
            LinkedUserId = null
        };

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        var result = await _sut.UnclaimAsync(memberId, userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotClaimed);
    }

    [Fact]
    [Trait("Category", "ClaimMember")]
    public async Task UnclaimAsync_成員不存在_應回傳錯誤()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _unitOfWork.Members.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns((Member?)null);

        // Act
        var result = await _sut.UnclaimAsync(memberId, userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotFound);
    }

    #endregion
}
