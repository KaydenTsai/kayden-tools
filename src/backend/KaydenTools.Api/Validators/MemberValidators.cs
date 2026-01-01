using FluentValidation;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Api.Validators;

/// <summary>
/// 建立成員請求驗證器
/// </summary>
public class CreateMemberDtoValidator : AbstractValidator<CreateMemberDto>
{
    public CreateMemberDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("成員名稱不可為空")
            .MaximumLength(100).WithMessage("成員名稱不可超過 100 字元");
    }
}

/// <summary>
/// 更新成員請求驗證器
/// </summary>
public class UpdateMemberDtoValidator : AbstractValidator<UpdateMemberDto>
{
    public UpdateMemberDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("成員名稱不可為空")
            .MaximumLength(100).WithMessage("成員名稱不可超過 100 字元");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("顯示順序必須大於或等於 0");
    }
}

/// <summary>
/// 認領成員請求驗證器
/// </summary>
public class ClaimMemberDtoValidator : AbstractValidator<ClaimMemberDto>
{
    public ClaimMemberDtoValidator()
    {
        When(x => x.DisplayName != null, () =>
        {
            RuleFor(x => x.DisplayName)
                .MaximumLength(100).WithMessage("顯示名稱不可超過 100 字元");
        });
    }
}
