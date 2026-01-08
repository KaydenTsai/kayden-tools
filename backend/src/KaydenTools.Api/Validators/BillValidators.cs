using FluentValidation;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Api.Validators;

/// <summary>
/// 建立帳單請求驗證器
/// </summary>
public class CreateBillDtoValidator : AbstractValidator<CreateBillDto>
{
    public CreateBillDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("帳單名稱不可為空")
            .MaximumLength(200).WithMessage("帳單名稱不可超過 200 字元");
    }
}

/// <summary>
/// 更新帳單請求驗證器
/// </summary>
public class UpdateBillDtoValidator : AbstractValidator<UpdateBillDto>
{
    public UpdateBillDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("帳單名稱不可為空")
            .MaximumLength(200).WithMessage("帳單名稱不可超過 200 字元");
    }
}

/// <summary>
/// 同步帳單請求驗證器
/// </summary>
public class SyncBillRequestDtoValidator : AbstractValidator<SyncBillRequestDto>
{
    public SyncBillRequestDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地帳單 ID 不可為空");

        RuleFor(x => x.BaseVersion)
            .GreaterThanOrEqualTo(0).WithMessage("基底版本號必須大於或等於 0");

        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name)
                .MaximumLength(200).WithMessage("帳單名稱不可超過 200 字元");
        });

        RuleFor(x => x.Members)
            .NotNull().WithMessage("成員同步集合不可為空");

        RuleFor(x => x.Expenses)
            .NotNull().WithMessage("費用同步集合不可為空");

        RuleForEach(x => x.Members.Upsert)
            .SetValidator(new SyncMemberDtoValidator());

        RuleForEach(x => x.Expenses.Upsert)
            .SetValidator(new SyncExpenseDtoValidator());
    }
}

/// <summary>
/// 同步成員資料驗證器
/// </summary>
public class SyncMemberDtoValidator : AbstractValidator<SyncMemberDto>
{
    public SyncMemberDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地成員 ID 不可為空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("成員名稱不可為空")
            .MaximumLength(100).WithMessage("成員名稱不可超過 100 字元");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("顯示順序必須大於或等於 0");
    }
}

/// <summary>
/// 同步費用資料驗證器
/// </summary>
public class SyncExpenseDtoValidator : AbstractValidator<SyncExpenseDto>
{
    public SyncExpenseDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地費用 ID 不可為空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("費用名稱不可為空")
            .MaximumLength(200).WithMessage("費用名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(999999999.99m).WithMessage("金額不可超過 999,999,999.99");

        RuleFor(x => x.ServiceFeePercent)
            .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
            .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");

        When(x => x.IsItemized && x.Items != null, () =>
        {
            RuleForEach(x => x.Items!.Upsert)
                .SetValidator(new SyncExpenseItemDtoValidator());
        });
    }
}

/// <summary>
/// 同步費用細項資料驗證器
/// </summary>
public class SyncExpenseItemDtoValidator : AbstractValidator<SyncExpenseItemDto>
{
    public SyncExpenseItemDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地細項 ID 不可為空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("細項名稱不可為空")
            .MaximumLength(200).WithMessage("細項名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(999999999.99m).WithMessage("金額不可超過 999,999,999.99");
    }
}
