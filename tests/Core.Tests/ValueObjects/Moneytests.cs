using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace SharedKernel.Tests.ValueObjects;

public class Moneytests
{
    // ---------------------------------------------------------------------
    // Construção / Invariantes
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_WithPositiveValue_SetsValueCorrectly()
    {
        // Arrange & Act
        var money = new Money(150.75m);

        // Assert
        Assert.Equal(150.75m, money.Value);
    }

    [Fact]
    public void Constructor_WithZero_SetsValueToZero()
    {
        // Arrange & Act
        var money = new Money(0m);

        // Assert
        Assert.Equal(0m, money.Value);
    }

    [Fact]
    public void Constructor_WithNegativeValue_ThrowsDomainException()
    {
        // Arrange & Act
        Action act = () => new Money(-10m);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData(10.125, 10.12)] // dígito de arredondamento (1) é ímpar -> arredonda para o par mais próximo abaixo (2)... 
    [InlineData(10.135, 10.14)] // banker's rounding: 3 -> par acima (4)
    [InlineData(10.005, 10.00)] // 0 já é par, mantém
    [InlineData(10.015, 10.02)] // 1 é ímpar, arredonda para 2
    public void Constructor_RoundsValueToTwoDecimalPlaces_UsingBankersRounding(decimal input, decimal expected)
    {
        // Arrange & Act
        var money = new Money(input);

        // Assert
        Assert.Equal(expected, money.Value);
    }

    [Fact]
    public void Constructor_WithMoreThanTwoDecimalPlaces_RoundsToTwoDecimalPlaces()
    {
        // Arrange & Act
        var money = new Money(99.999m);

        // Assert
        Assert.Equal(100.00m, money.Value);
    }

    // ---------------------------------------------------------------------
    // Zero
    // ---------------------------------------------------------------------

    [Fact]
    public void Zero_ReturnsMoneyInstanceWithValueZero()
    {
        // Arrange & Act
        var zero = Money.Zero;

        // Assert
        Assert.Equal(0m, zero.Value);
    }

    // ---------------------------------------------------------------------
    // Operador +
    // ---------------------------------------------------------------------

    [Fact]
    public void Addition_TwoPositiveValues_ReturnsSum()
    {
        // Arrange
        var a = new Money(100.50m);
        var b = new Money(50.25m);

        // Act
        var result = a + b;

        // Assert
        Assert.Equal(150.75m, result.Value);
    }

    [Fact]
    public void Addition_WithZero_ReturnsOriginalValue()
    {
        // Arrange
        var a = new Money(100m);

        // Act
        var result = a + Money.Zero;

        // Assert
        Assert.Equal(100m, result.Value);
    }

    // ---------------------------------------------------------------------
    // Operador -
    // ---------------------------------------------------------------------

    [Fact]
    public void Subtraction_WhenLeftIsGreaterThanRight_ReturnsDifference()
    {
        // Arrange
        var a = new Money(100m);
        var b = new Money(30m);

        // Act
        var result = a - b;

        // Assert
        Assert.Equal(70m, result.Value);
    }

    [Fact]
    public void Subtraction_WhenValuesAreEqual_ReturnsZero()
    {
        // Arrange
        var a = new Money(50m);
        var b = new Money(50m);

        // Act
        var result = a - b;

        // Assert
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public void Subtraction_WhenResultWouldBeNegative_ThrowsDomainException()
    {
        // Arrange
        var a = new Money(10m);
        var b = new Money(20m);

        // Act
        Action act = () => { var _ = a - b; };

        // Assert
        Assert.Throws<DomainException>(act);
    }

    // ---------------------------------------------------------------------
    // Operador *
    // ---------------------------------------------------------------------

    [Fact]
    public void Multiplication_ByPositiveFactor_ReturnsProduct()
    {
        // Arrange
        var money = new Money(10m);

        // Act
        var result = money * 3m;

        // Assert
        Assert.Equal(30m, result.Value);
    }

    [Fact]
    public void Multiplication_ByZeroFactor_ReturnsZero()
    {
        // Arrange
        var money = new Money(500m);

        // Act
        var result = money * 0m;

        // Assert
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public void Multiplication_ByNegativeFactor_ThrowsDomainException()
    {
        // Arrange
        var money = new Money(10m);

        // Act
        Action act = () => { var _ = money * -1m; };

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Multiplication_ResultIsRoundedToTwoDecimalPlaces()
    {
        // Arrange
        var money = new Money(10m);

        // Act
        var result = money * 0.333m;

        // Assert
        Assert.Equal(3.33m, result.Value);
    }

    // ---------------------------------------------------------------------
    // Operador /
    // ---------------------------------------------------------------------

    [Fact]
    public void Division_ByPositiveDivisor_ReturnsQuotient()
    {
        // Arrange
        var money = new Money(100m);

        // Act
        var result = money / 4m;

        // Assert
        Assert.Equal(25m, result.Value);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange
        var money = new Money(100m);

        // Act
        Action act = () => { var _ = money / 0m; };

        // Assert
        Assert.Throws<DivideByZeroException>(act);
    }

    [Fact]
    public void Division_ByNegativeDivisor_ThrowsDomainException()
    {
        // Arrange
        var money = new Money(100m);

        // Act
        Action act = () => { var _ = money / -2m; };

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Division_ResultIsRoundedToTwoDecimalPlaces()
    {
        // Arrange
        var money = new Money(10m);

        // Act
        var result = money / 3m;

        // Assert
        Assert.Equal(3.33m, result.Value);
    }

    // ---------------------------------------------------------------------
    // ToString
    // ---------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsValueFormattedWithTwoDecimalPlaces()
    {
        // Arrange
        // "N2" é sensível à cultura (separador decimal/milhar), então fixamos
        // a cultura da thread durante o teste para evitar falhas intermitentes
        // em máquinas/CI configuradas com culturas diferentes de en-US.
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            var money = new Money(1234.5m);

            // Act
            var result = money.ToString();

            // Assert
            Assert.Equal(1234.5m.ToString("N2", CultureInfo.CurrentCulture), result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ToString_WithZero_ReturnsZeroFormatted()
    {
        // Arrange
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            // Act
            var result = Money.Zero.ToString();

            // Assert
            Assert.Equal(0m.ToString("N2", CultureInfo.CurrentCulture), result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // ---------------------------------------------------------------------
    // Igualdade (record struct → igualdade por valor "de fábrica")
    // ---------------------------------------------------------------------

    [Fact]
    public void Equality_TwoInstancesWithSameValue_AreEqual()
    {
        // Arrange
        var a = new Money(100m);
        var b = new Money(100m);

        // Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_TwoInstancesWithDifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new Money(100m);
        var b = new Money(200m);

        // Assert
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_ValuesThatRoundToSameResult_AreEqual()
    {
        // Arrange
        // Ambos arredondam para 10.00m antes de serem armazenados
        var a = new Money(10.001m);
        var b = new Money(10.004m);

        // Assert
        Assert.Equal(a, b);
    }
}
