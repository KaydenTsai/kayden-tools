using System.Text.Json;
using FluentAssertions;
using Kayden.Commons.Common;
using KaydenTools.Api.Hubs;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KaydenTools.Services.Tests.SignalR;

/// <summary>
/// BillHub SignalR Hub 測試
/// </summary>
public class BillHubTests
{
    private readonly BillHub _sut;
    private readonly IOperationService _operationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BillHub> _logger;
    private readonly IHubCallerClients _clients;
    private readonly IGroupManager _groups;
    private readonly HubCallerContext _context;
    private readonly IClientProxy _groupClientProxy;
    private readonly IClientProxy _othersInGroupProxy;

    public BillHubTests()
    {
        _operationService = Substitute.For<IOperationService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<BillHub>>();
        _clients = Substitute.For<IHubCallerClients>();
        _groups = Substitute.For<IGroupManager>();
        _context = Substitute.For<HubCallerContext>();
        _groupClientProxy = Substitute.For<IClientProxy>();
        _othersInGroupProxy = Substitute.For<IClientProxy>();

        _context.ConnectionId.Returns("test-connection-id");
        _clients.OthersInGroup(Arg.Any<string>()).Returns(_othersInGroupProxy);

        _sut = new BillHub(_operationService, _currentUserService, _unitOfWork, _logger)
        {
            Clients = _clients,
            Groups = _groups,
            Context = _context
        };
    }

    #region JoinBill / LeaveBill 測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task JoinBill_應將連線加入正確群組()
    {
        // Arrange
        var billId = Guid.NewGuid();

        // Act
        await _sut.JoinBill(billId);

        // Assert
        await _groups.Received(1).AddToGroupAsync(
            "test-connection-id",
            $"bill_{billId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task LeaveBill_應將連線從群組移除()
    {
        // Arrange
        var billId = Guid.NewGuid();

        // Act
        await _sut.LeaveBill(billId);

        // Assert
        await _groups.Received(1).RemoveFromGroupAsync(
            "test-connection-id",
            $"bill_{billId}",
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region SendOperation 成功測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_操作成功_應回傳成功結果並廣播給群組()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "UPDATE_MEMBER",
            Guid.NewGuid(),
            JsonDocument.Parse("{}").RootElement,
            1
        );

        var operationDto = new OperationDto(
            operationId,
            billId,
            2,
            "UPDATE_MEMBER",
            request.TargetId,
            JsonDocument.Parse("{}"),
            userId,
            "client-123",
            DateTime.UtcNow
        );

        _operationService.ProcessOperationAsync(request, userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(operationDto));

        // Act
        var result = await _sut.SendOperation(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Operation.Should().NotBeNull();
        result.Operation!.Id.Should().Be(operationId);
        result.Rejected.Should().BeNull();

        // 驗證廣播給群組內其他人
        await _othersInGroupProxy.Received(1).SendCoreAsync(
            "OperationReceived",
            Arg.Is<object[]>(args => args.Length == 1 && ((OperationDto)args[0]).Id == operationDto.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_匿名使用者_應使用null作為userId()
    {
        // Arrange
        var billId = Guid.NewGuid();

        _currentUserService.UserId.Returns((Guid?)null);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "ADD_MEMBER",
            null,
            JsonDocument.Parse("{}").RootElement,
            1
        );

        var operationDto = new OperationDto(
            Guid.NewGuid(),
            billId,
            2,
            "ADD_MEMBER",
            null,
            JsonDocument.Parse("{}"),
            null,
            "client-123",
            DateTime.UtcNow
        );

        _operationService.ProcessOperationAsync(request, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(operationDto));

        // Act
        var result = await _sut.SendOperation(request);

        // Assert
        result.Success.Should().BeTrue();
        await _operationService.Received(1).ProcessOperationAsync(request, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region SendOperation 衝突測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_操作衝突_應回傳拒絕結果與遺漏操作()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "UPDATE_EXPENSE",
            Guid.NewGuid(),
            JsonDocument.Parse("{}").RootElement,
            1 // 過期版本
        );

        _operationService.ProcessOperationAsync(request, userId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<OperationDto>("CONFLICT", "版本衝突"));

        // 遺漏的操作
        var missingOps = new List<OperationDto>
        {
            new(
                Guid.NewGuid(),
                billId,
                2,
                "UPDATE_MEMBER",
                Guid.NewGuid(),
                JsonDocument.Parse("{}"),
                Guid.NewGuid(),
                "other-client",
                DateTime.UtcNow)
        };

        _operationService.GetOperationsAsync(billId, 1, Arg.Any<CancellationToken>())
            .Returns(missingOps);

        _unitOfWork.Bills.GetCurrentVersionAsync(billId, Arg.Any<CancellationToken>())
            .Returns(3L);

        // Act
        var result = await _sut.SendOperation(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Operation.Should().BeNull();
        result.Rejected.Should().NotBeNull();
        result.Rejected!.ClientId.Should().Be("client-123");
        result.Rejected.CurrentVersion.Should().Be(3);
        result.Rejected.MissingOperations.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_衝突但無法取得版本_應使用估算版本()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "UPDATE_EXPENSE",
            Guid.NewGuid(),
            JsonDocument.Parse("{}").RootElement,
            5
        );

        _operationService.ProcessOperationAsync(request, userId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<OperationDto>("CONFLICT", "版本衝突"));

        var missingOps = new List<OperationDto>
        {
            new(Guid.NewGuid(), billId, 6, "OP1", null, JsonDocument.Parse("{}"), null, "c1", DateTime.UtcNow),
            new(Guid.NewGuid(), billId, 7, "OP2", null, JsonDocument.Parse("{}"), null, "c2", DateTime.UtcNow)
        };

        _operationService.GetOperationsAsync(billId, 5, Arg.Any<CancellationToken>())
            .Returns(missingOps);

        _unitOfWork.Bills.GetCurrentVersionAsync(billId, Arg.Any<CancellationToken>())
            .Returns((long?)null); // 無法取得

        // Act
        var result = await _sut.SendOperation(request);

        // Assert
        result.Rejected.Should().NotBeNull();
        result.Rejected!.CurrentVersion.Should().Be(7); // baseVersion + missingOps.Count
    }

    #endregion

    #region 廣播行為測試

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_成功時_不應廣播給發送者()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "ADD_EXPENSE",
            null,
            JsonDocument.Parse("{}").RootElement,
            1
        );

        var operationDto = new OperationDto(
            Guid.NewGuid(),
            billId,
            2,
            "ADD_EXPENSE",
            null,
            JsonDocument.Parse("{}"),
            userId,
            "client-123",
            DateTime.UtcNow
        );

        _operationService.ProcessOperationAsync(request, userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(operationDto));

        // Act
        await _sut.SendOperation(request);

        // Assert - 驗證使用 OthersInGroup 而非 Group (不包含發送者)
        _clients.Received(1).OthersInGroup($"bill_{billId}");
        _clients.DidNotReceive().Group(Arg.Any<string>());
    }

    [Fact]
    [Trait("Category", "SignalR")]
    public async Task SendOperation_失敗時_不應廣播任何訊息()
    {
        // Arrange
        var billId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var request = new OperationRequestDto(
            "client-123",
            billId,
            "UPDATE_EXPENSE",
            Guid.NewGuid(),
            JsonDocument.Parse("{}").RootElement,
            1
        );

        _operationService.ProcessOperationAsync(request, userId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<OperationDto>("CONFLICT", "版本衝突"));

        _operationService.GetOperationsAsync(billId, 1, Arg.Any<CancellationToken>())
            .Returns(new List<OperationDto>());

        _unitOfWork.Bills.GetCurrentVersionAsync(billId, Arg.Any<CancellationToken>())
            .Returns(2L);

        // Act
        await _sut.SendOperation(request);

        // Assert - 失敗時不應廣播
        await _othersInGroupProxy.DidNotReceive().SendCoreAsync(
            "OperationReceived",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
