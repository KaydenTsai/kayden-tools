using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KaydenTools.Api.Hubs;

/// <summary>
/// 帳單即時協作 Hub
/// </summary>
[Authorize]
public class BillHub : Hub
{
    private readonly IBillAuthService _billAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BillHub> _logger;
    private readonly IOperationService _operationService;
    private readonly IUnitOfWork _unitOfWork;

    public BillHub(
        IOperationService operationService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<BillHub> logger,
        IBillAuthService billAuthService)
    {
        _operationService = operationService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _billAuthService = billAuthService;
    }

    /// <summary>
    /// 加入帳單房間
    /// </summary>
    public async Task JoinBill(Guid billId)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue || !await _billAuthService.IsOwnerOrParticipantAsync(billId, userId.Value))
            throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(billId));
        _logger.LogInformation("Client {ConnectionId} joined bill {BillId}", Context.ConnectionId, billId);
    }

    /// <summary>
    /// 離開帳單房間
    /// </summary>
    public async Task LeaveBill(Guid billId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(billId));
    }

    /// <summary>
    /// 發送操作請求（同步返回結果，解決連續操作的競態條件）
    /// </summary>
    /// <returns>操作結果：成功時包含 OperationDto，失敗時包含錯誤訊息</returns>
    public async Task<OperationResultDto> SendOperation(OperationRequestDto request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue || !await _billAuthService.IsOwnerOrParticipantAsync(request.BillId, userId.Value))
                return new OperationResultDto(false, null, new OperationRejectedDto(
                    request.ClientId, "Forbidden", 0, new List<OperationDto>()));

            _logger.LogInformation("SendOperation received: OpType={OpType}, BillId={BillId}, TargetId={TargetId}",
                request.OpType, request.BillId, request.TargetId);

            _logger.LogInformation("Processing operation for user: {UserId}", userId);

            var result = await _operationService.ProcessOperationAsync(request, userId);

            if (result.IsSuccess)
            {
                var op = result.Value;
                _logger.LogInformation("Operation processed successfully, broadcasting to group");
                // 廣播給房間內其他人（不包含發送者，因為發送者會從返回值獲取）
                await Clients.OthersInGroup(GetGroupName(request.BillId)).SendAsync("OperationReceived", op);

                // 同步返回給發送者
                return new OperationResultDto(true, op, null);
            }

            _logger.LogWarning("Operation failed: {Error}", result.Error.Message);
            // 如果失敗 (例如衝突)，取得遺漏的操作和實際版本
            var latestOps = await _operationService.GetOperationsAsync(request.BillId, request.BaseVersion);

            // 使用 AsNoTracking 直接查詢資料庫，確保取得最新版本，不受 EF Core 快取影響
            var currentVersion = await _unitOfWork.Bills.GetCurrentVersionAsync(request.BillId)
                                 ?? request.BaseVersion + latestOps.Count;

            _logger.LogInformation(
                "Operation rejected: baseVersion={BaseVersion}, currentVersion={CurrentVersion}, missingOps={MissingOpsCount}",
                request.BaseVersion, currentVersion, latestOps.Count);

            var rejected = new OperationRejectedDto(
                request.ClientId,
                result.Error.Message,
                currentVersion,
                latestOps
            );

            // 同步返回錯誤給發送者
            return new OperationResultDto(false, null, rejected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing operation: {OpType} for bill {BillId}", request.OpType,
                request.BillId);
            throw;
        }
    }

    private static string GetGroupName(Guid billId)
    {
        return $"bill_{billId}";
    }
}
