using Kayden.Commons.Extensions;

namespace KaydenTools.Services.Tests.Common;

public class MoneyHelperTests
{
    #region Allocate 基本測試

    [Fact]
    public void Allocate_單人_應返回原始金額()
    {
        var result = MoneyHelper.Allocate(100m, 1);

        Assert.Single(result);
        Assert.Equal(100m, result[0]);
    }

    [Fact]
    public void Allocate_整除金額_應平均分配()
    {
        var result = MoneyHelper.Allocate(100m, 4);

        Assert.Equal(4, result.Length);
        Assert.All(result, amount => Assert.Equal(25m, amount));
        Assert.Equal(100m, result.Sum());
    }

    [Fact]
    public void Allocate_無法整除_應使用Penny分配()
    {
        // 100 / 3 = 33.333...
        var result = MoneyHelper.Allocate(100m, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(33.34m, result[0]); // 前面的人多得 1 分
        Assert.Equal(33.33m, result[1]);
        Assert.Equal(33.33m, result[2]);
        Assert.Equal(100m, result.Sum());
    }

    [Fact]
    public void Allocate_兩分錢餘數_前兩人各多一分()
    {
        // 10 / 3 = 3.333...
        var result = MoneyHelper.Allocate(10m, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(3.34m, result[0]); // 多 1 分
        Assert.Equal(3.33m, result[1]);
        Assert.Equal(3.33m, result[2]);
        Assert.Equal(10m, result.Sum());
    }

    [Fact]
    public void Allocate_五人分攤_應正確分配()
    {
        // 100 / 5 = 20 (整除)
        var result = MoneyHelper.Allocate(100m, 5);

        Assert.Equal(5, result.Length);
        Assert.All(result, amount => Assert.Equal(20m, amount));
        Assert.Equal(100m, result.Sum());
    }

    [Fact]
    public void Allocate_七人分攤非整除_應正確分配Penny()
    {
        // 100 / 7 = 14.285714...
        // 基礎金額: 14.28
        // 餘數: 100 - (14.28 * 7) = 100 - 99.96 = 0.04 = 4 分
        var result = MoneyHelper.Allocate(100m, 7);

        Assert.Equal(7, result.Length);
        Assert.Equal(100m, result.Sum());
        // 前 4 人得 14.29，後 3 人得 14.28
        Assert.Equal(14.29m, result[0]);
        Assert.Equal(14.29m, result[1]);
        Assert.Equal(14.29m, result[2]);
        Assert.Equal(14.29m, result[3]);
        Assert.Equal(14.28m, result[4]);
        Assert.Equal(14.28m, result[5]);
        Assert.Equal(14.28m, result[6]);
    }

    #endregion

    #region Allocate 邊界條件

    [Fact]
    public void Allocate_零金額_應返回零陣列()
    {
        var result = MoneyHelper.Allocate(0m, 3);

        Assert.Equal(3, result.Length);
        Assert.All(result, amount => Assert.Equal(0m, amount));
    }

    [Fact]
    public void Allocate_人數為零_應拋出異常()
    {
        Assert.Throws<ArgumentException>(() => MoneyHelper.Allocate(100m, 0));
    }

    [Fact]
    public void Allocate_負人數_應拋出異常()
    {
        Assert.Throws<ArgumentException>(() => MoneyHelper.Allocate(100m, -1));
    }

    [Fact]
    public void Allocate_小金額_應正確分配()
    {
        // 0.01 / 2 = 0.005 => 基礎 0.00，餘數 1 分
        var result = MoneyHelper.Allocate(0.01m, 2);

        Assert.Equal(2, result.Length);
        Assert.Equal(0.01m, result[0]);
        Assert.Equal(0m, result[1]);
        Assert.Equal(0.01m, result.Sum());
    }

    [Fact]
    public void Allocate_大金額_應正確分配()
    {
        var result = MoneyHelper.Allocate(1000000.99m, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(1000000.99m, result.Sum());
    }

    #endregion

    #region CalculateAmountWithServiceFee 測試

    [Fact]
    public void CalculateAmountWithServiceFee_10服務費_應正確計算()
    {
        var result = MoneyHelper.CalculateAmountWithServiceFee(100m, 10m);
        Assert.Equal(110m, result);
    }

    [Fact]
    public void CalculateAmountWithServiceFee_無服務費_應返回原金額()
    {
        var result = MoneyHelper.CalculateAmountWithServiceFee(100m, 0m);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void CalculateAmountWithServiceFee_小數服務費_應四捨五入()
    {
        // 100 * 1.155 = 115.5 => 115.50
        var result = MoneyHelper.CalculateAmountWithServiceFee(100m, 15.5m);
        Assert.Equal(115.5m, result);
    }

    #endregion

    #region CalculateServiceFee 測試

    [Fact]
    public void CalculateServiceFee_10服務費_應正確計算()
    {
        var result = MoneyHelper.CalculateServiceFee(100m, 10m);
        Assert.Equal(10m, result);
    }

    [Fact]
    public void CalculateServiceFee_無服務費_應返回零()
    {
        var result = MoneyHelper.CalculateServiceFee(100m, 0m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateServiceFee_需四捨五入_應正確處理()
    {
        // 99 * 0.1 = 9.9
        var result = MoneyHelper.CalculateServiceFee(99m, 10m);
        Assert.Equal(9.9m, result);
    }

    #endregion
}
