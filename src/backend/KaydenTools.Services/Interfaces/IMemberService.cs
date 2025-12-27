using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface IMemberService
{
    Task<Result<MemberDto>> CreateAsync(Guid billId, CreateMemberDto dto, CancellationToken ct = default);
    Task<Result<MemberDto>> UpdateAsync(Guid id, UpdateMemberDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
