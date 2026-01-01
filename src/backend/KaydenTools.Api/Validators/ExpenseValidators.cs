using FluentValidation;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Api.Validators;

/// <summary>
/// 建立費用請求驗證器
/// </summary>
public class CreateExpenseDtoValidator : AbstractValidator<CreateExpenseDto>
{
    public CreateExpenseDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("費用名稱不可為空")
            .MaximumLength(200).WithMessage("費用名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(999999999.99m).WithMessage("金額不可超過 999,999,999.99");

        RuleFor(x => x.ServiceFeePercent)
            .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
            .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");

        When(x => x.PaidById.HasValue, () =>
        {
            RuleFor(x => x.PaidById)
                .Must(id => id != Guid.Empty).WithMessage("付款者 ID 不可為空的 GUID");
        });

        RuleFor(x => x.ParticipantIds)
            .NotNull().WithMessage("參與者清單不可為空");

        RuleForEach(x => x.ParticipantIds)
            .Must(id => id != Guid.Empty).WithMessage("參與者 ID 不可為空的 GUID");

        When(x => x.IsItemized && x.Items != null, () =>
        {
            RuleForEach(x => x.Items!)
                .SetValidator(new CreateExpenseItemDtoValidator());
        });
    }
}

/// <summary>
/// 更新費用請求驗證器
/// </summary>
public class UpdateExpenseDtoValidator : AbstractValidator<UpdateExpenseDto>
{
    public UpdateExpenseDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("費用名稱不可為空")
            .MaximumLength(200).WithMessage("費用名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(999999999.99m).WithMessage("金額不可超過 999,999,999.99");

        RuleFor(x => x.ServiceFeePercent)
            .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
            .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");

        When(x => x.PaidById.HasValue, () =>
        {
            RuleFor(x => x.PaidById)
                .Must(id => id != Guid.Empty).WithMessage("付款者 ID 不可為空的 GUID");
        });

        RuleFor(x => x.ParticipantIds)
            .NotNull().WithMessage("參與者清單不可為空");

        RuleForEach(x => x.ParticipantIds)
            .Must(id => id != Guid.Empty).WithMessage("參與者 ID 不可為空的 GUID");

        When(x => x.Items != null, () =>
        {
            RuleForEach(x => x.Items!)
                .SetValidator(new CreateExpenseItemDtoValidator());
        });
    }
}

/// <summary>
/// 建立費用細項請求驗證器
/// </summary>
public class CreateExpenseItemDtoValidator : AbstractValidator<CreateExpenseItemDto>
{
    public CreateExpenseItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("細項名稱不可為空")
            .MaximumLength(200).WithMessage("細項名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(999999999.99m).WithMessage("金額不可超過 999,999,999.99");

        When(x => x.PaidById.HasValue, () =>
        {
            RuleFor(x => x.PaidById)
                .Must(id => id != Guid.Empty).WithMessage("付款者 ID 不可為空的 GUID");
        });

        RuleFor(x => x.ParticipantIds)
            .NotNull().WithMessage("參與者清單不可為空");

        RuleForEach(x => x.ParticipantIds)
            .Must(id => id != Guid.Empty).WithMessage("參與者 ID 不可為空的 GUID");
    }
}
