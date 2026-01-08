using FluentAssertions;
using KaydenTools.Api.Hubs;
using KaydenTools.Api.Services;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace KaydenTools.Services.Tests.SignalR;

/// <summary>
/// BillNotificationService 通知服務測試
/// </summary>
public class BillNotificationServiceTests
{
    private readonly IHubContext<BillHub> _hubContext;
    private readonly IClientProxy _clientProxy;
    private readonly IHubClients _hubClients;
    private readonly BillNotificationService _sut;

    public BillNotificationServiceTests()
    {
        _hubContext = Substitute.For<IHubContext<BillHub>>();
        _hubClients = Substitute.For<IHubClients>();
        _clientProxy = Substitute.For<IClientProxy>();

        _hubContext.Clients.Returns(_hubClients);
        _hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);

        _sut = new BillNotificationService(_hubContext);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task NotifyBillUpdatedAsync_應發送通知到正確群組()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var newVersion = 5L;
        var userId = Guid.NewGuid();

        // Act
        await _sut.NotifyBillUpdatedAsync(billId, newVersion, userId);

        // Assert
        _hubClients.Received(1).Group($"bill_{billId}");
        await _clientProxy.Received(1).SendCoreAsync(
            "BillUpdated",
            Arg.Is<object[]>(args =>
                args.Length == 1 &&
                args[0] != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task NotifyBillUpdatedAsync_傳送正確的資料格式()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var newVersion = 10L;
        var userId = Guid.NewGuid();

        object? capturedPayload = null;
        await _clientProxy.SendCoreAsync(
            Arg.Any<string>(),
            Arg.Do<object[]>(args => capturedPayload = args[0]),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.NotifyBillUpdatedAsync(billId, newVersion, userId);

        // Assert
        capturedPayload.Should().NotBeNull();

        var payloadType = capturedPayload!.GetType();
        var billIdProp = payloadType.GetProperty("BillId")?.GetValue(capturedPayload);
        var newVersionProp = payloadType.GetProperty("NewVersion")?.GetValue(capturedPayload);
        var updatedByProp = payloadType.GetProperty("UpdatedBy")?.GetValue(capturedPayload);

        billIdProp.Should().Be(billId);
        newVersionProp.Should().Be(newVersion);
        updatedByProp.Should().Be(userId.ToString());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task NotifyBillUpdatedAsync_匿名使用者_UpdatedBy應為anonymous()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var newVersion = 3L;
        Guid? userId = null;

        object? capturedPayload = null;
        await _clientProxy.SendCoreAsync(
            Arg.Any<string>(),
            Arg.Do<object[]>(args => capturedPayload = args[0]),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.NotifyBillUpdatedAsync(billId, newVersion, userId);

        // Assert
        capturedPayload.Should().NotBeNull();

        var payloadType = capturedPayload!.GetType();
        var updatedByProp = payloadType.GetProperty("UpdatedBy")?.GetValue(capturedPayload);

        updatedByProp.Should().Be("anonymous");
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task NotifyBillUpdatedAsync_不同帳單_應發送到不同群組()
    {
        // Arrange
        var billId1 = Guid.NewGuid();
        var billId2 = Guid.NewGuid();

        // Act
        await _sut.NotifyBillUpdatedAsync(billId1, 1, null);
        await _sut.NotifyBillUpdatedAsync(billId2, 1, null);

        // Assert
        _hubClients.Received(1).Group($"bill_{billId1}");
        _hubClients.Received(1).Group($"bill_{billId2}");
    }
}
