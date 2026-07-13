using SharedKernel.Exceptions;
using SharedKernel.Mathematics;
using SharedKernel.ValueObjects;

namespace SharedKernel.Tests;

public sealed class FinancialTypesTests
{
    [Theory]
    [InlineData(100, 120, 20)]
    [InlineData(0, 120, 0)]
    public void CalculateReturn_ReturnsExpectedPercentage(decimal invested, decimal current, decimal expected) =>
        Assert.Equal(expected, FinancialMath.CalculateReturn(invested, current));

    [Theory]
    [InlineData(25, 100, 25)]
    [InlineData(25, 0, 0)]
    public void CalculateWeight_ReturnsExpectedPercentage(decimal position, decimal portfolio, decimal expected) =>
        Assert.Equal(expected, FinancialMath.CalculateWeight(position, portfolio));

    [Fact]
    public void CalculateTransactionCost_AppliesPointThreePercent() =>
        Assert.Equal(5.325m, FinancialMath.CalculateTransactionCost(1_775m));

    [Fact]
    public void ReturnRate_StoresPercentageAndFormatsIt()
    {
        var result = new ReturnRate(12.34567m);

        Assert.Equal(12.3457m, result.Value.Value);
        Assert.Equal(result.Value.ToString(), result.ToString());
    }

    [Fact]
    public void Volatility_StoresNonNegativePercentageAndRejectsNegativeValue()
    {
        var result = new Volatility(15.67891m);

        Assert.Equal(15.6789m, result.Value.Value);
        Assert.Equal(result.Value.ToString(), result.ToString());
        Assert.Throws<DomainException>(() => new Volatility(-0.01m));
    }

    [Fact]
    public void DomainException_PreservesMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new DomainException("domain failure", inner);

        Assert.Equal("domain failure", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
