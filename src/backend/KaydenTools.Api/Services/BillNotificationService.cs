using KaydenTools.Api.Hubs;
using KaydenTools.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace KaydenTools.Api.Services;

public class BillNotificationService(IHubContext<BillHub> hubContext) : IBillNotificationService
{
    private readonly IHubContext<BillHub> _hubContext = hubContext;

    public async Task NotifyBillUpdatedAsync(Guid billId, long newVersion, Guid? userId)
    {
        await _hubContext.Clients.Group($"bill_{billId}").SendAsync("BillUpdated", new
        {
            BillId = billId,
            NewVersion = newVersion,
            UpdatedBy = userId?.ToString() ?? "anonymous"
        });
    }
}
