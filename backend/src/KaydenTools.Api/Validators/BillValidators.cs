using FluentValidation;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Api.Validators;

#region Validation Constants

public static class ValidationLimits
{
    public const int MaxMembers = 50;
    public const int MaxExpenses = 200;
    public const int MaxExpenseItems = 200;
    public const int MaxSettlements = 100;
    public const int MaxDeleteIds = 200;
    public const int MaxParticipantIds = 50;
    public const int MaxLocalIdLength = 100;
    public const int MaxNameLength = 200;
    public const int MaxMemberNameLength = 100;
    public const decimal MaxAmount = 999_999_999.99m;
}

#endregion

#region Bill CRUD Validators

/// <summary>
/// 建立帳單請求驗證器
/// </summary>
public class CreateBillDtoValidator : AbstractValidator<CreateBillDto>
{
    public CreateBillDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("帳單名稱不可為空")
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("帳單名稱不可超過 200 字元");
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
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("帳單名稱不可超過 200 字元");
    }
}

#endregion

#region SyncBill Validators

/// <summary>
/// 同步帳單請求驗證器
/// </summary>
public class SyncBillRequestDtoValidator : AbstractValidator<SyncBillRequestDto>
{
    public SyncBillRequestDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地帳單 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地帳單 ID 不可超過 100 字元");

        RuleFor(x => x.BaseVersion)
            .GreaterThanOrEqualTo(0).WithMessage("基底版本號必須大於或等於 0");

        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name)
                .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("帳單名稱不可超過 200 字元");
        });

        RuleFor(x => x.Members)
            .NotNull().WithMessage("成員同步集合不可為空");

        RuleFor(x => x.Expenses)
            .NotNull().WithMessage("費用同步集合不可為空");

        RuleFor(x => x.Members.Upsert)
            .Must(x => x.Count <= ValidationLimits.MaxMembers)
            .WithMessage($"成員數量不可超過 {ValidationLimits.MaxMembers}");

        RuleFor(x => x.Members.DeletedIds)
            .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
            .WithMessage($"刪除成員 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");

        RuleFor(x => x.Expenses.Upsert)
            .Must(x => x.Count <= ValidationLimits.MaxExpenses)
            .WithMessage($"費用數量不可超過 {ValidationLimits.MaxExpenses}");

        RuleFor(x => x.Expenses.DeletedIds)
            .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
            .WithMessage($"刪除費用 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");

        When(x => x.SettledTransfers != null, () =>
        {
            RuleFor(x => x.SettledTransfers!)
                .Must(x => x.Count <= ValidationLimits.MaxSettlements)
                .WithMessage($"結算數量不可超過 {ValidationLimits.MaxSettlements}");
        });

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
            .NotEmpty().WithMessage("本地成員 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地成員 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("成員名稱不可為空")
            .MaximumLength(ValidationLimits.MaxMemberNameLength).WithMessage("成員名稱不可超過 100 字元");

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
            .NotEmpty().WithMessage("本地費用 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地費用 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("費用名稱不可為空")
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("費用名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");

        RuleFor(x => x.ServiceFeePercent)
            .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
            .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");

        RuleFor(x => x.ParticipantLocalIds)
            .Must(x => x.Count <= ValidationLimits.MaxParticipantIds)
            .WithMessage($"參與者數量不可超過 {ValidationLimits.MaxParticipantIds}");

        When(x => x.IsItemized && x.Items != null, () =>
        {
            RuleFor(x => x.Items!.Upsert)
                .Must(x => x.Count <= ValidationLimits.MaxExpenseItems)
                .WithMessage($"細項數量不可超過 {ValidationLimits.MaxExpenseItems}");

            RuleFor(x => x.Items!.DeletedIds)
                .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
                .WithMessage($"刪除細項 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");

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
            .NotEmpty().WithMessage("本地細項 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地細項 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("細項名稱不可為空")
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("細項名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");
    }
}

#endregion

#region DeltaSync Validators

/// <summary>
/// Delta 同步請求驗證器
/// </summary>
public class DeltaSyncRequestValidator : AbstractValidator<DeltaSyncRequest>
{
    public DeltaSyncRequestValidator()
    {
        RuleFor(x => x.BaseVersion)
            .GreaterThanOrEqualTo(0).WithMessage("基底版本號必須大於或等於 0");

        // Members
        When(x => x.Members != null, () =>
        {
            When(x => x.Members!.Add != null, () =>
            {
                RuleFor(x => x.Members!.Add!)
                    .Must(x => x.Count <= ValidationLimits.MaxMembers)
                    .WithMessage($"新增成員數量不可超過 {ValidationLimits.MaxMembers}");

                RuleForEach(x => x.Members!.Add!)
                    .SetValidator(new MemberAddDtoValidator());
            });

            When(x => x.Members!.Update != null, () =>
            {
                RuleFor(x => x.Members!.Update!)
                    .Must(x => x.Count <= ValidationLimits.MaxMembers)
                    .WithMessage($"更新成員數量不可超過 {ValidationLimits.MaxMembers}");

                RuleForEach(x => x.Members!.Update!)
                    .SetValidator(new MemberUpdateDtoValidator());
            });

            When(x => x.Members!.Delete != null, () =>
            {
                RuleFor(x => x.Members!.Delete!)
                    .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
                    .WithMessage($"刪除成員 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");
            });
        });

        // Expenses
        When(x => x.Expenses != null, () =>
        {
            When(x => x.Expenses!.Add != null, () =>
            {
                RuleFor(x => x.Expenses!.Add!)
                    .Must(x => x.Count <= ValidationLimits.MaxExpenses)
                    .WithMessage($"新增費用數量不可超過 {ValidationLimits.MaxExpenses}");

                RuleForEach(x => x.Expenses!.Add!)
                    .SetValidator(new ExpenseAddDtoValidator());
            });

            When(x => x.Expenses!.Update != null, () =>
            {
                RuleFor(x => x.Expenses!.Update!)
                    .Must(x => x.Count <= ValidationLimits.MaxExpenses)
                    .WithMessage($"更新費用數量不可超過 {ValidationLimits.MaxExpenses}");

                RuleForEach(x => x.Expenses!.Update!)
                    .SetValidator(new ExpenseUpdateDtoValidator());
            });

            When(x => x.Expenses!.Delete != null, () =>
            {
                RuleFor(x => x.Expenses!.Delete!)
                    .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
                    .WithMessage($"刪除費用 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");
            });
        });

        // ExpenseItems
        When(x => x.ExpenseItems != null, () =>
        {
            When(x => x.ExpenseItems!.Add != null, () =>
            {
                RuleFor(x => x.ExpenseItems!.Add!)
                    .Must(x => x.Count <= ValidationLimits.MaxExpenseItems)
                    .WithMessage($"新增細項數量不可超過 {ValidationLimits.MaxExpenseItems}");

                RuleForEach(x => x.ExpenseItems!.Add!)
                    .SetValidator(new ExpenseItemAddDtoValidator());
            });

            When(x => x.ExpenseItems!.Update != null, () =>
            {
                RuleFor(x => x.ExpenseItems!.Update!)
                    .Must(x => x.Count <= ValidationLimits.MaxExpenseItems)
                    .WithMessage($"更新細項數量不可超過 {ValidationLimits.MaxExpenseItems}");

                RuleForEach(x => x.ExpenseItems!.Update!)
                    .SetValidator(new ExpenseItemUpdateDtoValidator());
            });

            When(x => x.ExpenseItems!.Delete != null, () =>
            {
                RuleFor(x => x.ExpenseItems!.Delete!)
                    .Must(x => x.Count <= ValidationLimits.MaxDeleteIds)
                    .WithMessage($"刪除細項 ID 數量不可超過 {ValidationLimits.MaxDeleteIds}");
            });
        });

        // Settlements
        When(x => x.Settlements != null, () =>
        {
            When(x => x.Settlements!.Mark != null, () =>
            {
                RuleFor(x => x.Settlements!.Mark!)
                    .Must(x => x.Count <= ValidationLimits.MaxSettlements)
                    .WithMessage($"標記結算數量不可超過 {ValidationLimits.MaxSettlements}");

                RuleForEach(x => x.Settlements!.Mark!)
                    .SetValidator(new DeltaSettlementDtoValidator());
            });

            When(x => x.Settlements!.Unmark != null, () =>
            {
                RuleFor(x => x.Settlements!.Unmark!)
                    .Must(x => x.Count <= ValidationLimits.MaxSettlements)
                    .WithMessage($"取消標記結算數量不可超過 {ValidationLimits.MaxSettlements}");

                RuleForEach(x => x.Settlements!.Unmark!)
                    .SetValidator(new DeltaSettlementDtoValidator());
            });
        });

        // BillMeta
        When(x => x.BillMeta != null, () =>
        {
            When(x => x.BillMeta!.Name != null, () =>
            {
                RuleFor(x => x.BillMeta!.Name!)
                    .MaximumLength(ValidationLimits.MaxNameLength)
                    .WithMessage("帳單名稱不可超過 200 字元");
            });
        });
    }
}

/// <summary>
/// 新增成員資料驗證器 (DeltaSync)
/// </summary>
public class MemberAddDtoValidator : AbstractValidator<MemberAddDto>
{
    public MemberAddDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地成員 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地成員 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("成員名稱不可為空")
            .MaximumLength(ValidationLimits.MaxMemberNameLength).WithMessage("成員名稱不可超過 100 字元");

        When(x => x.DisplayOrder != null, () =>
        {
            RuleFor(x => x.DisplayOrder!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("顯示順序必須大於或等於 0");
        });
    }
}

/// <summary>
/// 更新成員資料驗證器 (DeltaSync)
/// </summary>
public class MemberUpdateDtoValidator : AbstractValidator<MemberUpdateDto>
{
    public MemberUpdateDtoValidator()
    {
        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name!)
                .MaximumLength(ValidationLimits.MaxMemberNameLength)
                .WithMessage("成員名稱不可超過 100 字元");
        });

        When(x => x.DisplayOrder != null, () =>
        {
            RuleFor(x => x.DisplayOrder!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("顯示順序必須大於或等於 0");
        });
    }
}

/// <summary>
/// 新增費用資料驗證器 (DeltaSync)
/// </summary>
public class ExpenseAddDtoValidator : AbstractValidator<ExpenseAddDto>
{
    public ExpenseAddDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地費用 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地費用 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("費用名稱不可為空")
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("費用名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");

        When(x => x.ServiceFeePercent != null, () =>
        {
            RuleFor(x => x.ServiceFeePercent!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
                .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");
        });

        When(x => x.PaidByMemberId != null, () =>
        {
            RuleFor(x => x.PaidByMemberId!)
                .MaximumLength(ValidationLimits.MaxLocalIdLength)
                .WithMessage("付款者 ID 不可超過 100 字元");
        });

        When(x => x.ParticipantIds != null, () =>
        {
            RuleFor(x => x.ParticipantIds!)
                .Must(x => x.Count <= ValidationLimits.MaxParticipantIds)
                .WithMessage($"參與者數量不可超過 {ValidationLimits.MaxParticipantIds}");
        });
    }
}

/// <summary>
/// 更新費用資料驗證器 (DeltaSync)
/// </summary>
public class ExpenseUpdateDtoValidator : AbstractValidator<ExpenseUpdateDto>
{
    public ExpenseUpdateDtoValidator()
    {
        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name!)
                .MaximumLength(ValidationLimits.MaxNameLength)
                .WithMessage("費用名稱不可超過 200 字元");
        });

        When(x => x.Amount != null, () =>
        {
            RuleFor(x => x.Amount!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
                .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");
        });

        When(x => x.ServiceFeePercent != null, () =>
        {
            RuleFor(x => x.ServiceFeePercent!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("服務費百分比必須大於或等於 0")
                .LessThanOrEqualTo(100).WithMessage("服務費百分比不可超過 100%");
        });

        When(x => x.PaidByMemberId != null, () =>
        {
            RuleFor(x => x.PaidByMemberId!)
                .MaximumLength(ValidationLimits.MaxLocalIdLength)
                .WithMessage("付款者 ID 不可超過 100 字元");
        });

        When(x => x.ParticipantIds != null, () =>
        {
            RuleFor(x => x.ParticipantIds!)
                .Must(x => x.Count <= ValidationLimits.MaxParticipantIds)
                .WithMessage($"參與者數量不可超過 {ValidationLimits.MaxParticipantIds}");
        });
    }
}

/// <summary>
/// 新增費用細項資料驗證器 (DeltaSync)
/// </summary>
public class ExpenseItemAddDtoValidator : AbstractValidator<ExpenseItemAddDto>
{
    public ExpenseItemAddDtoValidator()
    {
        RuleFor(x => x.LocalId)
            .NotEmpty().WithMessage("本地細項 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("本地細項 ID 不可超過 100 字元");

        RuleFor(x => x.ExpenseId)
            .NotEmpty().WithMessage("費用 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("費用 ID 不可超過 100 字元");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("細項名稱不可為空")
            .MaximumLength(ValidationLimits.MaxNameLength).WithMessage("細項名稱不可超過 200 字元");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
            .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");

        When(x => x.PaidByMemberId != null, () =>
        {
            RuleFor(x => x.PaidByMemberId!)
                .MaximumLength(ValidationLimits.MaxLocalIdLength)
                .WithMessage("付款者 ID 不可超過 100 字元");
        });

        When(x => x.ParticipantIds != null, () =>
        {
            RuleFor(x => x.ParticipantIds!)
                .Must(x => x.Count <= ValidationLimits.MaxParticipantIds)
                .WithMessage($"參與者數量不可超過 {ValidationLimits.MaxParticipantIds}");
        });
    }
}

/// <summary>
/// 更新費用細項資料驗證器 (DeltaSync)
/// </summary>
public class ExpenseItemUpdateDtoValidator : AbstractValidator<ExpenseItemUpdateDto>
{
    public ExpenseItemUpdateDtoValidator()
    {
        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name!)
                .MaximumLength(ValidationLimits.MaxNameLength)
                .WithMessage("細項名稱不可超過 200 字元");
        });

        When(x => x.Amount != null, () =>
        {
            RuleFor(x => x.Amount!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("金額必須大於或等於 0")
                .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("金額不可超過 999,999,999.99");
        });

        When(x => x.PaidByMemberId != null, () =>
        {
            RuleFor(x => x.PaidByMemberId!)
                .MaximumLength(ValidationLimits.MaxLocalIdLength)
                .WithMessage("付款者 ID 不可超過 100 字元");
        });

        When(x => x.ParticipantIds != null, () =>
        {
            RuleFor(x => x.ParticipantIds!)
                .Must(x => x.Count <= ValidationLimits.MaxParticipantIds)
                .WithMessage($"參與者數量不可超過 {ValidationLimits.MaxParticipantIds}");
        });
    }
}

/// <summary>
/// 結算資料驗證器 (DeltaSync)
/// </summary>
public class DeltaSettlementDtoValidator : AbstractValidator<DeltaSettlementDto>
{
    public DeltaSettlementDtoValidator()
    {
        RuleFor(x => x.FromMemberId)
            .NotEmpty().WithMessage("支付成員 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("支付成員 ID 不可超過 100 字元");

        RuleFor(x => x.ToMemberId)
            .NotEmpty().WithMessage("接收成員 ID 不可為空")
            .MaximumLength(ValidationLimits.MaxLocalIdLength).WithMessage("接收成員 ID 不可超過 100 字元");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("結算金額必須大於 0")
            .LessThanOrEqualTo(ValidationLimits.MaxAmount).WithMessage("結算金額不可超過 999,999,999.99");
    }
}

#endregion
