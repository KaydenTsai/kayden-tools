using FluentAssertions;
using FluentValidation.TestHelper;
using KaydenTools.Api.Validators;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Tests.Validators;

public class DeltaSyncValidatorTests
{
    private readonly DeltaSyncRequestValidator _validator = new();

    #region DeltaSyncRequest — Basic

    [Fact]
    public void EmptyRequest_ShouldPass()
    {
        var request = new DeltaSyncRequest();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BaseVersion_Negative_ShouldFail()
    {
        var request = new DeltaSyncRequest { BaseVersion = -1 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.BaseVersion);
    }

    [Fact]
    public void BaseVersion_Zero_ShouldPass()
    {
        var request = new DeltaSyncRequest { BaseVersion = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.BaseVersion);
    }

    #endregion

    #region DeltaSyncRequest — Collection Size Limits

    [Fact]
    public void Members_Add_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Members = new MemberChangesDto
            {
                Add = Enumerable.Range(0, ValidationLimits.MaxMembers + 1)
                    .Select(i => new MemberAddDto { LocalId = $"m{i}", Name = $"Member {i}" })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Members_Add_AtLimit_ShouldPass()
    {
        var request = new DeltaSyncRequest
        {
            Members = new MemberChangesDto
            {
                Add = Enumerable.Range(0, ValidationLimits.MaxMembers)
                    .Select(i => new MemberAddDto { LocalId = $"m{i}", Name = $"Member {i}" })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Members_Update_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Members = new MemberChangesDto
            {
                Update = Enumerable.Range(0, ValidationLimits.MaxMembers + 1)
                    .Select(_ => new MemberUpdateDto { RemoteId = Guid.NewGuid() })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Members_Delete_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Members = new MemberChangesDto
            {
                Delete = Enumerable.Range(0, ValidationLimits.MaxDeleteIds + 1)
                    .Select(_ => Guid.NewGuid())
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Expenses_Add_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Expenses = new ExpenseChangesDto
            {
                Add = Enumerable.Range(0, ValidationLimits.MaxExpenses + 1)
                    .Select(i => new ExpenseAddDto { LocalId = $"e{i}", Name = $"Expense {i}" })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ExpenseItems_Add_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            ExpenseItems = new ExpenseItemChangesDto
            {
                Add = Enumerable.Range(0, ValidationLimits.MaxExpenseItems + 1)
                    .Select(i => new ExpenseItemAddDto { LocalId = $"ei{i}", ExpenseId = "e1", Name = $"Item {i}" })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Settlements_Mark_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Settlements = new SettlementChangesDto
            {
                Mark = Enumerable.Range(0, ValidationLimits.MaxSettlements + 1)
                    .Select(i => new DeltaSettlementDto
                    {
                        FromMemberId = $"from{i}",
                        ToMemberId = $"to{i}",
                        Amount = 100
                    })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Settlements_Unmark_ExceedsLimit_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            Settlements = new SettlementChangesDto
            {
                Unmark = Enumerable.Range(0, ValidationLimits.MaxSettlements + 1)
                    .Select(i => new DeltaSettlementDto
                    {
                        FromMemberId = $"from{i}",
                        ToMemberId = $"to{i}",
                        Amount = 100
                    })
                    .ToList()
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region DeltaSyncRequest — BillMeta

    [Fact]
    public void BillMeta_Name_ExceedsMaxLength_ShouldFail()
    {
        var request = new DeltaSyncRequest
        {
            BillMeta = new BillMetaChangesDto
            {
                Name = new string('x', ValidationLimits.MaxNameLength + 1)
            }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BillMeta_Name_AtMaxLength_ShouldPass()
    {
        var request = new DeltaSyncRequest
        {
            BillMeta = new BillMetaChangesDto
            {
                Name = new string('x', ValidationLimits.MaxNameLength)
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}

public class MemberAddDtoValidatorTests
{
    private readonly MemberAddDtoValidator _validator = new();

    [Fact]
    public void LocalId_Empty_ShouldFail()
    {
        var dto = new MemberAddDto { LocalId = "", Name = "Alice" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.LocalId);
    }

    [Fact]
    public void Name_Empty_ShouldFail()
    {
        var dto = new MemberAddDto { LocalId = "m1", Name = "" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_ExceedsMaxLength_ShouldFail()
    {
        var dto = new MemberAddDto
        {
            LocalId = "m1",
            Name = new string('x', ValidationLimits.MaxMemberNameLength + 1)
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void LocalId_ExceedsMaxLength_ShouldFail()
    {
        var dto = new MemberAddDto
        {
            LocalId = new string('x', ValidationLimits.MaxLocalIdLength + 1),
            Name = "Alice"
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.LocalId);
    }

    [Fact]
    public void DisplayOrder_Negative_ShouldFail()
    {
        var dto = new MemberAddDto { LocalId = "m1", Name = "Alice", DisplayOrder = -1 };
        var result = _validator.TestValidate(dto);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Valid_ShouldPass()
    {
        var dto = new MemberAddDto { LocalId = "m1", Name = "Alice", DisplayOrder = 0 };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class ExpenseAddDtoValidatorTests
{
    private readonly ExpenseAddDtoValidator _validator = new();

    [Fact]
    public void Amount_Negative_ShouldFail()
    {
        var dto = new ExpenseAddDto { LocalId = "e1", Name = "Dinner", Amount = -1 };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_ExceedsMax_ShouldFail()
    {
        var dto = new ExpenseAddDto
        {
            LocalId = "e1",
            Name = "Dinner",
            Amount = ValidationLimits.MaxAmount + 1
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_AtMax_ShouldPass()
    {
        var dto = new ExpenseAddDto
        {
            LocalId = "e1",
            Name = "Dinner",
            Amount = ValidationLimits.MaxAmount
        };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ServiceFeePercent_Over100_ShouldFail()
    {
        var dto = new ExpenseAddDto
        {
            LocalId = "e1",
            Name = "Dinner",
            Amount = 100,
            ServiceFeePercent = 101
        };
        var result = _validator.TestValidate(dto);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ServiceFeePercent_Negative_ShouldFail()
    {
        var dto = new ExpenseAddDto
        {
            LocalId = "e1",
            Name = "Dinner",
            Amount = 100,
            ServiceFeePercent = -1
        };
        var result = _validator.TestValidate(dto);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParticipantIds_ExceedsLimit_ShouldFail()
    {
        var dto = new ExpenseAddDto
        {
            LocalId = "e1",
            Name = "Dinner",
            Amount = 100,
            ParticipantIds = Enumerable.Range(0, ValidationLimits.MaxParticipantIds + 1)
                .Select(i => $"p{i}").ToList()
        };
        var result = _validator.TestValidate(dto);
        result.IsValid.Should().BeFalse();
    }
}

public class DeltaSettlementDtoValidatorTests
{
    private readonly DeltaSettlementDtoValidator _validator = new();

    [Fact]
    public void Amount_Zero_ShouldFail()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "m1",
            ToMemberId = "m2",
            Amount = 0
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_Negative_ShouldFail()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "m1",
            ToMemberId = "m2",
            Amount = -10
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_Positive_ShouldPass()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "m1",
            ToMemberId = "m2",
            Amount = 100
        };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FromMemberId_Empty_ShouldFail()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "",
            ToMemberId = "m2",
            Amount = 100
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FromMemberId);
    }

    [Fact]
    public void ToMemberId_Empty_ShouldFail()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "m1",
            ToMemberId = "",
            Amount = 100
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ToMemberId);
    }

    [Fact]
    public void Amount_ExceedsMax_ShouldFail()
    {
        var dto = new DeltaSettlementDto
        {
            FromMemberId = "m1",
            ToMemberId = "m2",
            Amount = ValidationLimits.MaxAmount + 1
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }
}
