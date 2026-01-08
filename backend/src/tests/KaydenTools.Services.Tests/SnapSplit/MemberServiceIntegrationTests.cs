using FluentAssertions;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.TestUtilities.Database;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.Tests.SnapSplit;

/// <summary>
/// MemberService 整合測試
/// 使用真實資料庫驗證認領功能
/// </summary>
[Trait("Category", "Integration")]
public class MemberServiceIntegrationTests : DatabaseTestBase
{
    private IMemberService MemberService => GetService<IMemberService>();

    #region ClaimAsync 認領成員整合測試

    [Fact]
    public async Task ClaimAsync_應成功認領成員並寫入資料庫()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill = await SeedBill("認領測試帳單");
        var member = await SeedMember(bill, "待認領成員");

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await MemberService.ClaimAsync(member.Id, user.Id, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MemberId.Should().Be(member.Id);
        result.Value.LinkedUserId.Should().Be(user.Id);
        result.Value.LinkedUserDisplayName.Should().Be("測試使用者");
        result.Value.OriginalName.Should().Be("待認領成員");
        result.Value.Name.Should().Be("測試使用者");

        // 驗證資料庫
        ClearChangeTracker();
        var dbMember = await Db.Members.FindAsync(member.Id);
        dbMember!.LinkedUserId.Should().Be(user.Id);
        dbMember.OriginalName.Should().Be("待認領成員");
        dbMember.Name.Should().Be("測試使用者");
        dbMember.ClaimedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ClaimAsync_指定自訂名稱_應使用自訂名稱()
    {
        // Arrange
        var user = await SeedUser(displayName: "預設名稱");
        var bill = await SeedBill("認領測試帳單");
        var member = await SeedMember(bill, "待認領成員");

        var dto = new ClaimMemberDto("自訂名稱");

        // Act
        var result = await MemberService.ClaimAsync(member.Id, user.Id, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("自訂名稱");

        ClearChangeTracker();
        var dbMember = await Db.Members.FindAsync(member.Id);
        dbMember!.Name.Should().Be("自訂名稱");
    }

    [Fact]
    public async Task ClaimAsync_成員已被其他人認領_應回傳錯誤()
    {
        // Arrange
        var user1 = await SeedUser(displayName: "第一個使用者");
        var user2 = await SeedUser(displayName: "第二個使用者");
        var bill = await SeedBill("認領測試帳單");
        var member = await SeedMember(bill, "待認領成員", linkedUserId: user1.Id);

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await MemberService.ClaimAsync(member.Id, user2.Id, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberAlreadyClaimed);
    }

    [Fact]
    public async Task ClaimAsync_使用者已認領此帳單其他成員_應回傳錯誤()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill = await SeedBill("認領測試帳單");
        var member1 = await SeedMember(bill, "成員1", linkedUserId: user.Id, customize: m =>
        {
            m.ClaimedAt = DateTime.UtcNow;
        });
        var member2 = await SeedMember(bill, "成員2");

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await MemberService.ClaimAsync(member2.Id, user.Id, dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.UserAlreadyClaimedOther);
    }

    [Fact]
    public async Task ClaimAsync_同一使用者可認領不同帳單的成員()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill1 = await SeedBill("帳單1");
        var bill2 = await SeedBill("帳單2");
        var member1 = await SeedMember(bill1, "成員1", linkedUserId: user.Id, customize: m =>
        {
            m.ClaimedAt = DateTime.UtcNow;
        });
        var member2 = await SeedMember(bill2, "成員2");

        var dto = new ClaimMemberDto(null);

        // Act
        var result = await MemberService.ClaimAsync(member2.Id, user.Id, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LinkedUserId.Should().Be(user.Id);
    }

    #endregion

    #region UnclaimAsync 取消認領整合測試

    [Fact]
    public async Task UnclaimAsync_認領者本人取消_應成功取消並還原名稱()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill = await SeedBill("取消認領測試帳單");
        var member = await SeedMember(bill, "認領後名稱", linkedUserId: user.Id, customize: m =>
        {
            m.OriginalName = "原始名稱";
            m.ClaimedAt = DateTime.UtcNow;
        });

        // Act
        var result = await MemberService.UnclaimAsync(member.Id, user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        ClearChangeTracker();
        var dbMember = await Db.Members.FindAsync(member.Id);
        dbMember!.Name.Should().Be("原始名稱");
        dbMember.OriginalName.Should().BeNull();
        dbMember.LinkedUserId.Should().BeNull();
        dbMember.ClaimedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnclaimAsync_帳單擁有者取消他人認領_應成功()
    {
        // Arrange
        var owner = await SeedUser(displayName: "帳單擁有者");
        var claimer = await SeedUser(displayName: "認領者");
        var bill = await SeedBill("取消認領測試帳單", ownerId: owner.Id);
        var member = await SeedMember(bill, "認領後名稱", linkedUserId: claimer.Id, customize: m =>
        {
            m.OriginalName = "原始名稱";
            m.ClaimedAt = DateTime.UtcNow;
        });

        // Act - 使用帳單擁有者取消
        var result = await MemberService.UnclaimAsync(member.Id, owner.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        ClearChangeTracker();
        var dbMember = await Db.Members.FindAsync(member.Id);
        dbMember!.LinkedUserId.Should().BeNull();
    }

    [Fact]
    public async Task UnclaimAsync_非認領者且非擁有者_應回傳錯誤()
    {
        // Arrange
        var owner = await SeedUser(displayName: "帳單擁有者");
        var claimer = await SeedUser(displayName: "認領者");
        var randomUser = await SeedUser(displayName: "隨機使用者");
        var bill = await SeedBill("取消認領測試帳單", ownerId: owner.Id);
        var member = await SeedMember(bill, "認領後名稱", linkedUserId: claimer.Id, customize: m =>
        {
            m.ClaimedAt = DateTime.UtcNow;
        });

        // Act - 使用隨機使用者取消
        var result = await MemberService.UnclaimAsync(member.Id, randomUser.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.UnauthorizedUnclaim);
    }

    [Fact]
    public async Task UnclaimAsync_成員未被認領_應回傳錯誤()
    {
        // Arrange
        var user = await SeedUser();
        var bill = await SeedBill("測試帳單");
        var member = await SeedMember(bill, "未認領成員");

        // Act
        var result = await MemberService.UnclaimAsync(member.Id, user.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MemberNotClaimed);
    }

    #endregion

    #region 認領與同步整合測試

    [Fact]
    public async Task ClaimAsync_認領後帳單應可透過LinkedUserId查詢()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill = await SeedBill("認領測試帳單");
        var member = await SeedMember(bill, "待認領成員");

        var dto = new ClaimMemberDto(null);

        // Act
        await MemberService.ClaimAsync(member.Id, user.Id, dto);

        // Assert
        ClearChangeTracker();
        var linkedBills = await Db.Bills
            .Where(b => b.Members.Any(m => m.LinkedUserId == user.Id))
            .ToListAsync();

        linkedBills.Should().HaveCount(1);
        linkedBills.First().Id.Should().Be(bill.Id);
    }

    [Fact]
    public async Task ClaimAsync_認領多個帳單後應可正確查詢()
    {
        // Arrange
        var user = await SeedUser(displayName: "測試使用者");
        var bill1 = await SeedBill("帳單1");
        var bill2 = await SeedBill("帳單2");
        var bill3 = await SeedBill("帳單3");
        var member1 = await SeedMember(bill1, "成員1");
        var member2 = await SeedMember(bill2, "成員2");
        var member3 = await SeedMember(bill3, "成員3"); // 不認領這個

        var dto = new ClaimMemberDto(null);

        // Act
        await MemberService.ClaimAsync(member1.Id, user.Id, dto);
        await MemberService.ClaimAsync(member2.Id, user.Id, dto);

        // Assert
        ClearChangeTracker();
        var linkedBills = await Db.Bills
            .Where(b => b.Members.Any(m => m.LinkedUserId == user.Id))
            .ToListAsync();

        linkedBills.Should().HaveCount(2);
        linkedBills.Select(b => b.Id).Should().Contain(new[] { bill1.Id, bill2.Id });
        linkedBills.Select(b => b.Id).Should().NotContain(bill3.Id);
    }

    #endregion
}
