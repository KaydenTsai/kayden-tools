namespace KaydenTools.TestUtilities.Database;

/// <summary>
/// 測試資料庫連線設定
/// </summary>
public class TestDatabaseOptions
{
    public const string ConnectionStringEnvVar = "TEST_DB_CONNECTION_STRING";

    private const string DefaultConnectionString =
        "User ID=postgres;Password=Nt0EPEe46Xn8NMd4dVdUBYrEYLkHecet;Host=localhost;Port=5432;Database=KaydenToolsTest;Pooling=true;Maximum Pool Size=80";

    /// <summary>
    /// 資料庫連線字串
    /// </summary>
    public string ConnectionString { get; set; } = DefaultConnectionString;

    /// <summary>
    /// 每個測試前是否清空資料
    /// </summary>
    public bool CleanupBeforeEachTest { get; set; } = true;

    /// <summary>
    /// 是否執行 Migration
    /// </summary>
    public bool RunMigrations { get; set; } = true;

    /// <summary>
    /// 從環境變數建立選項（推薦方式）
    /// </summary>
    public static TestDatabaseOptions FromEnvironment()
    {
        var options = new TestDatabaseOptions();
        var envConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            options.ConnectionString = envConnectionString;
        }

        return options;
    }
}
