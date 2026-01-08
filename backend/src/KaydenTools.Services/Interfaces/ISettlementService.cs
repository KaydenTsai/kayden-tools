using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface ISettlementService
{
    Task<Result<SettlementResultDto>> CalculateAsync(Guid billId, CancellationToken ct = default);
    Task<Result> ToggleSettledAsync(Guid billId, Guid fromMemberId, Guid toMemberId, CancellationToken ct = default);
}
