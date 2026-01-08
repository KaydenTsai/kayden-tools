using KaydenTools.Models.Shared.Entities;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories;
using KaydenTools.TestUtilities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaydenTools.TestUtilities.Database;

/// <summary>
/// 資料庫整合測試基底類別
/// 提供 DI 容器、資料隔離、Seeder
/// </summary>
public abstract class DatabaseTestBase : UnitTestBase, IAsyncLifetime
{
    // Lazy Initialization：避免不需要 DB 的測試也觸發初始化
    private static DatabaseFixture? s_fixture;
    private static readonly object s_initLock = new();
    private static bool s_isDisposed;
    private static Action<IServiceCollection>? s_configureServices;

    protected static DatabaseFixture Fixture
    {
        get
        {
            EnsureInitialized();
            return s_fixture!;
        }
    }

    /// <summary>
    /// 確保 Fixture 已初始化（執行緒安全）
    /// </summary>
    private static void EnsureInitialized()
    {
        if (s_fixture != null) return;

        lock (s_initLock)
        {
            if (s_fixture == null)
            {
                // 先建立並初始化，成功後才賦值給 s_fixture
                var fixture = new DatabaseFixture(
                    TestDatabaseOptions.FromEnvironment(),
                    s_configureServices);
                fixture.Initialize();
                s_fixture = fixture;

                // 程序退出時自動清理
                AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeFixture();
            }
        }
    }

    private static void DisposeFixture()
    {
        if (s_isDisposed) return;
        lock (s_initLock)
        {
            if (!s_isDisposed)
            {
                s_fixture?.Dispose();
                s_isDisposed = true;
            }
        }
    }

    /// <summary>
    /// 當前測試的 Service Scope
    /// </summary>
    protected IServiceScope Scope { get; private set; } = null!;

    /// <summary>
    /// 當前測試的 DbContext
    /// </summary>
    protected AppDbContext Db => Scope.ServiceProvider.GetRequiredService<AppDbContext>();

    #region IAsyncLifetime

    /// <summary>
    /// 測試初始化（xUnit 自動呼叫）
    /// </summary>
    public virtual Task InitializeAsync()
    {
        // 首次初始化時設定 ConfigureServices（執行緒安全）
        lock (s_initLock)
        {
            if (s_fixture == null && s_configureServices == null)
            {
                s_configureServices = ConfigureServices;
            }
        }

        // 不再全域清空資料，每個測試使用唯一 GUID 避免干擾
        // 如需清空可在個別測試中呼叫 CleanupAsync()
        Scope = Fixture.CreateScope();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 測試清理（xUnit 自動呼叫）
    /// </summary>
    public virtual Task DisposeAsync()
    {
        Scope?.Dispose();
        return Task.CompletedTask;
    }

    #endregion

    #region Virtual Methods

    /// <summary>
    /// 自訂 DI 服務註冊（子類可覆寫）
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    #endregion

    #region Service Access

    /// <summary>
    /// 取得服務實例
    /// </summary>
    protected T GetService<T>() where T : notnull
        => Scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// 嘗試取得服務實例
    /// </summary>
    protected T? GetServiceOrDefault<T>() where T : class
        => Scope.ServiceProvider.GetService<T>();

    /// <summary>
    /// 建立新的獨立 Service Scope（用於併發測試）
    /// </summary>
    /// <remarks>
    /// 每個 Scope 有自己的 DbContext 實例，
    /// 可用於模擬多個用戶同時操作的情境
    /// </remarks>
    protected IServiceScope CreateNewScope()
        => Fixture.CreateScope();

    #endregion

    #region Data Seeding - 建立並存入資料庫

    /// <summary>
    /// 建立使用者並存入資料庫
    /// </summary>
    protected async Task<User> SeedUser(
        string? email = null,
        string? displayName = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"test-{Guid.NewGuid():N}@example.com",
            DisplayName = displayName ?? Faker.Name.FullName(),
            PrimaryProvider = Models.Shared.Enums.AuthProvider.Google,
            GoogleUserId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// 建立帳單並存入資料庫
    /// </summary>
    /// <remarks>
    /// 返回的 Bill 物件是 EF Core 追蹤的實體。
    /// 如果需要在 BillService 操作後獲取最新狀態，請使用 ReloadBillFromDb()
    /// </remarks>
    protected async Task<Bill> SeedBill(
        string? name = null,
        Guid? ownerId = null,
        Action<Bill>? customize = null)
    {
        var bill = MakeBill(name, ownerId, customize);
        Db.Bills.Add(bill);
        await Db.SaveChangesAsync();
        return bill;
    }

    /// <summary>
    /// 建立成員並存入資料庫
    /// </summary>
    /// <remarks>
    /// 注意：不會自動加入 bill.Members 集合，避免 EF Core 追蹤衝突。
    /// 如果需要完整的帳單資料，請使用 ReloadBillFromDb()
    /// </remarks>
    protected async Task<Member> SeedMember(
        Bill bill,
        string? name = null,
        int? displayOrder = null,
        Guid? linkedUserId = null,
        Action<Member>? customize = null)
    {
        // 查詢現有成員數量來決定 displayOrder
        var existingCount = await Db.Members.CountAsync(m => m.BillId == bill.Id);
        var order = displayOrder ?? existingCount;

        var member = MakeMember(name, order, linkedUserId, customize);
        member.BillId = bill.Id;

        Db.Members.Add(member);
        await Db.SaveChangesAsync();

        // 不手動加入 bill.Members，避免 EF Core 追蹤衝突
        return member;
    }

    /// <summary>
    /// 建立費用並存入資料庫
    /// </summary>
    /// <remarks>
    /// 注意：不會自動加入 bill.Expenses 集合，避免 EF Core 追蹤衝突。
    /// 如果需要完整的帳單資料，請使用 ReloadBillFromDb()
    /// </remarks>
    protected async Task<Expense> SeedExpense(
        Bill bill,
        string? name = null,
        decimal? amount = null,
        Member? paidBy = null,
        IEnumerable<Member>? participants = null,
        bool isItemized = false,
        Action<Expense>? customize = null)
    {
        var expense = MakeExpense(name, amount, paidBy?.Id, isItemized, customize);
        expense.BillId = bill.Id;

        // 使用導航屬性加入參與者（讓 EF Core 正確處理 FK）
        if (participants != null)
        {
            foreach (var participant in participants)
            {
                expense.Participants.Add(new ExpenseParticipant
                {
                    Expense = expense,
                    MemberId = participant.Id
                });
            }
        }

        Db.Expenses.Add(expense);
        await Db.SaveChangesAsync();

        // 不手動加入 bill.Expenses，避免 EF Core 追蹤衝突
        return expense;
    }

    /// <summary>
    /// 建立費用細項並存入資料庫
    /// </summary>
    /// <remarks>
    /// 注意：不會自動加入 expense.Items 集合，避免 EF Core 追蹤衝突
    /// </remarks>
    protected async Task<ExpenseItem> SeedExpenseItem(
        Expense expense,
        string? name = null,
        decimal? amount = null,
        Member? paidBy = null,
        IEnumerable<Member>? participants = null,
        Action<ExpenseItem>? customize = null)
    {
        var item = MakeExpenseItem(name, amount, paidBy?.Id, customize);
        item.ExpenseId = expense.Id;

        // 使用導航屬性加入參與者
        if (participants != null)
        {
            foreach (var participant in participants)
            {
                item.Participants.Add(new ExpenseItemParticipant
                {
                    ExpenseItem = item,
                    MemberId = participant.Id
                });
            }
        }

        Db.ExpenseItems.Add(item);
        await Db.SaveChangesAsync();

        // 不手動加入 expense.Items，避免 EF Core 追蹤衝突
        return item;
    }

    /// <summary>
    /// 建立完整的帳單（含成員和費用）並存入資料庫
    /// </summary>
    /// <remarks>
    /// 返回從資料庫重新載入的帳單，包含所有關聯資料
    /// </remarks>
    protected async Task<Bill> SeedCompleteBill(
        int memberCount = 2,
        int expenseCount = 1,
        string? name = null,
        Guid? ownerId = null)
    {
        var bill = await SeedBill(name, ownerId);

        // 建立成員
        var members = new List<Member>();
        for (int i = 0; i < memberCount; i++)
        {
            var member = await SeedMember(bill, displayOrder: i);
            members.Add(member);
        }

        // 建立費用
        for (int i = 0; i < expenseCount; i++)
        {
            var payer = members[i % memberCount];
            await SeedExpense(bill, paidBy: payer, participants: members);
        }

        // 清除追蹤並重新載入，確保返回完整資料
        ClearChangeTracker();
        return (await ReloadBillFromDb(bill.Id))!;
    }

    /// <summary>
    /// 清除 EF Core 變更追蹤器
    /// </summary>
    /// <remarks>
    /// 在測試中切換不同操作情境時使用，避免追蹤衝突
    /// </remarks>
    protected void ClearChangeTracker()
    {
        Db.ChangeTracker.Clear();
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// 斷言帳單數量
    /// </summary>
    protected async Task AssertBillCount(int expected)
    {
        var count = await Db.Bills.CountAsync();
        Assert.Equal(expected, count);
    }

    /// <summary>
    /// 斷言成員數量
    /// </summary>
    protected async Task AssertMemberCount(Guid billId, int expected)
    {
        var count = await Db.Members.CountAsync(m => m.BillId == billId);
        Assert.Equal(expected, count);
    }

    /// <summary>
    /// 斷言費用數量
    /// </summary>
    protected async Task AssertExpenseCount(Guid billId, int expected)
    {
        var count = await Db.Expenses.CountAsync(e => e.BillId == billId);
        Assert.Equal(expected, count);
    }

    /// <summary>
    /// 斷言費用參與者數量
    /// </summary>
    protected async Task AssertExpenseParticipantCount(Guid expenseId, int expected)
    {
        var count = await Db.ExpenseParticipants.CountAsync(p => p.ExpenseId == expenseId);
        Assert.Equal(expected, count);
    }

    /// <summary>
    /// 重新從資料庫載入帳單（繞過 EF 快取）
    /// </summary>
    protected async Task<Bill?> ReloadBillFromDb(Guid billId)
    {
        return await Db.Bills
            .AsNoTracking()
            .Include(b => b.Members)
            .Include(b => b.Expenses)
                .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
                .ThenInclude(e => e.Items)
                    .ThenInclude(i => i.Participants)
            .Include(b => b.SettledTransfers)
            .FirstOrDefaultAsync(b => b.Id == billId);
    }

    /// <summary>
    /// 斷言帳單不存在
    /// </summary>
    protected async Task AssertBillNotExists(Guid billId)
    {
        var exists = await Db.Bills.AnyAsync(b => b.Id == billId);
        Assert.False(exists, $"帳單 {billId} 不應存在");
    }

    /// <summary>
    /// 斷言帳單已結清
    /// </summary>
    protected async Task AssertBillSettled(Guid billId, bool isSettled = true)
    {
        var bill = await Db.Bills.FindAsync(billId);
        Assert.NotNull(bill);
        Assert.Equal(isSettled, bill.IsSettled);
    }

    /// <summary>
    /// 斷言已結清轉帳數量
    /// </summary>
    protected async Task AssertSettledTransferCount(Guid billId, int expected)
    {
        var count = await Db.SettledTransfers.CountAsync(st => st.BillId == billId);
        Assert.Equal(expected, count);
    }

    /// <summary>
    /// 斷言費用細項數量
    /// </summary>
    protected async Task AssertExpenseItemCount(Guid expenseId, int expected)
    {
        var count = await Db.ExpenseItems.CountAsync(i => i.ExpenseId == expenseId);
        Assert.Equal(expected, count);
    }

    #endregion
}
